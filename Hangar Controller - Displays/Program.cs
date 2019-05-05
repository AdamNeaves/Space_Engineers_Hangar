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
        static float FONT_SIZE = 1.043f;
        static int DISPLAY_WIDTH = 25;

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
                screens = panels;
                display_string = BuildScreenString();
                foreach(IMyTextPanel panel in panels)
                {
                    panel.FontSize = FONT_SIZE;
                    panel.Font = "monospace";
                    panel.TextPadding = 0f;
                }
            }

            public void SetDisplays()
            {
                string display = display_string;

                Dictionary<string, string> ship_info = GetShipInfo();
                display = string.Format(display, ship_info["id"].PadLeft(DISPLAY_WIDTH), ship_info["name"].PadLeft(DISPLAY_WIDTH));

                foreach(IMyTextPanel screen in screens)
                {
                    
                    screen.WriteText(display);
                }

            }

            private Dictionary<string, string> GetShipInfo()
            {
                if (computer.CustomData.ToLower().Contains("docked"))
                {
                    string[] data = computer.CustomData.Split(',');
                    return new Dictionary<string, string> { { "name", data[2].Trim() }, { "id", data[1].Trim() } };
                }
                else
                {
                    return new Dictionary<string, string> { { "name", "N/A" }, { "id", "N/A" } };
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

                string ship_info_string = "Ship ID:\n{0}\nShip Name:\n{1}";

                display_docknum += "\n" + ship_info_string;

                return display_docknum;
            }

            readonly string[] display_border = new string[]
            {
          ".#.#.#.#.#.#.#.#.\n",
          "#.......#.......#\n"
            };

            readonly string[] display_letters = new string[]
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

            readonly string[] display_numbers = new string[]
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

            readonly char yellow_square = '\ue2f0';
            readonly char black_square = '\ue100';
        }
    }
}
