using System;

namespace Server
{
    class ServerHandle
    {
        public static void WelcomeReceived(int _fromClient, Packet _packet)
        {
            int _clientIdCheck = _packet.ReadInt();
            string _username = _packet.ReadString();

            Console.WriteLine($"{Server.clients[_fromClient].tcp.socket.Client.RemoteEndPoint} connected successfully and is now player {_fromClient}.");
            if (_fromClient != _clientIdCheck)
            {
                Console.WriteLine($"Player \"{_username}\" (ID: {_fromClient}) has assumed the wrong client ID ({_clientIdCheck})!");
            }
            Server.clients[_fromClient].SendIntoGame(_username);

            if (_fromClient != Client.HOST_ID)
            {
                //Player hostPlayer = Server.clients[Client.HOST_ID].player;
                //
                //hostPlayer.SendAllICECandidates(_fromClient);
                //hostPlayer.SendOffer(_fromClient);
                ServerSend.GenerateOffer(_fromClient);
            }
        }

        public static void ButtonState(int _fromClient, Packet _packet)
        {
            if (Server.clients.ContainsKey(_fromClient) && Server.clients[_fromClient].player != null)
                Server.clients[_fromClient].player.buttonState = _packet.ReadInt();
        }
        public static void AnalogState(int _fromClient, Packet _packet)
        {
            if (Server.clients.ContainsKey(_fromClient) && Server.clients[_fromClient].player != null)
                for (int i = 0; i < 6; i++)
                    Server.clients[_fromClient].player.analogInput[i] = _packet.ReadShort();
        }

        public static void PeerOffer(int _fromClient, Packet _packet)
        {
            if (!Server.clients.ContainsKey(_fromClient) || Server.clients[_fromClient].player == null)
                return;

            int toId = _packet.ReadInt();
            string base64Offer = _packet.ReadString();

            if (!Server.clients.ContainsKey(toId) || Server.clients[toId].player == null) return;

            ServerSend.PeerOffer(base64Offer, toId);
        }

        public static void PeerAnswer(int _fromClient, Packet _packet)
        {
            if (!Server.clients.ContainsKey(_fromClient) || Server.clients[_fromClient].player == null)
                return;

            int fromId = _packet.ReadInt();
            string base64Answer = _packet.ReadString();
            ServerSend.PeerAnswer(base64Answer, fromId);
        }

        public static void ICECandidate(int _fromClient, Packet _packet)
        {
            if (!Server.clients.ContainsKey(_fromClient) || Server.clients[_fromClient].player == null)
                return;

            int toId = _packet.ReadInt();
            string base64ICECandidate = _packet.ReadString();

            ServerSend.ICECandidate(base64ICECandidate, toId, _fromClient);
        }

        public static void ClosePeerConnection(int _fromClient, Packet _packet)
        {
            ServerSend.ClosePeerConnection();
        }
    }
}