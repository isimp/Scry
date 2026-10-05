using BepInEx;

namespace Scry
{
    /// <summary>
    /// The self-test as a plugin of its own beside Scry: it registers with Scry as it loads
    /// (<see cref="SelfTestHost"/>). Installing it is what turns the self-test on, so a
    /// release, which packages Scry.dll alone, has nothing of it.
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
