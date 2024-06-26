﻿namespace ControlSync.Client
{

    /// <summary>
    /// Methods for sending packets to the server.
    /// </summary>
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
            using Packet _packet = new Packet((int)ClientPackets.WelcomeReceived);

            _packet.Write(Client.myId);
            ClientPg.instance.Dispatcher.Invoke(() => _packet.Write(ClientPg.instance.uidTB.Text));

            SendTCPData(_packet);

        }

        public static void ButtonState(int xButtons)
        {
            using Packet _packet = new Packet((int)ClientPackets.ButtonState);

            _packet.Write(xButtons);
            SendUDPData(_packet);

        }
        public static void AnalogState(short[] inputs)
        {
            using Packet _packet = new Packet((int)ClientPackets.AnalogState);

            foreach (var item in inputs)
            {
                _packet.Write(item);
            }
            SendUDPData(_packet);
        }

        public static void PeerOffer(string base64Offer, int toId)
        {
            using Packet _packet = new Packet((int)ClientPackets.PeerOffer);

            _packet.Write(toId);
            _packet.Write(base64Offer);

            SendTCPData(_packet);

        }

        public static void PeerAnswer(string base64Answer, int fromId)
        {
            using Packet _packet = new Packet((int)ClientPackets.PeerAnswer);

            _packet.Write(fromId);
            _packet.Write(base64Answer);

            SendTCPData(_packet);

        }

        public static void ICECandidate(string base64ICECandidate, int toId)
        {
            using Packet _packet = new Packet((int)ClientPackets.ICECandidate);

            _packet.Write(toId);
            _packet.Write(base64ICECandidate);

            SendTCPData(_packet);

        }

        public static void ClosePeerConnection()
        {
            using Packet _packet = new Packet((int)ClientPackets.ClosePeerConnection);

            SendTCPData(_packet);
        }
        #endregion
    }
}
