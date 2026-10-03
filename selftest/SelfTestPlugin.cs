using BepInEx;

namespace Scry
{
    /// <summary>
    /// The self-test as a plugin of its own beside Scry: it registers with Scry as it loads
    /// (<see cref="SelfTestHost"/>), and runs only with Scry's SelfTest setting on.
    /// </summary>
    [BepInPlugin(Guid, "Scry self-test", About.Version)]
    [BepInDependency(About.Guid)]
    [BepInProcess("valheim.exe")]
    public class SelfTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Scry.SelfTest";

        private void Awake() => SelfTestHost.Register(new SelfTest.Runner());
    }
}
