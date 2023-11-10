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
    }
}