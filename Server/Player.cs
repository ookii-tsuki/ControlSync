using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace Server
{
    class Player
    {
        public int id;
        public string username;

        public int buttonState;
        public short[] analogInput = new short[6];
        public string base64Offer;
        public List<string> iceCandidates = new List<string>();

        public Player(int _id, string _username)
        {
            id = _id;
            username = _username;
        }

        public void Update()
        {
            if (id == Client.HOST_ID)
                return;

            ServerSend.ButtonState(this);
            ServerSend.AnalogState(this);
        }

        public void SendOffer(int toId)
        {
            ServerSend.PeerOffer(this, toId);
        }

        public void SendAnswer(string base64Answer)
        {
            ServerSend.PeerAnswer(base64Answer);
        }

        public void SendICECandidate(string base64ICECandidate, int toId)
        {
            ServerSend.ICECandidate(base64ICECandidate, toId);
        }
        public void SendAllICECandidates(int toId)
        {
            foreach (var iceCandidate in iceCandidates)
            {
                ServerSend.ICECandidate(iceCandidate, toId);
            }
        }
    }
}