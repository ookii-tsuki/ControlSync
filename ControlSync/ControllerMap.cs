using Newtonsoft.Json;
using ScpDriverInterface;
using System.Windows.Input;

namespace ControlSync
{
    public enum Analog
    {
        LeftTrigger,
        RightTrigger,
        RightStickX,
        RightStickY,
        LeftStickX,
        LeftStickY
    }
    public class AnalogInput
    {
        public Analog Type { get; set; }
        public int Value { get; set; }
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ControllerMap
    {
        [JsonProperty]
        public Key PcControl { get; set; }

        [JsonProperty]
        public X360Buttons? XBtnControl { get; set; }

        [JsonProperty]
        public AnalogInput XAnalogControl { get; set; }

        public string PcControlName { get => PcControl.ToString();}
        public string XControlName { get
            {
                if (XBtnControl != null)
                    return XBtnControl.ToString();
                string name = XAnalogControl.Type.ToString();
                name = XAnalogControl.Value < 0 ? "-" + name : "+" + name;
                return name;
            } 
        }
    }
}
