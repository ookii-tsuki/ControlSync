using System;
using System.Collections.Generic;
using System.Text;

namespace ControlSync.Client
{
    public class ClientSend
    {
        private static void SendTCPData(Packet _packet)
        {
            _packet.WriteLength();
            Client.tcp.SendData(_packet);
        }

        private static void SendUDPData(Packet _packet)
        {
            _packet.WriteLength();
            Client.udp.SendData(_packet);
        }

        #region Packets
        public static void WelcomeReceived()
        {
            using (Packet _packet = new Packet((int)ClientPackets.WelcomeReceived))
            {
                _packet.Write(Client.myId);
                ClientPg.instance.Dispatcher.Invoke(() => _packet.Write(ClientPg.instance.uidTB.Text));

                SendTCPData(_packet);
            }
        }

        public static void ButtonState(int xButtons)
        {
            using (Packet _packet = new Packet((int)ClientPackets.ButtonState))
            {
                _packet.Write(xButtons);               
                SendUDPData(_packet);
            }
        }
        public static void AnalogState(int[] inputs)
        {
            using (Packet _packet = new Packet((int)ClientPackets.AnalogState))
            {
                foreach (var item in inputs)
                {
                    _packet.Write(item);
                }
                SendUDPData(_packet);
            }
        }
        #endregion
    }
}
