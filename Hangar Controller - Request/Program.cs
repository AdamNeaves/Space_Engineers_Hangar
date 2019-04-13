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

        IMyTextPanel textPanel;
        IMyRadioAntenna antenna;
        IMyBroadcastListener listener;

        const string dockAccept = "DOCKING REQUEST GRANTED.\nPROCEED TO DOCK {0}";
        const string dockReject = "DOCKING REQUEST DENIED.\nNO HANGARS AVAILABLE";
        const string dockEscape = "LEAVING DOCK. PLEASE\nWAIT UNTIL DOORS FULLY OPEN";
        const string dockRemain = "ERROR: UNABLE TO UNDOCK\nCHECK SHIP ID";

        public Program()
        {
            textPanel = GridTerminalSystem.GetBlockWithName("Docking Display") as IMyTextPanel;
            //antenna = GridTerminalSystem.GetBlockWithName("Docking Antenna") as IMyRadioAntenna;

            List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(antennas);
            antenna = antennas[0];
            Echo(String.Format("ANTENNA FOUND: {0}", antenna.CustomName));
            antenna.EnableBroadcasting = true;
            antenna.AttachedProgrammableBlock = Me.EntityId;
            listener = IGC.RegisterBroadcastListener("docking");
            listener.SetMessageCallback("DOCK_MESSAGE");

            Echo("CURRENT STATUS: " + Storage);

        }

        public void Main(string argument, UpdateType updateSource)
        {

            if (updateSource == UpdateType.IGC)
            {
                Echo("RECEIVED FOLLOWING TRANSMISSION: " + argument);
                //script ran by anntenna receiving broadcast, with matching ID ensuring the broadcast is for this ship

                bool isAccepted = true;
               
                MyIGCMessage message = listener.AcceptMessage();
                Dictionary<string, object> messageData = DecodeMessage((string)message.Data);
                isAccepted = !messageData["request"].Equals("null");
                //SetPanel(args[2], isAccepted, args[1]);
                return;
            }

            Echo("REQUESTING " + argument);
            SendMessage(argument);
            //antenna.TransmitMessage(string.Format("{0} , {1}", Me.CubeGrid.EntityId.ToString(), argument), MyTransmitTarget.Everyone);
        }

        public void SetPanel(string docking, bool isAccepted, string dockName)
        {
            try
            {
                if (docking.Trim().Equals("DOCK"))
                {
                    if (isAccepted)
                    {
                        textPanel.WritePublicText(string.Format(dockAccept, dockName));
                    }
                    else
                    {
                        textPanel.WritePublicText(dockReject);
                    }
                }
                else
                {
                    if (isAccepted)
                    {
                        textPanel.WritePublicText(dockEscape);
                    }
                    else
                    {
                        textPanel.WritePublicText(dockRemain);
                    }
                }
            }
            catch (Exception)
            {
                Echo("UNABLE TO DISPLAY TEXT. DOES LCD EXIST?");
            }
        }

        public void SendMessage(string request)
        {
            //string message = string.Concat(Me.CubeGrid.EntityId.ToString(), ",", request);

            Dictionary<string, object> message = new Dictionary<string, object>
            {
                ["shipID"] = Me.CubeGrid.EntityId,
                ["request"] = request
            };

            //Dictionary<string, string> message = new Dictionary<string, string>
            //{
            //    {"ship_id", Me.CubeGrid.EntityId.ToString()},
            //    {"request", request}
            //};
            Echo(string.Format("Sending message:\n{0}", EncodeMessage(message)));
            IGC.SendBroadcastMessage("docking", EncodeMessage(message));
        }

        private string EncodeMessage(Dictionary<string, object> dict)
        {
            Echo("ENCODING MESSAGE");
            string message = "";
            foreach (KeyValuePair<string, object> item in dict)
            {
                string type = item.Value.GetType().Name;
                string itemString = string.Format("{0}: {1}, {2}\n", item.Key, item.Value.ToString(), type);
                Echo(string.Format("Message Part: {0}", itemString));
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
                    break;
                }
                //Echo(string.Format("Message Part:\n {0}", part));
                string[] values = part.Split(delim);
                string key = values[0];
                values = values[1].Split(',');
                string type = values[1];
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
                    case "Int64":
                        value = long.Parse(values[0]);
                        break;
                    case "Vector3D":
                        string[] vectors = values[0].Split(',');
                        value = new Vector3(float.Parse(vectors[0]), float.Parse(vectors[1]), float.Parse(vectors[2]));
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
    }
}