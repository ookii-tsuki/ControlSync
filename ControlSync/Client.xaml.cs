using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace ControlSync
{
    /// <summary>
    /// Interaction logic for Client.xaml
    /// </summary>
    public partial class ClientPg : Page
    {
        public static ClientPg instance;
        static string logData;

        public Client.Client.Event onConnect, onDisconnect;
        public List<Client.PlayerManager> Players { get => Client.Manager.players.Values.OrderBy(x => x.Id).ToList();}
        public ClientPg()
        {
            InitializeComponent();
            if(instance != null)
            {
                portTB.Text = instance.portTB.Text;
                ipTB.Text = instance.ipTB.Text;
                uidTB.Text = instance.uidTB.Text;
            }
            instance = this;
            clientPg.Background = null;
            consoleBox.AppendText(logData);
            if (Client.Client.isConnected)
                joinBtn.Content = "Disconnect from server";
            else
                joinBtn.Content = "Connect to server";
            DataContext = this;

            onConnect = () => instance.Dispatcher.Invoke(() => { joinBtn.Content = "Disconnect from server"; joinBtn.IsEnabled = true; });
            onDisconnect = () => instance.Dispatcher.Invoke(() => { joinBtn.Content = "Connect to server"; joinBtn.IsEnabled = true; });

            Client.Client.onConnect += onConnect;
            Client.Client.onDisconnect += onDisconnect;
            Client.Client.onFailConnect += onDisconnect;
        }
        private void CheckPortValidation(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void joinBtn_Click(object sender, RoutedEventArgs e)
        {
            
            if (!Client.Client.isConnected)
            {
                if (portTB.Text.Length == 0)
                {
                    Log("You must provite a valid port");
                    return;
                }
                if (ipTB.Text.Length == 0)
                {
                    Log("You must provite a valid ip");
                    return;
                }
                if (uidTB.Text.Length == 0)
                {
                    Log("Username cannot be empty");
                    return;
                }
                joinBtn.IsEnabled = false;
                Client.Client.ConnectToServer(ipTB.Text, int.Parse(portTB.Text));
                
            }
            else
            {
                Client.Client.Disconnect();                
            }
        }
        public static void Log(string message)
        {
            logData += message + "\n";
            instance.Dispatcher.Invoke(() => 
            { 
                instance.consoleBox.AppendText(message + "\n");
                instance.consoleBox.ScrollToEnd();
            });
        }
    }
}
