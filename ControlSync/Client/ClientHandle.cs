using System.Collections;
using System.Numerics;
using System.Net;
using ScpDriverInterface;

namespace ControlSync.Client
{
    public class ClientHandle
    {
        public static void Welcome(Packet _packet)
        {
            string _msg = _packet.ReadString();
            int _myId = _packet.ReadInt();

            Client.myId = _myId;
            ClientSend.WelcomeReceived();
            ClientPg.Log(_msg);

            Client.udp.Connect(((IPEndPoint)Client.tcp.socket.Client.LocalEndPoint).Port);

            ClientLogic.Start();
        }

        public static void ConnectPlayer(Packet _packet)
        {
            int _id = _packet.ReadInt();
            string _username = _packet.ReadString();

            Manager.ConnectController(_id, _username);
        }
        public static void DisconnectPlayer(Packet _packet)
        {
            int _id = _packet.ReadInt();
            Manager.DisconnectPlayer(_id);
        }

        public static void ButtonState(Packet _packet)
        {
            int _id = _packet.ReadInt();
            int clickState = _packet.ReadInt();
            if(Manager.players.ContainsKey(_id))
                Manager.players[_id].Click((X360Buttons)clickState);
        }
        public static void AnalogState(Packet _packet)
        {
            int _id = _packet.ReadInt();
            if (Manager.players.ContainsKey(_id)) 
            {
                for (int i = 0; i < 6; i++)
                {
                    if ((Analog)i == Analog.LeftTrigger || (Analog)i == Analog.RightTrigger)
                        Manager.players[_id].Trigger((Analog)i, (byte)_packet.ReadInt());
                    else
                    {
                        int val = _packet.ReadInt();
                        Manager.players[_id].MoveStick((Analog)i, (short)val);
                    }
                }
            }

        }

        public static void VideoBuffer(Packet _packet)
        {
            if (Client.isHost)
                return;

            int _id = _packet.ReadInt();
            int originalSize = _packet.ReadInt();
            int compressedSize = _packet.ReadInt();
            byte[] buffer = _packet.ReadBytes(compressedSize);

            Manager.UpdateScreenView(buffer, originalSize);
        }
    }
}
