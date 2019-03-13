using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region mdk macros
        // This script was deployed at $MDK_DATETIME$
        #endregion
        //CHANGE THE FOLLOWING VARAIBLES FOR YOUR SETUP

        //Names of blocks, minus the dock specific prefix. My setup uses "Dock 1 - Sensor" as the name of the piston of the first dock, for instance.
        //The Prefix (Dock X in this case) is gained from the name of the programming block this code runs on.
        const string DOCK_SENSOR_NAME = "Sensor";
        const string DEPART_SENSOR_NAME = "Exit Sensor";
        const string SPOTLIGHT_NAME = "Spotlight";
        const string CONNECTOR_NAME = "Connector";
        const string TIMER_SENSOR_NAME = "Sensor Timer";
        const string TIMER_VENT_NAME = "Vent Timer";
        const string TIMER_DEPART_NAME = "Departure Timer";

        const string GROUP_VENT_NAME = "Air Vent";
        const string GROUP_AIRLOCK_NAME = "Airlock";
        const string GROUP_DOOR_NAME = "Airtight Hangar Door";
        const string GROUP_EXT_LIGHTS_NAME = "Door Light";
        const string GROUP_INT_LIGHTS_NAME = "Interior Light";
        const string GROUP_WARN_LIGHTS_NAME = "Warning Light";
        const string GROUP_INT_LCDS_NAME = "Internal LCD";
        const string GROUP_EXT_LCDS_NAME = "External LCD";

        const string GROUP_INTERNAL_DISPLAY_NAME = "LCD Display";


        //DO NOT EDIT ANYTHING BEYOND THIS POINT----------------w----------------------------------------------------------

        const string POWER_ON = "OnOff_On";
        const string POWER_OFF = "OnOff_Off";

        List<IMyTerminalBlock> vents; //oxygen vents

        List<IMyTerminalBlock> airlocks; // interior airlock doors
        List<IMyTerminalBlock> hangarDoors; //big door for the ship to come through

        List<IMyTerminalBlock> doorLights;     //external lights for door
        List<IMyTerminalBlock> interiorLights; //interior lights
        List<IMyTerminalBlock> warningLights;  // warning lights around big door

        List<IMyTerminalBlock> internalTextPanels; //text panels above access doors
        List<IMyTerminalBlock> externalTextPanels; //text panels with arrow or no entry graphic
        List<IMyTerminalBlock> hangarSigns;        //signs that show the hangar name and current occupant info

        List<List<IMyTerminalBlock>> allGroups;    //list of all block groups
        List<IMyTerminalBlock> otherBlocks;

        IMySensorBlock sensor; //sensor on the landing pad to detect if/when a ship has landed
        IMySensorBlock exitSensor; //sensor by the door, detects if/when a ship has left the hangar

        IMyTimerBlock sensorTimer; //timer to pause the code till the sensor detects a ship
        IMyTimerBlock ventTimer; //timer to pause the code till the hanger is fully pressurised
        IMyTimerBlock departureTimer; //timer to pause the code till the ship has left the hanger

        IMyShipConnector connector;

        string hangarPrefix;
        string hangarNum;

        string arrowTexture = "Arrow";
        string warningTexture = "Danger";
        string stopTexture = "No Entry";

        Color red = new Color(255, 0, 0, 0);
        Color green = new Color(0, 255, 0, 0);

        int DISPLAY_WIDTH = 25;

        public Program()
        {
            Echo("COLLECTING BLOCK REFERENCES");
            String name = Me.CustomName;
            hangarPrefix = name.Split('-')[0].Trim(); //get the bit of name before the dash.

            sensor = GetDockBlock<IMySensorBlock>(DOCK_SENSOR_NAME);
            exitSensor = GetDockBlock<IMySensorBlock>(DEPART_SENSOR_NAME);

            sensorTimer = GetDockBlock<IMyTimerBlock>(TIMER_SENSOR_NAME);
            ventTimer = GetDockBlock<IMyTimerBlock>(TIMER_VENT_NAME);
            departureTimer = GetDockBlock<IMyTimerBlock>(TIMER_DEPART_NAME);
            connector = GetDockBlock<IMyShipConnector>(CONNECTOR_NAME);

            otherBlocks = new List<IMyTerminalBlock>() {
                sensor,
                exitSensor,
                sensorTimer,
                ventTimer,
                departureTimer,
                connector,
                Me
            };

            GetGroups();

            foreach (IMyTextPanel panel in hangarSigns)
            {
                ApplyDisplay(hangarPrefix, panel);
            }

            Echo("COMPLETED SETUP");
        }

        public void Main(string argument, UpdateType updateSource)
        {

            string[] args = argument.Split(',');
            switch (args[0].Trim())
            {
                case "DOCK":
                    Echo("INITIATING DOCKING PROCEDURE");

                    //change graphic of external LCDs to arrow
                    SetOutsideScreen(arrowTexture);
                    Echo(string.Format("OUTTA LCDS SET TO {0}", arrowTexture));
                    //open door
                    OpenCloseHangarDoor(true);
                    Echo("OPENING BIG DOOR");
                    PowerInteriorLights(false);
                    PowerExternalLights(true);
                    PowerWarningLights(red);
                    Echo("LIGHTS SET");

                    //light up external lights, maybe flash

                    //WAIT for sensor to detect ship landing
                    Echo("WAITING FOR SENSOR TO DETECT SHIP");
                    sensorTimer.ApplyAction(POWER_ON); //sensor timer calls this programming block with argument "SENSOR CHECK"
                    sensorTimer.ApplyAction("Start");
                    break;
                case "UNDOCK":
                    SetOutsideScreen(warningTexture);//change graphic of external LCDs to WARNING
                    SetInteriorDoorsLock(true);
                    PowerInteriorLights(false);
                    SetVentPressure(false);
                    OpenCloseHangarDoor(true);
                    Echo("ACTIVATING DEPARTURE TIMER");
                    departureTimer.ApplyAction(POWER_ON); //vent timer calls this programming block with argument "VENT CHECK"
                    departureTimer.ApplyAction("Start");
                    break;
                case "SENSOR CHECK":
                    if (CheckSensor())
                    {
                        Echo("SENSOR DETECTED SHIP");
                        sensorTimer.ApplyAction(POWER_OFF);
                        //close big door
                        OpenCloseHangarDoor(false);
                        SetOutsideScreen(stopTexture); //external LCDs to NO ENTRY
                        Echo(string.Format("OUTTA LCDS SET TO {0}", stopTexture));
                        //vents pressurise, wait until complete
                        SetVentPressure(true);
                        Echo("WAITING FOR PRESSURIZATION");
                        ventTimer.ApplyAction(POWER_ON); //vent timer calls this programming block with argument "VENT CHECK"
                        ventTimer.ApplyAction("Start");
                    }
                    break;
                case "VENT CHECK":
                    float pressure = GetVentPressure();
                    Echo(string.Format("CURRENT PRESSURE: {0}%", pressure * 100));
                    if (pressure == 1)
                    {
                        Echo("HANGER PRESSURIZED");
                        ventTimer.ApplyAction(POWER_OFF);
                        PowerWarningLights(green);

                        PowerInteriorLights(true);
                        SetInteriorDoorsLock(false);
                        PowerExternalLights(false);
                    }
                    break;
                case "DEPARTURE CHECK":
                    if (!IsDockOccupied())
                    {
                        departureTimer.ApplyAction(POWER_OFF);
                        OpenCloseHangarDoor(false);
                        SetOutsideScreen(stopTexture);
                        PowerExternalLights(false);
                        PowerWarningLights(red);
                    }
                    break;
                case "RENAME":
                    string new_prefix = args[1].Trim();
                    RenameAllBlocks(new_prefix);
                    break;
                default:
                    Echo(string.Format("ARUGUMENT {0} NOT RECOGNISED", argument));
                    break;
            }

            foreach (IMyTextPanel panel in hangarSigns)
            {
                ApplyDisplay(hangarPrefix, panel);
            }

        }



        public void SetInteriorDoorsLock(bool locking)
        {
            // change the lock status of the airlocks, and the door LCDs
            if (locking)
            {
                foreach (IMyDoor door in airlocks)
                {
                    door.ApplyAction("Open_Off");
                    if (door.Status == DoorStatus.Closed)
                    {
                        door.ApplyAction(POWER_OFF);
                    }
                }

                foreach (IMyTextPanel panel in internalTextPanels)
                {
                    panel.WritePublicText("LOCKED");
                    panel.FontColor = new Color(255, 0, 0, 255);
                }
            }
            else
            {
                foreach (IMyDoor door in airlocks)
                {
                    //door.ApplyAction("Open_Off");
                    door.ApplyAction(POWER_ON);
                }
                foreach (IMyTextPanel panel in internalTextPanels)
                {
                    panel.WritePublicText("Access Granted  ");
                    panel.FontColor = new Color(200, 255, 200, 255);
                }
            }
        }

        public void SetOutsideScreen(string id)
        {
            foreach (IMyTextPanel panel in externalTextPanels)
            {
                panel.ClearImagesFromSelection();
                panel.AddImageToSelection(id);
                panel.ShowTextureOnScreen();


            }
        }

        public void OpenCloseHangarDoor(bool open)
        {
            foreach (IMyAirtightHangarDoor door in hangarDoors)
            {
                if (open)
                {
                    door.ApplyAction("Open_On");
                }
                else
                {
                    door.ApplyAction("Open_Off");
                }
            }
        }

        public void PowerWarningLights(Color lightColor)
        {
            try
            {
                foreach (IMyLightingBlock light in warningLights)
                {
                    light.Color = lightColor;
                }
            }
            catch
            {
                Echo("Warning Lights FAILED");
            }
        }

        public void PowerExternalLights(bool power)
        {
            foreach (IMyLightingBlock light in doorLights)
            {
                if (power)
                {
                    light.ApplyAction(POWER_ON);
                }
                else
                {
                    light.ApplyAction(POWER_OFF);
                }
            }
        }

        public void SetVentPressure(bool pressurize)
        {
            foreach (IMyAirVent vent in vents)
            {
                vent.Depressurize = !pressurize;
            }
        }

        public bool CheckSensor()
        {
            if (sensor == null)
            {
                sensor = GetDockBlock<IMySensorBlock>(DOCK_SENSOR_NAME);
            }
            return sensor.IsActive;
        }

        public float GetVentPressure()
        {
            try
            {
                IMyAirVent vent = vents[0] as IMyAirVent;
                return vent.GetOxygenLevel();
            }
            catch
            {
                Echo("VENT PRESSURE FAILED");
                return -1;
            }
        }

        public void PowerInteriorLights(bool power)
        {
            foreach (IMyLightingBlock light in interiorLights)
            {
                try
                {
                    if (power)
                    {
                        light.ApplyAction(POWER_ON);
                    }
                    else
                    {
                        light.ApplyAction(POWER_OFF);
                    }
                }
                catch (Exception)
                {
                    Echo("INTERIOR LIGHT FAILED");
                }
            }
        }

        public bool IsDockOccupied()
        {
            if (connector.Status == MyShipConnectorStatus.Connected)
            {
                return true;
            }
            else return (exitSensor.IsActive || sensor.IsActive);
            
        }


        //GET BLOCK FUNCTIONS BELOW

        public void GetGroups()
        {
            vents = new List<IMyTerminalBlock>();

            airlocks = new List<IMyTerminalBlock>();
            hangarDoors = new List<IMyTerminalBlock>();

            doorLights = new List<IMyTerminalBlock>();
            interiorLights = new List<IMyTerminalBlock>();
            warningLights = new List<IMyTerminalBlock>();

            internalTextPanels = new List<IMyTerminalBlock>();
            externalTextPanels = new List<IMyTerminalBlock>();
            hangarSigns = new List<IMyTerminalBlock>();

            GetDockBlockGroup(GROUP_AIRLOCK_NAME, airlocks);
            GetDockBlockGroup(GROUP_DOOR_NAME, hangarDoors);
            GetDockBlockGroup(GROUP_EXT_LCDS_NAME, externalTextPanels);
            GetDockBlockGroup(GROUP_EXT_LIGHTS_NAME, doorLights);
            GetDockBlockGroup(GROUP_INT_LCDS_NAME, internalTextPanels);
            GetDockBlockGroup(GROUP_INT_LIGHTS_NAME, interiorLights);
            GetDockBlockGroup(GROUP_VENT_NAME, vents);
            GetDockBlockGroup(GROUP_WARN_LIGHTS_NAME, warningLights);
            GetDockBlockGroup(GROUP_INTERNAL_DISPLAY_NAME, hangarSigns);

            allGroups = new List<List<IMyTerminalBlock>>()
            {
                vents,
                airlocks,
                hangarDoors,
                doorLights,
                interiorLights,
                warningLights,
                internalTextPanels,
                externalTextPanels,
                hangarSigns,
                otherBlocks
            };
        }

        private T GetDockBlock<T>(string blockName) where T : class, IMyTerminalBlock
        {
            return GridTerminalSystem.GetBlockWithName(string.Format("{0} - {1}", hangarPrefix, blockName)) as T;
        }

        private void GetDockBlockGroup(string groupName, List<IMyTerminalBlock> destList)
        {
            try
            {
                string block_name = string.Format("{0} - {1}", hangarPrefix, groupName);
                //Echo(block_name);
                GridTerminalSystem.SearchBlocksOfName(block_name, destList as List<IMyTerminalBlock>);
                if (destList.Count > 0)
                {
                    Echo(string.Format("GOT {0}: TRUE", groupName));
                }
                else
                {
                    Echo(string.Format("GOT {0}: FALSE", groupName));
                }
            }
            catch (Exception)
            {
                Echo(string.Format("GOT {0}: FALSE", groupName));
            }
        }

        private void ApplyDisplay(string hangarName, IMyTextPanel display)
        {
            hangarNum = hangarName.Replace("Hangar ", "").Trim();
            int number = (int)Char.GetNumericValue(hangarNum[0]) - 1;
            int letter;
            if (hangarNum[1].Equals('A'))
            {
                letter = 0;
            }
            else
            {
                letter = 1;
            }

            String displayText = CreateDisplay(number, letter);
            display.WritePublicText(displayText);
            display.FontSize = 1.043f;

        }

        public string CreateDisplay(int number_pos, int letter_pos)
        {
            string[] letter = display_letters[letter_pos].Split('\n');
            string[] number = display_numbers[number_pos].Split('\n');

            string display_docknum = "";
            display_docknum += display_border[0];
            display_docknum += display_border[1];

            for (int i = 0; i < letter.Length; i++)
            {
                display_docknum += "." + number[i] + "." + letter[i] + "\n";
            }
            display_docknum += display_border[1];
            display_docknum += display_border[0];

            display_docknum = display_docknum.Replace('.', black_square);
            display_docknum = display_docknum.Replace('#', yellow_square);

            string ship_info_string = "Ship ID: {0}\nShip Name: {1}";
            string[] ship_info = getShipNameAndID();

            display_docknum += "\n" + string.Format(ship_info_string, ship_info[0], ship_info[1]);

            return display_docknum;
        }

        public string[] getShipNameAndID()
        {
            string shipIDHex;
            string shipName;
            if(CheckSensor())
            {
                shipIDHex = sensor.LastDetectedEntity.EntityId.ToString("X");
                shipIDHex = shipIDHex.Substring(shipIDHex.Length - 8);
                shipName = sensor.LastDetectedEntity.Name;
            }
            else if(connector.Status == MyShipConnectorStatus.Connected)
            {
                shipIDHex = connector.OtherConnector.EntityId.ToString("X");
                shipIDHex = shipIDHex.Substring(shipIDHex.Length - 8);
                shipName = connector.OtherConnector.CubeGrid.CustomName;
            }
            else
            {
                shipIDHex = "N/A";
                shipName = "N/A";
            }

            if(shipIDHex.Length + "Ship Id: ".Length < DISPLAY_WIDTH)
            {
                shipIDHex = shipIDHex.PadLeft(DISPLAY_WIDTH - "Ship ID: ".Length);
            }
            else
            {
                shipIDHex = string.Format("\n{0}", shipIDHex);
            }

            if(shipName.Length + "Ship Name: ".Length < DISPLAY_WIDTH)
            {
                shipName = shipName.PadLeft(DISPLAY_WIDTH - "Ship Name: ".Length);
            }
            else
            {
                shipName = string.Format("\n{0}", shipName);
            }
            string[] shipInfo = new string[2];
            shipInfo[0] = shipIDHex;
            shipInfo[1] = shipName;
            return shipInfo;
        }

        public void RenameAllBlocks(string new_prefix)
        {
            Echo("RENAMING HANGAR TO " + new_prefix);
            foreach (List<IMyTerminalBlock> list in allGroups)
            {
                foreach (IMyTerminalBlock block in list)
                {
                    string currentName = block.CustomName;
                    string newName = currentName.Replace(hangarPrefix, new_prefix);
                    Echo(newName);
                    block.CustomName = newName;
                }
            }
            foreach (IMyTextPanel panel in hangarSigns)
            {
                ApplyDisplay(new_prefix, panel);
            }

            hangarPrefix = new_prefix;
            Echo("HANGAR RENAME COMPLETE");
        }

        String[] display_border = new string[]
        {
          ".#.#.#.#.#.#.#.#.\n",
          "#.......#.......#\n"
        };

        String[] display_letters = new string[]
        {
          "..####.\n"
        + ".#...#.\n"
        + ".#...#.\n"
        + ".#####.\n"
        + ".#...#.\n"
        + ".#...#.\n"
        + ".#...#.",

          ".####..\n"
        + ".#...#.\n"
        + ".#...#.\n"
        + ".####..\n"
        + ".#...#.\n"
        + ".#...#.\n"
        + "..####.."
        };

        String[] display_numbers = new string[]
        {
          "...#...\n"
        + "..##...\n"
        + "...#...\n"
        + "...#...\n"
        + "...#...\n"
        + "...#...\n"
        + "..###..",

          "..###..\n"
        + ".#...#.\n"
        + ".....#.\n"
        + "...##..\n"
        + "..#....\n"
        + ".#.....\n"
        + ".#####.",

          "..###..\n"
        + ".#...#.\n"
        + ".....#.\n"
        + "...##..\n"
        + ".....#.\n"
        + ".#...#.\n"
        + "..###..",

          "....##.\n"
        + "...#.#.\n"
        + "..#..#.\n"
        + ".#...#.\n"
        + ".#####.\n"
        + ".....#.\n"
        + ".....#.",

          ".#####.\n"
        + ".#.....\n"
        + ".####..\n"
        + ".....#.\n"
        + ".....#.\n"
        + ".#...#.\n"
        + "..###..",

          "..###..\n"
        + ".#...#.\n"
        + ".#.....\n"
        + ".####..\n"
        + ".#...#.\n"
        + ".#...#.\n"
        + "..###..",

          ".#####.\n"
        + ".....#.\n"
        + "....#..\n"
        + "...#...\n"
        + "..#....\n"
        + "..#....\n"
        + "..#....",

          "..###..\n"
        + ".#...#.\n"
        + ".#...#.\n"
        + "..###..\n"
        + ".#...#.\n"
        + ".#...#.\n"
        + "..###.."

        };

        char yellow_square = '\ue2f0';
        char black_square = '\ue100';
    }
}