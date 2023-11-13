using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

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

            if (_fromClient != 1)
                Server.clients[1].player.SendOffer(_fromClient);
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
                    Server.clients[_fromClient].player.analogInput[i] = _packet.ReadInt();
        }

        public static void PeerOffer(int _fromClient, Packet _packet)
        {
            if (!Server.clients.ContainsKey(_fromClient) || Server.clients[_fromClient].player == null)
                return;

            string base64Offer = _packet.ReadString();
            Player player = Server.clients[_fromClient].player;
            player.base64Offer = base64Offer;
            for (int i = 1; i <= Server.clients.Count; i++)
            {
                if (i == _fromClient) continue;
                if (!Server.clients.ContainsKey(i) || Server.clients[i].player == null) continue;

                ServerSend.PeerOffer(player, i);
            }
        }

        public static void PeerAnswer(int _fromClient, Packet _packet)
        {
            if (!Server.clients.ContainsKey(_fromClient) || Server.clients[_fromClient].player == null)
                return;

            string base64Answer = _packet.ReadString();
            Server.clients[_fromClient].player.SendAnswer(base64Answer);
        }

        public static void ICECandidate(int _fromClient, Packet _packet)
        {
            if (!Server.clients.ContainsKey(_fromClient) || Server.clients[_fromClient].player == null)
                return;

            int toId = _packet.ReadInt();
            string base64ICECandidate = _packet.ReadString();
            Server.clients[_fromClient].player.SendICECandidate(base64ICECandidate, toId);
        }

        public static void ClosePeerConnection(int _fromClient, Packet _packet)
        {
            ServerSend.ClosePeerConnection();
        }
    }
}