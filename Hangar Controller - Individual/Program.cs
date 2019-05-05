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
using VRage.Game.GUI.TextPanel;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region mdk macros
        // This script was deployed using the MDK api at $MDK_DATETIME$
        #endregion

        /// <summary>
        /// Names of blocks, minus the dock specific prefix. My setup uses "Hangar 1A - Sensor" as the name of the sensor of the first dock, for instance.
        /// The Prefix (Hangar XX in this case) is gained from the name of the programming block this code runs on.
        /// </summary>
        private Dictionary<string, string> block_names = new Dictionary<string, string>
        {
            {"landing_sensor", "Sensor" },
            {"exit_sensor", "Exit Sensor"},
            {"group_connectors", "Connector" },
            {"group_vents", "Air Vent"},
            {"group_airlocks", "Airlock"},
            {"group_big_door", "Airtight Hangar Door"},
            {"group_ext_lights", "Door Light"},
            {"group_int_lights", "Interior Light"},
            {"group_warn_lights", "Warning Light"},
            {"group_int_lcds", "Internal LCD"},
            {"group_ext_lcds", "External LCD"},
            {"group_display_lcds", "LCD Display"}
        };

        HangarSystem hangar;
        string hangar_name;

        public Program()
        {
            hangar_name = Me.CustomName.Split('-')[0].Trim(); //get the bit of name before the dash.
            Me.GetSurface(1).ContentType = ContentType.TEXT_AND_IMAGE;
            Me.GetSurface(1).WriteText(hangar_name);
            Me.GetSurface(1).FontSize = 8.0f;
            Me.GetSurface(1).Alignment = TextAlignment.CENTER;
            IMyTextSurface computerScreen = Me.GetSurface(0);
            computerScreen.ContentType = ContentType.TEXT_AND_IMAGE;
            computerScreen.FontSize = 1.7f;
            computerScreen.TextPadding = 0f;
            
            hangar = new HangarSystem(hangar_name, block_names, this, computerScreen);
            Echo("COMPLETED SETUP");
        }

        public void Main(string argument, UpdateType updateSource)
        {

            string[] args = argument.Split(',');
            switch (args[0].Trim())
            {
                case "DOCK":
                    Echo("INITIATING DOCKING PROCEDURE");

                    hangar.StartDockingProcedure();
                    break;

                case "UNDOCK":
                    Echo("INITIATING UNDOCKING PROCEDURE");
                    hangar.StartUndockingProcedure();
                    break;

                case "SENSOR CHECK":
                    Echo("SENSOR DETECTS SHIP");
                    hangar.ShipHasLanded();
                    break;

                case "VENT CHECK":
                    Echo("HANGAR PRESSURIZED");
                    hangar.HangarPressurized();
                    break;

                case "DEPARTURE CHECK":
                    Echo("SHIP LEFT");
                    hangar.ShipHasLeft();
                    break;

                case "RENAME":
                    Echo("RENAMING HANGAR");
                    string new_prefix = args[1].Trim();
                    hangar.RenameHangar(new_prefix);

                    string current_name = Me.CustomName;
                    string new_name = current_name.Replace(hangar_name, new_prefix);
                    Me.CustomName = new_name;
                    Me.GetSurface(1).WriteText(new_prefix);
                    break;

                default:
                    Echo(string.Format("ARUGUMENT {0} NOT RECOGNISED", argument));
                    break;
            }
        }

        /// <summary>
        /// Class to represent a full hangar system. Provides methods to access all required blocks and to
        /// initiate docking/undocking procedures
        /// </summary>
        class HangarSystem
        {
            Dictionary<string, IMyTerminalBlock> blocks;
            Dictionary<string, List<IMyTerminalBlock>> grouped_blocks;
            string hangar_prefix;
            IMyTextSurface screen;
            enum HangarStatus
            {
                free,
                docking,
                pressurizing,
                docked,
                undocking
            }

            class DisplayTextures
            {
                public static string arrowTexture = "Arrow";
                public static string warningTexture = "Danger";
                public static string stopTexture = "No Entry";
            }

            HangarStatus status;
            MyGridProgram source;
            
            public HangarSystem(string hangar_prefix, Dictionary<string, string> block_names, MyGridProgram source, IMyTextSurface screen)
            {
                this.hangar_prefix = hangar_prefix;
                this.source = source;
                this.screen = screen;
                blocks = new Dictionary<string, IMyTerminalBlock>();
                grouped_blocks = new Dictionary<string, List<IMyTerminalBlock>>();
                GetBlocks(block_names);
                string saved_status = "";
                try
                {
                   saved_status = source.Me.CustomData.Split(',')[0];
                }
                catch(Exception e)
                {
                    source.Echo(string.Format("ERROR GETTING SAVED INFO: {0}", e.Message));
                }
                if(saved_status != "")
                {
                    source.Echo(string.Format("Saved Info:\n{0}", saved_status));
                    HangarStatus tmp_status;
                    Enum.TryParse(saved_status, out tmp_status);
                    SetHangarStatus(tmp_status);
                }
                else if(IsDocked())
                {
                    SetHangarStatus(HangarStatus.docked);
                }
                else
                {
                    SetHangarStatus(HangarStatus.free);
                }

                IMySensorBlock landing_sensor = (IMySensorBlock)blocks["landing_sensor"];
                IMySensorBlock exit_sensor = (IMySensorBlock)blocks["exit_sensor"];

                landing_sensor.DetectSmallShips = true;
                landing_sensor.DetectSubgrids = true;
                landing_sensor.DetectEnemy = false;

                exit_sensor.DetectSmallShips = true;
                exit_sensor.DetectSubgrids = true;
                exit_sensor.DetectEnemy = false;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public bool IsDocked()
            {
                IMySensorBlock landing_sensor = (IMySensorBlock)blocks["landing_sensor"];
                return landing_sensor.IsActive;
            }

            /// <summary>
            /// Open the hangar door and prepare the hangar for a ship to dock.
            /// </summary>
            public void StartDockingProcedure()
            {
                if (status == HangarStatus.free)
                {
                    SetHangarStatus(HangarStatus.docking);
                    //change graphic of external LCDs to arrow
                    SetExternalScreens(DisplayTextures.arrowTexture);

                    //open door
                    OpenCloseHangarDoor(true);
                    source.Echo("OPENING BIG DOOR");
                    PowerLights(false, "group_int_lights");
                    PowerLights(true, "group_ext_lights");
                    PowerLights(true, "group_warn_lights");
                    source.Echo("LIGHTS SET");

                    //WAIT for sensor to detect ship landing
                    source.Echo("WAITING FOR SENSOR TO DETECT SHIP");
                }
            }

            /// <summary>
            /// Close the Doors and begins to pressurize the hangar when a ship has landed.
            /// Called by the landing sensor detecting a ship
            /// </summary>
            public void ShipHasLanded()
            {
                if(status == HangarStatus.docking)
                {
                    SetHangarStatus(HangarStatus.pressurizing);
                    SetExternalScreens(DisplayTextures.stopTexture);
                    OpenCloseHangarDoor(false);
                    SetPressurize(true);
                    PowerLights(false, "group_ext_lights");
                    OpenCloseHangarDoor(false);
                }                
            }

            /// <summary>
            /// Close the doors after a ship has left the hangar
            /// Called by the exit sensor stopping detection of a ship
            /// </summary>
            public void ShipHasLeft()
            {
                if (status == HangarStatus.undocking)
                {
                    SetHangarStatus(HangarStatus.free);
                    SetExternalScreens(DisplayTextures.stopTexture);
                    OpenCloseHangarDoor(false);
                    PowerLights(false, "group_warn_lights");
                    PowerLights(false, "group_ext_lights");
                }
            }

            /// <summary>
            /// Hangar has fully pressurized
            /// Called by the vents getting to full pressure
            /// </summary>
            public void HangarPressurized()
            {
                if (status == HangarStatus.pressurizing)
                {
                    SetHangarStatus(HangarStatus.docked);
                    PowerLights(false, "group_warn_lights");
                    PowerLights(true, "group_int_lights");
                    SetInteriorDoorsLock(false);
                }
            }

            /// <summary>
            /// Open the hangar door and prepare to let a ship leave.
            /// </summary>
            public void StartUndockingProcedure()
            {
                if(status == HangarStatus.docked)
                {
                    SetHangarStatus(HangarStatus.undocking);
                    SetExternalScreens(DisplayTextures.warningTexture);//change graphic of external LCDs to WARNING
                    SetInteriorDoorsLock(true);
                    PowerLights(false, "group_int_lights");
                    PowerLights(true, "group_warn_lights");
                    PowerLights(true, "group_ext_lights");
                    SetPressurize(false);
                    OpenCloseHangarDoor(true);
                }
                else
                {
                    source.Echo("Unable to start Undocking Procedure: Already in progress");
                }
            }

            private void PowerLights(bool power, string light_group_name)
            {
                foreach (IMyLightingBlock light in grouped_blocks[light_group_name])
                {
                    try
                    {
                        light.Enabled = power;
                    }
                    catch (Exception)
                    {
                        source.Echo("INTERIOR LIGHT FAILED");
                    }
                }
            }

            private void OpenCloseHangarDoor(bool open)
            {
                foreach(IMyAirtightHangarDoor door in grouped_blocks["group_big_door"])
                {
                    if(open)
                    {
                        door.OpenDoor();
                    }
                    else
                    {
                        door.CloseDoor();
                    }
                }
            }

            private void SetInteriorDoorsLock(bool locked)
            {
                foreach(IMyAirtightSlideDoor door in grouped_blocks["group_airlocks"])
                {
                    Color text_color;
                    string text;
                    if (locked)
                    {
                        door.CloseDoor();
                        door.Enabled = false;
                        text = "LOCKED";
                        text_color = new Color(255, 0, 0);
                    }
                    else
                    {
                        door.Enabled = true;
                        text = "ACCESS GRANTED";
                        text_color = new Color(0, 255, 0);
                    }
                    foreach (IMyTextPanel lcd in grouped_blocks["group_int_lcds"])
                    {
                        lcd.WriteText(text);
                        lcd.FontColor = text_color;
                    }
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="pressurize"></param>
            private void SetPressurize(bool pressurize)
            {
                foreach(IMyAirVent vent in grouped_blocks["group_vents"])
                {
                    vent.Depressurize = !pressurize;
                }
            }

            private void SetExternalScreens(string display)
            {
                foreach (IMyTextPanel panel in grouped_blocks["group_ext_lcds"])
                {
                    panel.ClearImagesFromSelection();
                    panel.AddImageToSelection(display);
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="block_names"></param>
            /// <returns></returns>
            private void GetBlocks(Dictionary<string, string> block_names)
            {
                foreach(KeyValuePair<string, string> block_name in block_names)
                {
                    if(block_name.Key.Contains("group"))
                    {
                        List<IMyTerminalBlock> tmp_blocks = new List<IMyTerminalBlock>();
                        GetBlockGroup(block_name.Value, tmp_blocks);
                        this.grouped_blocks.Add(block_name.Key, tmp_blocks);

                    }
                    else
                    {
                        IMySensorBlock tmp_block = GetBlock<IMySensorBlock>(block_name.Value);
                        blocks.Add(block_name.Key, tmp_block);
                    }
                }
            }

            public void RenameHangar(string new_name)
            {
                source.Echo("RENAMING HANGAR TO " + new_name);
                foreach (KeyValuePair<string, List<IMyTerminalBlock>> list in grouped_blocks)
                {
                    foreach (IMyTerminalBlock block in list.Value)
                    {
                        try
                        {
                            string currentName = block.CustomName;
                            string newName = currentName.Replace(hangar_prefix, new_name);
                            source.Echo(newName);
                            block.CustomName = newName;
                        }
                        catch
                        {
                            source.Echo(string.Format("MISSING BLOCK {0}", list.Key));
                        }
                    }
                }
                foreach (KeyValuePair<string, IMyTerminalBlock> block in blocks)
                {
                    try
                    {
                        string currentName = block.Value.CustomName;
                        string newName = currentName.Replace(hangar_prefix, new_name);
                        source.Echo(newName);
                        block.Value.CustomName = newName;
                    }
                    catch
                    {
                        source.Echo(string.Format("Missing Block {0}", block.Key));
                    }
                }

                hangar_prefix = new_name;
                source.Echo("HANGAR RENAME COMPLETE");
            }

            private T GetBlock<T>(string blockName) where T : class, IMyTerminalBlock
            {
                T block = source.GridTerminalSystem.GetBlockWithName(string.Format("{0} - {1}", hangar_prefix, blockName)) as T;
                source.Echo(string.Format("GOT {0}: {1}", blockName, block != null));
                return block;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="groupName"></param>
            /// <param name="destList"></param>
            private void GetBlockGroup(string groupName, List<IMyTerminalBlock> destList)
            {
                try
                {
                    string block_name = string.Format("{0} - {1}", hangar_prefix, groupName);
                    source.GridTerminalSystem.SearchBlocksOfName(block_name, destList as List<IMyTerminalBlock>);
                    if (destList.Count > 0)
                    {
                        source.Echo(string.Format("GOT {0}: TRUE", groupName));
                    }
                    else
                    {
                        source.Echo(string.Format("GOT {0}: FALSE", groupName));
                    }
                }
                catch (Exception)
                {
                    source.Echo(string.Format("GOT {0}: FALSE", groupName));
                }
            }

            private void SetHangarStatus(HangarStatus status)
            {
                source.Echo(string.Format("Setting Hangar Status to: {0}", status.ToString()));
                this.status = status;
                string data_string = status.ToString();
                string screenString = "Status:\n {0}\nShip Name:\n {1}\nShip ID:\n {2}";
                string ship_name = "";
                string ship_id = "";
                string data = source.Me.CustomData;
                if (status == HangarStatus.docked)
                {
                    string[] saved_info = source.Me.CustomData.Split(',');
                    if (saved_info.Count() != 3)
                    {
                        IMySensorBlock sensor = (IMySensorBlock)blocks["landing_sensor"];
                        ship_name = sensor.LastDetectedEntity.Name;
                        ship_id = sensor.LastDetectedEntity.EntityId.ToString("X");
                    }
                    else
                    {
                        ship_name = saved_info[1].Trim();
                        ship_id = saved_info[2].Trim();
                    }

                    ship_id = ship_id.Substring(Math.Max(0, ship_id.Length - 8));
                    data_string = string.Format("{0},\n{1},\n{2}", data_string, ship_name, ship_id);
                }
                source.Me.CustomData = data_string;

                screen.WriteText(string.Format(screenString, status.ToString(), ship_name, ship_id));
            } 
        }
    }
}