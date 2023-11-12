using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using LZ4;
using System.Diagnostics;

namespace ControlSync.Client
{
    public static class Manager
    {

        public static Dictionary<int, PlayerManager> players = new Dictionary<int, PlayerManager>();

        public static void ConnectController(int _id, string _username)
        {
            PlayerManager player = new PlayerManager(_id, _username);
            player.ConnectController();

            players.Add(_id, player);
            UpdateList();
        }
        public static void DisconnectPlayer(int _id)
        {
            players[_id].DisconnectController();
            players.Remove(_id);
            UpdateList();
        }
        public static void DisconnectAll()
        {
            foreach (var player in players)
            {
                player.Value.DisconnectController();
            }
            players.Clear();
            UpdateList();
        }
        public static void ShowScreen()
        {
            if (!Client.isHost)
            {
                ScreenView.instance.Dispatcher.Invoke(ScreenView.instance.Show);
            }
        }
        public static void UpdateScreenView(byte[] buffer, int originalSize)
        {
            var uncompressedBuffer = LZ4Codec.Decode(buffer, 0, buffer.Length, originalSize);

            if (ScreenView.instance == null)
                return;
            Debug.WriteLine(uncompressedBuffer.Length);
            ScreenView.instance.Dispatcher.Invoke(() =>
            {
                ScreenView.instance.viewer.Source = LoadImage(uncompressedBuffer);
            });
        }
        private static void UpdateList()
        {
            ClientPg.instance.Dispatcher.Invoke(() => {
                ClientPg.instance.playerList.ItemsSource = null;
                ClientPg.instance.playerList.ItemsSource = ClientPg.instance.Players;
            });
        }


        private static BitmapSource LoadImage(byte[] imageData)
        {
            return BitmapSource.Create(1920 / 2, 1080 / 2, 300, 300, PixelFormats.Rgb24, null, imageData, (1920 / 2) * 3);
        }
    }
}
