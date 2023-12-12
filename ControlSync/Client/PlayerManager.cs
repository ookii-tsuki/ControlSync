using ScpDriverInterface;

namespace ControlSync.Client
{
    public class PlayerManager
    {
        public int Id { get; set; }
        public string Username { get; set; }

        /// <summary>
        /// The controller of the player.
        /// </summary>
        private X360Controller controller;

        public PlayerManager(int id, string username)
        {
            Id = id;
            Username = username;
        }

        /// <summary>
        /// Connects the controller to the host.
        /// </summary>
        public void ConnectController()
        {
            ScpManager.Plug(Id);
            controller = new X360Controller();
        }

        /// <summary>
        /// Disconnects the controller from the host.
        /// </summary>
        public void DisconnectController()
        {
            ScpManager.Unplug(Id);
            controller = null;
        }

        /// <summary>
        /// Signals the controller to click a button.
        /// </summary>
        /// <param name="button">the buttons clicked</param>
        public void Click(X360Buttons button)
        {
            controller.Buttons = button;
            ScpManager.Report(Id, controller.GetReport());
        }

        /// <summary>
        /// Signals the controller press the triggers.
        /// </summary>
        /// <param name="analogInput">analog input</param>
        /// <param name="value">value of the trigger</param>
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
        /// <summary>
        /// Signals the controller to move the sticks.
        /// </summary>
        /// <param name="analogInput">analog input</param>
        /// <param name="value">value of the stick</param>
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
