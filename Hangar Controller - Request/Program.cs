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

        IMyTextSurface textPanel;
        IMyRadioAntenna antenna;
        IMyUnicastListener listener;

        public Program()
        {
            textPanel = GridTerminalSystem.GetBlockWithName("Docking Display") as IMyTextPanel;

            List<IMyCockpit> shipControllers = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(shipControllers);
            foreach(IMyCockpit cockpit in shipControllers)
            {
                //if cockpit is currently controlled by a player
                if(cockpit.IsMainCockpit)
                {
                    for(int i = 0; i < cockpit.SurfaceCount; i++)
                    {
                        IMyTextSurface surface = cockpit.GetSurface(i);
                        if(surface.ContentType == ContentType.TEXT_AND_IMAGE)
                        {
                            textPanel = surface;
                            Echo("FOUND TEXT SUFRACE IN COCKPIT");
                            textPanel.WriteText("DOCK REQUEST CONTROLLER LOADED");
                            break;
                        }
                    }
                    break;
                }
            }

            List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType(antennas);
            antenna = antennas[0];
            Echo(String.Format("ANTENNA FOUND: {0}", antenna.CustomName));
            antenna.EnableBroadcasting = true;
            antenna.AttachedProgrammableBlock = Me.EntityId;
            listener = IGC.UnicastListener;
            listener.SetMessageCallback("DOCK_MESSAGE");

            Echo("CURRENT STATUS: " + Storage);

        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo(string.Format("UPDATE CALLED FROM: {0}", updateSource.ToString()));
            if (argument == "DOCK_MESSAGE")
            {
                //script ran by anntenna receiving broadcast, with matching ID ensuring the broadcast is for this ship

                bool isAccepted = true;

                MyIGCMessage message = listener.AcceptMessage();
                Echo("RECEIVED FOLLOWING TRANSMISSION: " + message.Data.ToString());
                Dictionary<string, object> messageData = DecodeMessage((string)message.Data);
                Echo(string.Format("Is Accepted: {0}", messageData["accepted"].ToString()));
                isAccepted = (bool)messageData["accepted"];
                SetPanel(messageData["action"].ToString(), isAccepted, messageData["message"].ToString());

                return;
            }

            Echo("REQUESTING " + argument);
            SendMessage(argument);
        }

        public void SetPanel(string action, bool isAccepted, string message_text)
        {
            try
            {
                textPanel.WriteText(message_text);    
            }
            catch (Exception)
            {
                Echo("UNABLE TO DISPLAY TEXT. DOES LCD EXIST?");
            }
        }

        public void SendMessage(string request)
        {

            Dictionary<string, object> message = new Dictionary<string, object>
            {
                ["shipID"] = Me.CubeGrid.EntityId,
                ["request"] = request
            };

            Echo(string.Format("Sending message:\n{0}", EncodeMessage(message)));
            IGC.SendBroadcastMessage("docking", EncodeMessage(message));
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
    }
}