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
        public int[] analogInput = new int[6];
        public string base64Offer;

        public Player(int _id, string _username)
        {
            id = _id;
            username = _username;
        }

        public void Update()
        {
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
    }
}