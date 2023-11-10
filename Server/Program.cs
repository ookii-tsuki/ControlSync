using System;
using System.Threading;

namespace Server
{
    class Program
    {
        private static bool isRunning = false;

        static void Main(string[] args)
        {
            int port = 9600;
            try
            {
                if (args.Length > 0)
                    port = int.Parse(args[0]);
                else
                    Environment.Exit(-1);
            }
            catch { 
                Console.WriteLine("Error parsing the port");
                Environment.Exit(-1);
            }
            isRunning = true;

            Thread mainThread = new Thread(new ThreadStart(MainThread));
            mainThread.Start();

            Server.Start(4, port);
        }

        private static void MainThread()
        {
            Console.WriteLine($"Main thread started. Running at {Constants.TICKS_PER_SEC} ticks per second.");
            DateTime _nextLoop = DateTime.Now;

            while (isRunning)
            {
                while (_nextLoop < DateTime.Now)
                {
                    ServerLogic.Update();

                    _nextLoop = _nextLoop.AddMilliseconds(Constants.MS_PER_TICK);

                    if (_nextLoop > DateTime.Now)
                    {
                        Thread.Sleep(_nextLoop - DateTime.Now);
                    }
                }
            }
        }
    }
}
