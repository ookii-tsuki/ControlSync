using ScpDriverInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace ControlSync.Client
{
    public static class ControllerInput
    {
        public static void SendInputToServer()
        {
            if (Client.isConnected)
            {
                int[] input = new int[6]
                {
                    0, 0, 0, 0, 0, 0
                };
                X360Buttons buttons = 0;
                foreach (var button in Mapping.buttons)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Keyboard.IsKeyDown(button.PcControl))
                        {
                            if (button.XBtnControl != null)
                            {
                                buttons |= (X360Buttons)button.XBtnControl;
                            }
                            else
                            {
                                input[(int)button.XAnalogControl.Type] = button.XAnalogControl.Value;
                            }
                        }
                        else
                            if (button.XBtnControl != null)
                                buttons &= ~(X360Buttons)button.XBtnControl;
                    });
                }
                ClientSend.ButtonState((int)buttons);
                ClientSend.AnalogState(input);
            }
        }
    }
}
