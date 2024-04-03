using System.Collections.Generic;

namespace Server
{
    class Player
    {
        public int id;
        public string username;

        public int buttonState;
        public short[] analogInput = new short[6];

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


    }
}