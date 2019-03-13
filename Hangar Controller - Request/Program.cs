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

        const string dockAccept = "DOCKING REQUEST GRANTED.\nPROCEED TO DOCK {0}";
        const string dockReject = "DOCKING REQUEST DENIED.\nNO HANGARS AVAILABLE";
        const string dockEscape = "LEAVING DOCK. PLEASE\nWAIT UNTIL DOORS FULLY OPEN";
        const string dockRemain = "ERROR: UNABLE TO UNDOCK\nCHECK SHIP ID";

        public Program()
        {
            textPanel = GridTerminalSystem.GetBlockWithName("Docking Display") as IMyTextPanel;
            //antenna = GridTerminalSystem.GetBlockWithName("Docking Antenna") as IMyRadioAntenna;

            List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(antennas);
            antenna = antennas[0];
            Echo(String.Format("ANTENNA FOUND: {0}", antenna.CustomName));
            antenna.EnableBroadcasting = true;

            Echo("CURRENT STATUS: " + Storage);

        }

        public void Main(string argument, UpdateType updateSource)
        {

            if ((updateSource & UpdateType.Antenna) != 0)
            {
                Echo("RECEIVED FOLLOWING TRANSMISSION: " + argument);
                //script ran by anntenna receiving broadcast, with matching ID ensuring the broadcast is for this ship
                string shipID;
                bool isAccepted = true;
                string[] args;
                //argument should be composed of two parts,
                try
                {
                    args = argument.Split(',');
                    shipID = args[0].Trim(); //the ID
                    if (args[0].Equals("null"))
                    {
                        isAccepted = false;
                    }
                }
                catch (Exception e)
                {
                    Echo("MALFORMED ARGUMENT. EXPECTED THREE STRINGS SEPARATED BY COMMA, INSTEAD GOT '" + argument + "'");
                    return;
                }
                if (shipID == Me.CubeGrid.EntityId.ToString())
                {
                    SetPanel(args[2], isAccepted, args[1]);
                }
                return;
            }

            Echo("REQUESTING " + argument);
            Echo(string.Format("BROADCASTING REQUEST: {0} , {1}", Me.CubeGrid.EntityId.ToString(), argument));
            antenna.TransmitMessage(string.Format("{0} , {1}", Me.CubeGrid.EntityId.ToString(), argument), MyTransmitTarget.Everyone);
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
            catch (Exception e)
            {
                Echo("UNABLE TO DISPLAY TEXT. DOES LCD EXIST?");
            }
        }
    }
}