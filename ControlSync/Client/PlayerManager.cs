using ScpDriverInterface;

namespace ControlSync.Client
{
    public class PlayerManager
    {
        public int Id { get; set; }
        public string Username { get; set; }

        private X360Controller controller;

        public PlayerManager(int id, string username)
        {
            Id = id;
            Username = username;
        }

        public void ConnectController()
        {
            ScpManager.Plug(Id);
            controller = new X360Controller();
        }

        public void DisconnectController()
        {
            ScpManager.Unplug(Id);
            controller = null;
        }
        public void Click(X360Buttons button)
        {
            controller.Buttons = button;
            ScpManager.Report(Id, controller.GetReport());
        }
        public void Trigger(Analog analogInput, byte value)
        {
            switch (analogInput)
            {
                case Analog.RightTrigger:
                    controller.RightTrigger = value;
                    ScpManager.Report(Id, controller.GetReport());
                    break;
                case Analog.LeftTrigger:
                    controller.LeftTrigger = value;
                    ScpManager.Report(Id, controller.GetReport());
                    break;
            }
        }
        public void MoveStick(Analog analogInput, short value)
        {
            switch (analogInput)
            {
                case Analog.RightStickX:
                    controller.RightStickX = value;
                    break;
                case Analog.RightStickY:
                    controller.RightStickY = value;
                    break;
                case Analog.LeftStickX:
                    controller.LeftStickX = value;
                    break;
                case Analog.LeftStickY:
                    controller.LeftStickY = value;
                    break;
            }
            ScpManager.Report(Id, controller.GetReport());
        }
    }
}
