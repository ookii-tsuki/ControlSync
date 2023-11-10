using ScpDriverInterface;

namespace ControlSync
{
    public static class ScpManager
    {
        static ScpBus scpBus = new ScpBus();

        public static void Plug(int controllerIndex)
            => scpBus.PlugIn(controllerIndex);
        public static void Unplug(int controllerIndex)
            => scpBus.Unplug(controllerIndex);
        public static void UnplugAll()
            => scpBus.UnplugAll();
        public static void Report(int controllerIndex, byte[] report)
            => scpBus.Report(controllerIndex, report);
    }
}
