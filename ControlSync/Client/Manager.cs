using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControlSync.Client
{
    public static class Manager
    {

        public static Dictionary<int, PlayerManager> players = new Dictionary<int, PlayerManager>();

        public static void ConnectController(int _id, string _username)
        {
            PlayerManager player = new PlayerManager(_id, _username);

            if (Client.isHost)
                player.ConnectController();

            players.Add(_id, player);
            UpdateList();
        }
        public static void DisconnectPlayer(int _id)
        {
            if (Client.isHost)
                players[_id].DisconnectController();
            players.Remove(_id);
            UpdateList();
        }
        public static void DisconnectAll()
        {
            if (Client.isHost)
            {
                foreach (var player in players)
                {
                    player.Value.DisconnectController();
                }
            }
            players.Clear();
            UpdateList();
        }
        public static void ShowScreen()
        {
            if (!Client.isHost)
            {
                ScreenView.instance.Dispatcher.Invoke(ScreenView.instance.Show);
                ClientPg.instance.Dispatcher.Invoke(() => { ClientPg.instance.showScreen.Visibility = Visibility.Hidden; });
            }
        }
        public static void CloseScreen()
        {
            ScreenView.instance.Dispatcher.Invoke(ScreenView.instance.Hide);
            if (!Client.isHost)
            {
                if (ClientPeer.ConnectionState == SIPSorcery.Net.RTCPeerConnectionState.connected)
                    ClientPg.instance.Dispatcher.Invoke(() => { ClientPg.instance.showScreen.Visibility = Visibility.Visible; });
                else
                    ClientPg.instance.Dispatcher.Invoke(() => { ClientPg.instance.showScreen.Visibility = Visibility.Hidden; });
            }
        }
        public static void UpdateScreenView(byte[] buffer, int width, int height, int stride)
        {
            //var uncompressedBuffer = LZ4Codec.Decode(buffer, 0, buffer.Length, originalSize);

            if (ScreenView.instance == null)
                return;
            //Debug.WriteLine(uncompressedBuffer.Length);
            ScreenView.instance.Dispatcher.Invoke(() =>
            {
                ScreenView.instance.viewer.Source = LoadImage(buffer, width, height, stride);
            });
        }
        private static void UpdateList()
        {
            ClientPg.instance.Dispatcher.Invoke(() =>
            {
                ClientPg.instance.playerList.ItemsSource = null;
                ClientPg.instance.playerList.ItemsSource = ClientPg.instance.Players;
            });
        }


        private static BitmapSource LoadImage(byte[] imageData, int width, int height, int stride)
        {
            return BitmapSource.Create(width, height, 300, 300, PixelFormats.Rgb24, null, imageData, stride);
        }
    }
}
