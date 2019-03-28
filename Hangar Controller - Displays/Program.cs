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
        // This script was deployed using the MDK api at $MDK_DATETIME$
        #endregion
        string PANEL_NAME = "LCD Display";

        private List<DisplaySystem> displaySystems;

        public Program()
        {
            List<IMyProgrammableBlock> computers = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(computers, computer => computer.CustomName.ToLower().Contains("hangar") && computer.CustomData != "");
            displaySystems = new List<DisplaySystem>();
            foreach(IMyProgrammableBlock computer in computers)
            {
                string hangar_name = computer.CustomName.Split('-')[0].Trim();
                List<IMyTextPanel> panels = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType(panels, panel => panel.CustomName.Contains(hangar_name) && panel.CustomName.Contains(PANEL_NAME));
                displaySystems.Add(new DisplaySystem(hangar_name, computer, panels));
            }
            Echo(string.Format("GOT {0} DOCK SYSTEMS", displaySystems.Count));
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            foreach(DisplaySystem display in displaySystems)
            {
                display.SetDisplays();
            }

        }

        class DisplaySystem
        {
            public string hangar_name;
            string display_string;
            List<IMyTextPanel> screens;
            IMyProgrammableBlock computer;

            public DisplaySystem(string hangar_name, IMyProgrammableBlock computer, List<IMyTextPanel> panels)
            {
                this.hangar_name = hangar_name;
                this.computer = computer;
                this.screens = panels;
                this.display_string = BuildScreenString();
            }

            public void SetDisplays()
            {
                string display = display_string;

                string[] ship_info = GetShipInfo();
                display = string.Format(display, ship_info[0], ship_info[1]);

                foreach(IMyTextPanel screen in screens)
                {
                    screen.WritePublicText(display);
                }

            }

            private string[] GetShipInfo()
            {
                if (computer.CustomData.ToLower().Contains("docked"))
                {
                    string[] data = computer.CustomData.Split(',');
                    return new string[] { data[1].Trim(), data[2].Trim() };
                }
                else
                {
                    return new string[] { "N/A", "N/A" };
                }
            }

            /// <summary>
            /// Creates the display string for the hangar, including a formattable section for the ship info
            /// if one is docked.
            /// </summary>
            /// <returns>the string that will be displayed</returns>
            private string BuildScreenString()
            {

                string hangarNum = hangar_name.Replace("Hangar ", "").Trim();
                int number_pos = (int)Char.GetNumericValue(hangarNum[0]) - 1;
                int letter_pos;
                if (hangarNum[1].Equals('A'))
                {
                    letter_pos = 0;
                }
                else
                {
                    letter_pos = 1;
                }

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

                display_docknum += "\n" + ship_info_string;

                return display_docknum;
            }

            string[] display_border = new string[]
            {
          ".#.#.#.#.#.#.#.#.\n",
          "#.......#.......#\n"
            };

            string[] display_letters = new string[]
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

            string[] display_numbers = new string[]
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
}

//private void ApplyDisplay(string hangarName, IMyTextPanel display)
//{
//    hangarNum = hangarName.Replace("Hangar ", "").Trim();
//    int number = (int)Char.GetNumericValue(hangarNum[0]) - 1;
//    int letter;
//    if (hangarNum[1].Equals('A'))
//    {
//        letter = 0;
//    }
//    else
//    {
//        letter = 1;
//    }

//    String displayText = CreateDisplay(number, letter);
//    Echo("APPLYING DISPLAY");
//    display.WritePublicText(displayText);
//    display.FontSize = 1.043f;

//}

//public string CreateDisplay(int number_pos, int letter_pos)
//{
//    Echo(string.Format("CREATING DISPLAY, LETTER {0}, NUMBER {1}", letter_pos, number_pos));
//    string[] letter = display_letters[letter_pos].Split('\n');
//    string[] number = display_numbers[number_pos].Split('\n');

//    string display_docknum = "";
//    display_docknum += display_border[0];
//    display_docknum += display_border[1];
//    Echo("TOP BORDER ADDED");
//    for (int i = 0; i < letter.Length; i++)
//    {
//        display_docknum += "." + number[i] + "." + letter[i] + "\n";
//    }
//    Echo("BOTTOM BORDER ADDED");
//    display_docknum += display_border[1];
//    display_docknum += display_border[0];

//    display_docknum = display_docknum.Replace('.', black_square);
//    display_docknum = display_docknum.Replace('#', yellow_square);

//    string ship_info_string = "Ship ID: {0}\nShip Name: {1}";
//    string[] ship_info = getShipNameAndID();

//    display_docknum += "\n" + string.Format(ship_info_string, ship_info[0], ship_info[1]);

//    return display_docknum;
//}

//public string[] getShipNameAndID()
//{
//    string shipIDHex;
//    string shipName;
//    if(CheckSensor())
//    {
//        shipIDHex = sensor.LastDetectedEntity.EntityId.ToString("X");
//        shipIDHex = shipIDHex.Substring(shipIDHex.Length - 8);
//        shipName = sensor.LastDetectedEntity.Name;
//    }
//    else
//    {
//        shipIDHex = "N/A";
//        shipName = "N/A";
//    }

//    if(shipIDHex.Length + "Ship Id: ".Length < DISPLAY_WIDTH)
//    {
//        shipIDHex = shipIDHex.PadLeft(DISPLAY_WIDTH - "Ship ID: ".Length);
//    }
//    else
//    {
//        shipIDHex = string.Format("\n{0}", shipIDHex);
//    }

//    if(shipName.Length + "Ship Name: ".Length < DISPLAY_WIDTH)
//    {
//        shipName = shipName.PadLeft(DISPLAY_WIDTH - "Ship Name: ".Length);
//    }
//    else
//    {
//        shipName = string.Format("\n{0}", shipName);
//    }
//    string[] shipInfo = new string[2];
//    shipInfo[0] = shipIDHex;
//    shipInfo[1] = shipName;
//    return shipInfo;
//}