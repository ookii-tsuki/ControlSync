using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControlSync.Client
{
    /// <summary>
    /// Manages the players connected to the host.
    /// </summary>
    public static class Manager
    {

        public static Dictionary<int, PlayerManager> players = new Dictionary<int, PlayerManager>();

        /// <summary>
        /// Connects a controller to the host.
        /// </summary>
        /// <param name="_id">The id of the player</param>
        /// <param name="_username">The username of the player</param>
        public static void ConnectController(int _id, string _username)
        {
            PlayerManager player = new PlayerManager(_id, _username);

            if (Client.isHost)
                player.ConnectController();

            players.Add(_id, player);
            UpdateList();
        }
        /// <summary>
        /// Disconnects a controller from the host.
        /// </summary>
        public static void DisconnectPlayer(int _id)
        {
            if (Client.isHost)
                players[_id].DisconnectController();
            players.Remove(_id);
            UpdateList();
        }
        /// <summary>
        /// Disconnects all controllers from the host.
        /// </summary>
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
        /// <summary>
        /// Shows the screen of the host.
        /// </summary>
        public static void ShowScreen()
        {
            if (!Client.isHost)
            {
                ScreenView.instance.Dispatcher.Invoke(ScreenView.instance.Show);
                ClientPg.instance.Dispatcher.Invoke(() => { ClientPg.instance.showScreen.Visibility = Visibility.Hidden; });
            }
        }

        /// <summary>
        /// Closes the screen of the host.
        /// </summary>
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

        /// <summary>
        /// Updates the screen view with a new frame.
        /// </summary>
        /// <param name="buffer">Frame buffer</param>
        /// <param name="width">Frame width</param>
        /// <param name="height">Frame height</param>
        /// <param name="stride">Frame stride</param>
        public static void UpdateScreenView(byte[] buffer, int width, int height, int stride)
        {

            if (ScreenView.instance == null)
                return;

            ScreenView.instance.Dispatcher.Invoke(() =>
            {
                ScreenView.instance.viewer.Source = LoadImage(buffer, width, height, stride);
            });
        }

        /// <summary>
        /// Updates the UI player list.
        /// </summary>
        private static void UpdateList()
        {
            ClientPg.instance.Dispatcher.Invoke(() =>
            {
                ClientPg.instance.playerList.ItemsSource = null;
                ClientPg.instance.playerList.ItemsSource = ClientPg.instance.Players;
            });
        }

        /// <summary>
        /// Creates a bitmap image from a byte array.
        /// </summary>
        /// <param name="imageData">Frame buffer</param>
        /// <param name="width">Width of the frame</param>
        /// <param name="height">Height of the frame</param>
        /// <param name="stride">Stride of the frame</param>
        /// <returns></returns>
        private static BitmapSource LoadImage(byte[] imageData, int width, int height, int stride)
        {
            return BitmapSource.Create(width, height, 300, 300, PixelFormats.Rgb24, null, imageData, stride);
        }
    }
}
