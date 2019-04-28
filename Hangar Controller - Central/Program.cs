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
        // This script was deployed at $MDK_DATETIME$
        #endregion

        Queue<Hangar> dockControllers = new Queue<Hangar>();

        IMyRadioAntenna antenna;
        IMyTextSurface logger;
        IMyBroadcastListener listener;
        string printString;

        const int MAX_LINES = 33;
        const int LOG_WIDTH = 48;
        const float FONT_SIZE = 0.52f;

        const string MESSAGE_TAG = "docking";
        const string LOGGER_HEADER = "                    DOCKING LOG                  \n" +
                                     "-TIME-----+-SHIP ID---EVENT-----------------------";

        const string MESSAGE_ACCEPT_REQUEST = "Docking Request - GRANTED\nProceed to {0}\n\nWelcome to Lovelace Station!\nWatch your speed!";
        const string MESSAGE_REJECT_REQUEST = "Docking Request - DENIED \nNO HANGARS AVAILABLE\nPlease wait before re-sending request";
        const string MESSAGE_ACCEPT_LEAVE =   "Release Request - GRANTED\nAllow doors to open fully before exit\n\nWatch your speed!";
        const string MESSAGE_REJECT_LEAVE =   "ERROR - Ship not currently docked.";



        public Program()
        {
            //gets all programmable blocks, where the block's name starts with "Dock"
            int hangarCount = GetAllHangars();
            Echo(String.Format("Found {0} Hangar Controllers", hangarCount));

            antenna = GridTerminalSystem.GetBlockWithName("Dock Request Antenna") as IMyRadioAntenna; //TODO: Remove specific name requrement
            if(antenna != null)
            {
                antenna.AttachedProgrammableBlock = Me.EntityId;
                listener = IGC.RegisterBroadcastListener(MESSAGE_TAG);
                listener.SetMessageCallback("REQUEST_WAITING");

            }
            logger = GridTerminalSystem.GetBlockWithName("LCD Logger") as IMyTextPanel;
            Echo(string.Format("ANTENNA FOUND: {0}", antenna != null));
            Echo(string.Format("LOGGER FOUND: {0}", logger != null));
            if (logger == null)
            {
                logger = Me.GetSurface(0);
            }
            logger.ContentType = ContentType.TEXT_AND_IMAGE;
            logger.FontSize = FONT_SIZE;
            string loggerText = logger.GetText();
            if (!loggerText.Contains("DOCKING LOG"))
            {
                Echo("LOGGER TEXT DOES NOT INCLUDE HEADER. Only Found:\n" + loggerText);
                logger.WriteText(LOGGER_HEADER);
            }
            
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo(String.Format("UPDATE CALLED FROM {0}", updateSource.ToString()));
            
            // Get message from listener
            MyIGCMessage message = listener.AcceptMessage();
            Dictionary<string, object> messageData = DecodeMessage((string)message.Data);
            long requestSource = message.Source;
            Echo(messageData["shipID"].ToString());
            long shipID = (long)messageData["shipID"];
            string request = messageData["request"].ToString().Trim();

            Echo(string.Format("ID: {0}, request: {1}", shipID.ToString(), request));

            Log(shipID, request);
            Hangar dock;
            bool accepted = false;
            switch (request)
            {

                case "DOCK":
                    Echo(string.Format("DOCK REQUEST RECEIVED FROM SHIP: {0}", shipID.ToString("X")));
                    dock = GetAvailableDock();
                    
                    if (dock != null)
                    {
                        dock.RunProgram("DOCK");
                        accepted = true;
                    }
                    TransmitMessage(requestSource, shipID, dock, "DOCK", accepted);
                    break;
                case "UNDOCK":
                    Echo(string.Format("UNDOCK REQUEST RECEIVED FROM SHIP: {0}", shipID.ToString("X")));
                    dock = GetDockOfShip(shipID);

                    if (dock != null)
                    {
                        dock.RunProgram("UNDOCK");
                        accepted = true;
                    }
                    TransmitMessage(requestSource, shipID, dock, "UNDOCK", accepted);
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

        public void TransmitMessage(long requestSource, long shipID, Hangar dock, string action, bool accepted)
        {
            Dictionary<string, object> messageDict = new Dictionary<string, object>
            {
                //["location"] = dock.GetDockPosition(),
                ["action"] = action,
                ["accepted"] = accepted
            };
            string message_text;
            if(action.ToLower() == "dock")
            {
                if(accepted)
                {
                    message_text = string.Format(MESSAGE_ACCEPT_REQUEST, dock.name);
                }
                else
                {
                    message_text = MESSAGE_REJECT_REQUEST;
                }
            }
            else
            {
                if(accepted)
                {
                    message_text = MESSAGE_ACCEPT_LEAVE;
                }
                else
                {
                    message_text = MESSAGE_REJECT_LEAVE;
                }
            }
            messageDict["message"] = message_text;
            string message = EncodeMessage(messageDict);
            string dock_string = (dock != null) ? dock.name : "null";
            Log(shipID, dock_string);
            Echo("TRANSMITTING MESSAGE: " + message);

            IGC.SendUnicastMessage(requestSource, "docking", message);
            

            //antenna.TransmitMessage(message, MyTransmitTarget.Everyone);


        }

        public void WriteToScreen(string line)
        {
            try
            {
                string currentScreen = logger.GetText();

                var lines = currentScreen.Split('\n').ToList<string>();
                lines.Insert(2, line);
                if (lines.Count > MAX_LINES)
                {
                    lines.RemoveRange(MAX_LINES, lines.Count - MAX_LINES);
                }

                logger.WriteText("");
                foreach (string newLine in lines)
                {
                    logger.WriteText(newLine + "\n", true);
                }
            }
            catch (NullReferenceException)
            {
                Echo("Logger Not Found");
            }
        }

        public void Log(long shipID, string request)
        {
            try
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
            catch
            {
                return;
            }
        }

        private string EncodeMessage(Dictionary<string, object> dict)
        {
            Echo("ENCODING MESSAGE");
            string message = "";
            foreach (KeyValuePair<string, object> item in dict)
            {
                string type = item.Value.GetType().Name.ToLower();
                string item_value = type == "string" ? item.Value.ToString().Replace('\n', '\t') : item.Value.ToString();
                string itemString = string.Format("{0}: {1}, {2}\n", item.Key, item_value, type);
                //Echo(string.Format("Message Part: {0}", itemString));
                message += itemString;
            }

            return message;
        }

        private Dictionary<string, object> DecodeMessage(string message)
        {
            Echo(string.Format("Decoding Message:\n {0}", message));
            Dictionary<string, object> dict = new Dictionary<string, object>();
            string[] message_parts = message.Split('\n');
            char[] delim = ":".ToCharArray();
            foreach (string part in message_parts)
            {
                if (part == "")
                {
                    continue;
                }
                Echo(string.Format("MESSAGE PART: {0}", part));
                string[] values = part.Split(delim);
                string key = values[0];
                values = values[1].Split(',');
                string type = values[1].Trim().ToLower();
                object value;
                switch (type)
                {
                    case "int":
                        value = Int32.Parse(values[0]);
                        break;
                    case "float":
                        value = float.Parse(values[0]);
                        break;
                    case "long":
                    case "int64":
                        value = long.Parse(values[0]);
                        break;
                    case "boolean":
                        value = bool.Parse(values[0]);
                        break;
                    case "vector3d":
                        string[] vectors = values[0].Split(',');
                        value = new Vector3(float.Parse(vectors[0]), float.Parse(vectors[1]), float.Parse(vectors[2]));
                        break;
                    case "string":
                        value = values[0].ToString().Replace('\t', '\n');
                        break;
                    default:
                        value = values[0];
                        break;
                }

                dict.Add(key, value);
            }
            Echo("MESSAGE DECODED");
            return dict;
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
                string saved_status = controller.CustomData.ToLower().Split(',')[0];
                return saved_status.Trim() != "free";

                
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

            public Vector3D GetDockPosition()
            {
                //UNUSED SO FAR
                return controller.GetPosition();
            }
        }
    }
}