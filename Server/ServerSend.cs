using System;
using System.Collections.Generic;
using System.Text;

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
            using (Packet _packet = new Packet((int)ServerPackets.Welcome))
            {
                _packet.Write(_msg);
                _packet.Write(_toClient);

                SendTCPData(_toClient, _packet);
            }
        }

        public static void ConnectPlayer(int _toClient, Player _player)
        {
            using (Packet _packet = new Packet((int)ServerPackets.ConnectPlayer))
            {
                _packet.Write(_player.id);
                _packet.Write(_player.username);

                SendTCPData(_toClient, _packet);
            }
        }
        public static void DisconnectPlayer(int _toClient, Player _player)
        {
            using (Packet _packet = new Packet((int)ServerPackets.DisconnectPlayer))
            {
                _packet.Write(_player.id);

                SendTCPData(_toClient, _packet);
            }
        }

        public static void ButtonState(Player _player)
        {
            using (Packet _packet = new Packet((int)ServerPackets.ButtonState))
            {
                _packet.Write(_player.id);
                _packet.Write(_player.buttonState);

                SendTCPDataToAll(_packet); // was UDP
            }
        }
        public static void AnalogState(Player _player)
        {
            using Packet _packet = new Packet((int)ServerPackets.AnalogState);
            
            _packet.Write(_player.id);
            for (int i = 0; i < _player.analogInput.Length; i++)
                _packet.Write(_player.analogInput[i]);

            SendTCPDataToAll(_packet); // was UDP
            
        }
        public static void VideoBuffer(Player _player)
        {
            if (_player.id != 1 || _player.videoBuffer == null)
                return;
            using Packet _packet = new Packet((int)ServerPackets.VideoBuffer);
            
            _packet.Write(_player.id);
            _packet.Write(_player.originalsize);
            _packet.Write(_player.videoBuffer.Length);
            _packet.Write(_player.videoBuffer);
            
            // starting from 2 to not sent the buffer to the host client
            SendUDPDataToAll(1, _packet);
        }
        #endregion
    }
}