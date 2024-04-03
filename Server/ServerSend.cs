using System;

namespace Server
{
    class ServerSend
    {
        private static void SendTCPData(int _toClient, Packet _packet)
        {
            _packet.WriteLength();
            Server.clients[_toClient].tcp.SendData(_packet);
        }

        private static void SendUDPData(int _toClient, Packet _packet)
        {
            _packet.WriteLength();
            Server.clients[_toClient].udp.SendData(_packet);
        }

        private static void SendTCPDataToAll(Packet _packet)
        {
            _packet.WriteLength();
            for (int i = 1; i <= Server.MaxPlayers; i++)
            {
                Server.clients[i].tcp.SendData(_packet);
            }
        }
        private static void SendTCPDataToAll(int _exceptClient, Packet _packet)
        {
            _packet.WriteLength();
            for (int i = 1; i <= Server.MaxPlayers; i++)
            {
                if (i != _exceptClient)
                {
                    Server.clients[i].tcp.SendData(_packet);
                }
            }
        }

        private static void SendUDPDataToAll(Packet _packet)
        {
            _packet.WriteLength();
            for (int i = 1; i <= Server.MaxPlayers; i++)
            {
                Server.clients[i].udp.SendData(_packet);
            }
        }
        private static void SendUDPDataToAll(int _exceptClient, Packet _packet)
        {
            _packet.WriteLength();
            for (int i = 1; i <= Server.MaxPlayers; i++)
            {
                if (i != _exceptClient)
                {
                    Server.clients[i].udp.SendData(_packet);
                }
            }
        }

        #region Packets
        public static void Welcome(int _toClient, string _msg)
        {
            using Packet _packet = new Packet((int)ServerPackets.Welcome);
         
            _packet.Write(_msg);
            _packet.Write(_toClient);

            SendTCPData(_toClient, _packet);
            
        }

        public static void ConnectPlayer(int _toClient, Player _player)
        {
            using Packet _packet = new Packet((int)ServerPackets.ConnectPlayer);
         
            _packet.Write(_player.id);
            _packet.Write(_player.username);

            SendTCPData(_toClient, _packet);
         
        }
        public static void DisconnectPlayer(int _toClient, Player _player)
        {
            using Packet _packet = new Packet((int)ServerPackets.DisconnectPlayer);
          
            _packet.Write(_player.id);

            SendTCPData(_toClient, _packet);
          
        }

        public static void ButtonState(Player _player)
        {
            using Packet _packet = new Packet((int)ServerPackets.ButtonState);
         
            _packet.Write(_player.id);
            _packet.Write(_player.buttonState);

            SendUDPData(Client.HOST_ID, _packet);
         
        }
        public static void AnalogState(Player _player)
        {
            using Packet _packet = new Packet((int)ServerPackets.AnalogState);

            _packet.Write(_player.id);
            for (int i = 0; i < _player.analogInput.Length; i++)
                _packet.Write(_player.analogInput[i]);

            SendUDPData(Client.HOST_ID, _packet);

        }
        public static void PeerOffer(string base64Offer, int toId)
        {
            using Packet _packet = new Packet((int)ServerPackets.PeerOffer);

            _packet.Write(base64Offer);

            Console.WriteLine("sending offer to player " + toId);

            SendTCPData(toId, _packet);
        }

        public static void PeerAnswer(string base64Answer, int fromId)
        {
            if (!Server.clients.ContainsKey(1))
                return;

            using Packet _packet = new Packet((int)ServerPackets.PeerAnswer);

            _packet.Write(fromId);
            _packet.Write(base64Answer);

            SendTCPData(Client.HOST_ID, _packet);
        }

        public static void ICECandidate(string base64ICECandidate, int toId, int fromId)
        {
            if (!Server.clients.ContainsKey(toId))
                return;

            using Packet _packet = new Packet((int)ServerPackets.ICECandidate);

            _packet.Write(fromId);
            _packet.Write(base64ICECandidate);

            SendTCPData(toId, _packet);
        }

        public static void ClosePeerConnection()
        {
            using Packet _packet = new Packet((int)ServerPackets.ClosePeerConnection);

            Console.WriteLine("Peer connection is closed by the host");

            SendTCPDataToAll(Client.HOST_ID, _packet);
        }

        public static void GenerateOffer(int _toId)
        {
            using Packet _packet = new Packet((int)ServerPackets.GenerateOffer);

            _packet.Write(_toId);

            SendTCPData(Client.HOST_ID, _packet);
        }
        #endregion
    }
}