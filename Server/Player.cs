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

    }
}