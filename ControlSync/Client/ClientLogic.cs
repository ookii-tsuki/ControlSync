using System;

namespace ControlSync.Client
{
    /// <summary>
    /// Basic logic for the client.
    /// </summary>
    static class ClientLogic
    {
        public static int Fps { get; private set; }

        private static float deltaTime;
        private static DateTime lastFrame;
        public static void Start()
        {
            if (Client.isHost)
                HostPeer.StartPeerConnection();
        }
        public static void Update()
        {
            ControllerInput.SendInputToServer();
            CalculateFPS();
        }

        private static void CalculateFPS()
        {
            deltaTime = (float)(DateTime.Now - lastFrame).TotalSeconds;
            lastFrame = DateTime.Now;

            Fps = (int)(1f / deltaTime);
        }
    }
}
