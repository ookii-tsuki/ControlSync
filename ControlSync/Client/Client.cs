using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System;
using System.Diagnostics;

namespace ControlSync.Client
{
    public static class Client
    {
        public delegate void Event();

        public static int dataBufferSize = 4096;

        public static string ip = "127.0.0.1";
        public static int port = 9600;
        public static int myId = 0;
        public static TCP tcp;
        public static UDP udp;
        public static bool isConnected = false;
        public static bool isHost => myId == 1;

        public static Event onFailConnect;
        public static Event onDisconnect;
        public static Event onConnect;
        
        private delegate void PacketHandler(Packet _packet);
        private static Dictionary<int, PacketHandler> packetHandlers;

        public static void ConnectToServer(string _ip, int _port)
        {
            ip = _ip;
            port = _port;

            tcp = new TCP();
            udp = new UDP();
            InitializeClientData();

            tcp.Connect();
        }

        public class TCP
        {
            public TcpClient socket;

            private NetworkStream stream;
            private Packet receivedData;
            private byte[] receiveBuffer;

            public void Connect()
            {
                try
                {
                    socket = new TcpClient { ReceiveBufferSize = dataBufferSize, SendBufferSize = dataBufferSize };
                    receiveBuffer = new byte[dataBufferSize];
                    socket.BeginConnect(ip, port, ConnectCallback, socket);
                }
                catch (Exception ex)
                { 
                    ClientPg.Log(ex.Message);
                }
            }

            private void ConnectCallback(IAsyncResult _result)
            {
                try
                {
                    socket.EndConnect(_result);

                    if (!socket.Connected)
                    {
                        return;
                    }

                    stream = socket.GetStream();
                    receivedData = new Packet();

                    stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
                    isConnected = true;
                    onConnect?.Invoke();
                }
                catch (Exception ex)
                {
                    ClientPg.Log(ex.Message);
                    onFailConnect?.Invoke();
                }
            }

            public void SendData(Packet _packet)
            {
                try
                {
                    if (socket != null)
                    {
                        stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
                    }
                }
                catch (Exception _ex)
                {
                    ClientPg.Log($"Error sending data to server via TCP: {_ex}");
                    onFailConnect?.Invoke();
                }
            }

            private void ReceiveCallback(IAsyncResult _result)
            {
                try
                {
                    int _byteLength = stream.EndRead(_result);
                    if (_byteLength <= 0)
                    {
                        Client.Disconnect();
                        return;
                    }

                    byte[] _data = new byte[_byteLength];
                    Array.Copy(receiveBuffer, _data, _byteLength);

                    receivedData.Reset(HandleData(_data));
                    stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
                }
                catch (Exception ex)
                {
                    Disconnect();
                    onFailConnect?.Invoke();
                }
            }

            private bool HandleData(byte[] _data)
            {
                int _packetLength = 0;

                receivedData.SetBytes(_data);

                if (receivedData.UnreadLength() >= 4)
                {
                    _packetLength = receivedData.ReadInt();
                    if (_packetLength <= 0)
                    {
                        return true;
                    }
                }

                while (_packetLength > 0 && _packetLength <= receivedData.UnreadLength())
                {
                    byte[] _packetBytes = receivedData.ReadBytes(_packetLength);
                    ThreadManager.ExecuteOnMainThread(() =>
                    {
                        using (Packet _packet = new Packet(_packetBytes))
                        {
                            int _packetId = _packet.ReadInt();
                            packetHandlers[_packetId](_packet);
                        }
                    });

                    _packetLength = 0;
                    if (receivedData.UnreadLength() >= 4)
                    {
                        _packetLength = receivedData.ReadInt();
                        if (_packetLength <= 0)
                        {
                            return true;
                        }
                    }
                }

                if (_packetLength <= 1)
                {
                    return true;
                }

                return false;
            }

            public void Disconnect()
            {
                Client.Disconnect();

                stream = null;
                receivedData = null;
                receiveBuffer = null;
                socket = null;
            }
        }

        public class UDP
        {
            public UdpClient socket;
            public IPEndPoint endPoint;

            public UDP()
            {
                try
                {
                    endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                }catch(Exception ex)
                {
                    ClientPg.Log(ex.Message);
                }
            }

            public void Connect(int _localPort)
            {
                try
                {
                    socket = new UdpClient(_localPort);

                    socket.Connect(endPoint);
                    socket.BeginReceive(ReceiveCallback, null);

                    using (Packet _packet = new Packet())
                    {
                        SendData(_packet);
                    }
                }catch(Exception ex)
                {
                    ClientPg.Log(ex.Message);
                    onFailConnect?.Invoke();
                }
            }

            public void SendData(Packet _packet)
            {
                try
                {
                    _packet.InsertInt(myId);
                    if (socket != null)
                    {
                        socket.BeginSend(_packet.ToArray(), _packet.Length(), null, null);
                    }
                }
                catch (Exception _ex)
                {
                    ClientPg.Log($"Error sending data to server via UDP: {_ex}");
                    onFailConnect?.Invoke();
                }
            }

            private void ReceiveCallback(IAsyncResult _result)
            {
                try
                {
                    byte[] _data = socket.EndReceive(_result, ref endPoint);
                    socket.BeginReceive(ReceiveCallback, null);

                    if (_data.Length < 4)
                    {
                        Client.Disconnect();
                        return;
                    }

                    HandleData(_data);
                }
                catch
                {
                    Disconnect();
                    onFailConnect?.Invoke();
                }
            }

            private void HandleData(byte[] _data)
            {
                using (Packet _packet = new Packet(_data))
                {
                    int _packetLength = _packet.ReadInt();
                    _data = _packet.ReadBytes(_packetLength);
                }

                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_data))
                    {
                        int _packetId = _packet.ReadInt();
                        packetHandlers[_packetId](_packet);
                    }
                });
            }

            public void Disconnect()
            {
                Client.Disconnect();

                endPoint = null;
                socket = null;
            }
        }

        private static void InitializeClientData()
        {
            packetHandlers = new Dictionary<int, PacketHandler>()
            {
                { (int)ServerPackets.Welcome, ClientHandle.Welcome },
                { (int)ServerPackets.ConnectPlayer, ClientHandle.ConnectPlayer },
                { (int)ServerPackets.DisconnectPlayer, ClientHandle.DisconnectPlayer },
                { (int)ServerPackets.ButtonState, ClientHandle.ButtonState },
                { (int)ServerPackets.AnalogState, ClientHandle.AnalogState },
                { (int)ServerPackets.VideoBuffer, ClientHandle.VideoBuffer}
            };
        }

        public static void Disconnect()
        {
            if (isConnected)
            {
                try
                {
                    isConnected = false;
                    Manager.DisconnectAll();
                    tcp.socket.Close();
                    udp.socket.Close();
                    ClientPg.Log("Disconnected from server.");
                    onDisconnect?.Invoke();
                }catch(Exception ex)
                {
                    ClientPg.Log(ex.Message);
                }
            }
        }
    }
}
