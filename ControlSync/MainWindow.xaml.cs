﻿using ControlSync.Client;
using ModernWpf.Controls;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace ControlSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }
        public MainWindow()
        {
            InitializeComponent();
            content.Navigate(typeof(Host));

            Thread mainThread = new Thread(new ThreadStart(MainThread));
            mainThread.Start();

            Client.Client.onConnect += () => Dispatcher.Invoke(() => mappingPage.IsEnabled = false);
            Client.Client.onDisconnect += () => Dispatcher.Invoke(() => mappingPage.IsEnabled = true);
            Client.Client.onFailConnect += () => Dispatcher.Invoke(() => mappingPage.IsEnabled = true);

            new Mapping(); // to initialize the mapping
            Instance = this;
        }
        private static void MainThread()
        {
            DateTime _nextLoop = DateTime.Now;

            while (true)
            {
                while (_nextLoop < DateTime.Now)
                {
                    if (Client.Client.isConnected)
                    {
                        ClientLogic.Update();
                    }

                    ThreadManager.UpdateMain();

                    _nextLoop = _nextLoop.AddMilliseconds(Constants.MS_PER_TICK);

                    if (_nextLoop > DateTime.Now)
                    {
                        Thread.Sleep(_nextLoop - DateTime.Now);
                    }
                }
            }
        }
        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            Type pageType = null;
            if (sender.SelectedItem == hostPage)
            {
                pageType = typeof(Host);
            }
            else if (sender.SelectedItem == clientPage)
            {
                pageType = typeof(ClientPg);
            }
            else if (sender.SelectedItem == mappingPage)
            {
                pageType = typeof(Mapping);
            }
            if (ClientPg.instance != null)
            {
                Client.Client.onConnect -= ClientPg.instance.onConnect;
                Client.Client.onDisconnect -= ClientPg.instance.onDisconnect;
                Client.Client.onFailConnect -= ClientPg.instance.onDisconnect;
            }
            content.Navigate(pageType, null, args.RecommendedNavigationTransitionInfo);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Host.process != null)
                Host.process.Kill();

            if (Client.Client.isHost)
                HostPeer.CloseConnection();
            else
                Client.Client.Disconnect();

            Process.GetCurrentProcess().Kill();
        }
    }
}
