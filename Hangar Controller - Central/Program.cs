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

        Queue<Hangar> dockControllers = new Queue<Hangar>();

        IMyRadioAntenna antenna;
        IMyTextPanel logger;

        string printString;

        const int MAX_LINES = 33;
        const int LOG_WIDTH = 48;

        const string LOGGER_HEADER = "                    DOCKING LOG                  \n" +
                                     "-TIME-----+-SHIP ID---EVENT----------------------";

        public Program()
        {
            //gets all programmable blocks, where the block's name starts with "Dock"
            int hangarCount = GetAllHangars();
            Echo(String.Format("Found {0} Hangar Controllers", hangarCount));

            antenna = GridTerminalSystem.GetBlockWithName("Dock Request Antenna") as IMyRadioAntenna;
            logger = GridTerminalSystem.GetBlockWithName("LCD Logger") as IMyTextPanel;
            Echo(string.Format("ANTENNA FOUND: {0}", antenna != null));
            Echo(string.Format("LOGGER FOUND: {0}", logger != null));
            if (logger != null)
            {
                string loggerText = logger.GetPublicText();
                if (!loggerText.Contains("DOCKING LOG"))
                {
                    Echo("LOGGER TEXT DOES NOT INCLUDE HEADER. Only Found:\n" + loggerText);
                    logger.WritePublicText(LOGGER_HEADER);
                }
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo(String.Format("UPDATE CALLED FROM {0}", updateSource.ToString()));
            long shipID;
            string request;
            //argument should be composed of two parts,
            try
            {
                var args = argument.Split(',');
                long.TryParse(args[0], out shipID); //the ID
                request = args[1].Trim(); //the request to dock/undock
            }
            catch (Exception)
            {
                Echo("MALFORMED ARGUMENT. EXPECTED TWO STRING SEPARATED BY COMMA, INSTEAD GOT '" + argument + "'");
                return;
            }
            Log(shipID, request);
            Hangar dock;
            string dockName;
            switch (request)
            {
                case "DOCK":
                    Echo(string.Format("DOCK REQUEST RECEIVED FROM SHIP: {0}", shipID.ToString("X")));
                    dock = GetAvailableDock();

                    if (dock != null)
                    {
                        dock.RunProgram("DOCK");
                        dockName = dock.name;
                    }
                    else
                    {
                        dockName = "null";
                    }
                    TransmitMessage(shipID, dockName, "DOCK");
                    break;
                case "UNDOCK":
                    Echo(string.Format("UNDOCK REQUEST RECEIVED FROM SHIP: {0}", shipID.ToString("X")));
                    dock = GetDockOfShip(shipID);
                    if (dock != null)
                    {
                        dock.RunProgram("UNDOCK");
                        dockName = dock.name;
                    }
                    else
                    {
                        dockName = "null";
                    }
                    TransmitMessage(shipID, dockName, "UNDOCK");
                    break;
                default:
                    return;
            }
        }

        public int GetAllHangars()
        {
            List<IMyProgrammableBlock> controllers = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType(controllers, (IMyProgrammableBlock x) => x.CustomName.EndsWith("Computer Control"));

            foreach (IMyProgrammableBlock computer in controllers)
            {
                dockControllers.Enqueue(new Hangar(Echo, GridTerminalSystem, computer));
            }

            return dockControllers.Count;
        }

        /// <summary>
        /// Finds the first empty hangar.
        /// </summary>
        /// <returns>The number for the hangar, or -1 if no hangars are empty</returns>
        public Hangar GetAvailableDock()
        {
            int count = dockControllers.Count;

            while (count > 0)
            {
                Hangar hangar = dockControllers.Dequeue();
                dockControllers.Enqueue(hangar);

                if (!hangar.IsOccupied())
                {
                    return hangar;
                }

                count--;
            }

            return null; //-1 means no hangars are free
        }

        /// <summary>
        /// Find the hangar containing the ship
        /// </summary>
        /// <param name="shipID">the ID number of the ship that we're looking for</param>
        /// <returns>The number for the hangar, or -1 if no hangars are empty</returns>
        public Hangar GetDockOfShip(long shipID)
        {
            //using a list so checking for the correct hangar does not change the order of the queue
            List<Hangar> hangar_list = dockControllers.ToList();
            foreach (Hangar hangar in hangar_list)
            {
                if (hangar.GetShipID() == shipID)
                {
                    return hangar;
                }
            }
            return null; //can't find the ship!
        }

        public void TransmitMessage(long shipID, string dockName, string action)
        {
            string message = string.Format("{0} , {1} , {2}", shipID, dockName, action);
            Log(shipID, dockName);
            Echo("TRANSMITTING MESSAGE: " + message);
            antenna.TransmitMessage(message, MyTransmitTarget.Everyone);


        }

        public void WriteToScreen(string line)
        {
            try
            {
                string currentScreen = logger.GetPublicText();

                var lines = currentScreen.Split('\n').ToList<string>();
                lines.Insert(2, line);
                if (lines.Count > MAX_LINES)
                {
                    lines.RemoveRange(MAX_LINES, lines.Count - MAX_LINES);
                }

                logger.WritePublicText("");
                foreach (string newLine in lines)
                {
                    logger.WritePublicText(newLine + "\n", true);
                }
            }
            catch (NullReferenceException e)
            {
                Echo("Logger Not Found");
            }
        }

        public void Log(long shipID, string request)
        {
            string shipIDHex = shipID.ToString("X");
            shipIDHex = shipIDHex.Substring(shipIDHex.Length - 8);
            //string printString;
            DateTime time = DateTime.Now;
            string timeString = string.Format("{0:00}{1:00}{2:00}{3:00}", time.Month, time.Day, time.Hour, time.Minute);
            switch (request) //this method is either called when its a request from a ship, or a response
            {
                case "DOCK":
                    printString = string.Format(" {0} | {1}: DOCK REQ: ", timeString, shipIDHex);
                    break;
                case "UNDOCK":
                    printString = string.Format(" {0} | {1}: RELEASE REQ: ", timeString, shipIDHex);
                    break;
                default:
                    Echo("TRANMISSION TO SHIP");

                    int padWidth = LOG_WIDTH - printString.Length;
                    if (!request.Equals("null"))
                    {


                        if (printString.Contains(shipIDHex) && printString.Contains("RELEASE"))
                        {
                            printString = string.Format("{0}{1}", printString, string.Format("{0} CLEAR", request).PadLeft(padWidth));
                            WriteToScreen(printString);
                            return;
                        }
                        if (printString.Contains(shipIDHex) && printString.Contains("DOCK"))
                        {
                            printString = string.Format("{0}{1}", printString, string.Format("ASSIGNED {0}", request).PadLeft(padWidth));
                            WriteToScreen(printString);
                            return;
                        }


                    }
                    else
                    {
                        //printString = string.Format(" {0} | {1}: REQUEST DENIED", timeString, shipID.ToString("X"));
                        printString = string.Format("{0}{1}", printString, "DENIED".PadLeft(padWidth));
                        WriteToScreen(printString);
                    }

                    break;
            }
        }

        public class Hangar
        {
            readonly Action<string> Echo;
            readonly IMyGridTerminalSystem GridTerminalSystem;

            readonly IMyProgrammableBlock controller;
            readonly IMySensorBlock sensor;
            public readonly string name;

            public Hangar(
                Action<string> echo,
                IMyGridTerminalSystem system,
                IMyProgrammableBlock controller)
            {
                Echo = echo;
                this.controller = controller;
                GridTerminalSystem = system;

                name = controller.CustomName.Split('-')[0].Trim();

                List<IMyTerminalBlock> sensors = new List<IMyTerminalBlock>();
                GridTerminalSystem.SearchBlocksOfName(name, sensors, sensor => sensor is IMySensorBlock && !sensor.CustomName.Contains("exit"));
                sensor = sensors[0] as IMySensorBlock;
            }

            public bool RunProgram(string argument)
            {
                if (controller != null)
                {
                    return controller.TryRun(argument);
                }
                else
                {
                    return false;
                }
            }

            public bool IsOccupied()
            {
                if (sensor != null)
                {
                    return sensor.IsActive;
                }
                else
                {
                    return false;
                }
            }

            public long GetShipID()
            {
                if (IsOccupied())
                {
                    return sensor.LastDetectedEntity.EntityId;
                }
                else
                {
                    return -1;
                }
            }

            public Vector3 getDockPosition()
            {
                //UNUSED SO FAR
                return controller.GetPosition();
            }
        }
    }
}