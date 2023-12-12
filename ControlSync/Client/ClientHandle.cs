using ScpDriverInterface;
using System.Net;

namespace ControlSync.Client
{
    /// <summary>
    /// Methods for handling packets from the server.
    /// </summary>
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
            if (Manager.players.ContainsKey(_id))
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
                        Manager.players[_id].Trigger((Analog)i, (byte)_packet.ReadShort());
                    else
                    {
                        short val = _packet.ReadShort();
                        Manager.players[_id].MoveStick((Analog)i, val);
                    }
                }
            }

        }

        public static void PeerOffer(Packet _packet)
        {
            if (Client.isHost) // this offer is meant for clients who will watch the stream
                return;

            string base64Offer = _packet.ReadString();

            ClientPg.Log("received offer");

            ClientPeer.StartPeerConnection(base64Offer);
        }

        public static void PeerAnswer(Packet _packet)
        {
            if (!Client.isHost) // this answer for the host who hosts the stream
                return;

            string base64Answer = _packet.ReadString();

            HostPeer.HandleAnswer(base64Answer);
        }

        public static void ICECandidate(Packet _packet)
        {
            string base64ICECandidate = _packet.ReadString();

            if (Client.isHost)
                HostPeer.AddICECandidate(base64ICECandidate);
            else
                ClientPeer.AddICECandidate(base64ICECandidate);
        }

        public static void ClosePeerConnection(Packet _packet)
        {
            if (Client.isHost) // this answer for the host who hosts the stream
                return;

            ClientPeer.CloseConnection();
            Manager.CloseScreen();
        }
    }
}
