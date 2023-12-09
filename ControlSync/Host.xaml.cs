using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlSync
{
    /// <summary>
    /// Interaction logic for Host.xaml
    /// </summary>
    public partial class Host : Page
    {
        static string publicIp;
        static string logData;
        static Host instance;
        public static Process process;

        public Host()
        {
            InitializeComponent();

            hostPg.Background = null;
            if (instance != null)
            {
                portTB.Text = instance.portTB.Text;
            }
            instance = this;
            consoleBox.AppendText(logData);

            try
            {
                if (string.IsNullOrEmpty(publicIp))
                    publicIp = new WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim();
            }
            catch { Log("Failed to get the public IPv4 address"); }
            pubIp.Content = $"Public IPv4: {(!string.IsNullOrEmpty(publicIp) ? publicIp : "N/A")}";

            if (process == null)
                startBtn.Content = "Start server";
            else
                startBtn.Content = "Stop server";
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

        private void startBtn_Click(object sender, RoutedEventArgs e)
        {
            var processes = Process.GetProcessesByName("Server");
            Array.ForEach(processes, p => p.Kill());
            if (process == null)
            {
                if (portTB.Text.Length == 0)
                {
                    Log("You must provite a valid port");
                    return;
                }
                if (!File.Exists("Server.exe"))
                {
                    Log("Failed to start the server process");
                    return;
                }
                process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = "Server.exe";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.Arguments = portTB.Text;
                process.OutputDataReceived += (sender, args) => Log(args.Data);
                process.Start();
                process.BeginOutputReadLine();
                startBtn.Content = "Stop server";
            }
            else
            {
                process.Kill();
                process = null;
                Log("Server stopped.");
                startBtn.Content = "Start server";
            }
        }
        private void CheckPortValidation(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
