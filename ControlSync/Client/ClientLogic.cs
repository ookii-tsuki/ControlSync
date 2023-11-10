using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace ControlSync.Client
{
    class ClientLogic
    {
        public static void Update()
        {
            ControllerInput.SendInputToServer();
            
            ThreadManager.UpdateMain();
        }
    }
}
