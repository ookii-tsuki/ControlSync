using ScpDriverInterface;
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
                short[] input = new short[6]
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

                if (Client.isHost)
                {
                    int id = Client.myId;
                    if (!Manager.players.ContainsKey(id))
                        return;

                    Manager.players[id].Click(buttons);

                    for (int i = 0; i < 6; i++)
                    {
                        if ((Analog)i == Analog.LeftTrigger || (Analog)i == Analog.RightTrigger)
                            Manager.players[id].Trigger((Analog)i, (byte)input[i]);
                        else
                        {
                            Manager.players[id].MoveStick((Analog)i, input[i]);
                        }
                    }
                }
                else
                {
                    ClientSend.ButtonState((int)buttons);
                    ClientSend.AnalogState(input);
                }
            }
        }
    }
}
