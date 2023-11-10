using System;
using System.Collections.Generic;
using System.Text;

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
        private static void UpdateList()
        {
            ClientPg.instance.Dispatcher.Invoke(() => {
                ClientPg.instance.playerList.ItemsSource = null;
                ClientPg.instance.playerList.ItemsSource = ClientPg.instance.Players;
            });
        }
    }
}
