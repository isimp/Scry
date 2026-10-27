using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Scry.Tests
{
    public class ProbeSourceTests
    {
        // What animators were seen to play is kept from one session to the next with the probe's
        // own code in its stamp (ClipStore.ProbeSource), so what a changed probe would see is never
        // taken from what an older one saw. The stamp is a hash of TriggerProbe.cs, held to it here.

        [Fact]
        public void TheClipsKeptAreStampedWithTheProbeTheyWereSeenBy()
        {
            var root = ScrySource.Root();
            var probe = File.ReadAllText(Path.Combine(root, "src", "Preview", "TriggerProbe.cs")).Replace("\r\n", "\n");
            var store = File.ReadAllText(Path.Combine(root, "src", "Preview", "ClipStore.cs"));
            var stamped = Regex.Match(store, "ProbeSource = \"([0-9a-f]+)\"").Groups[1].Value;
            var now = Stored.Hash(probe);
            Assert.True(stamped == now, $"TriggerProbe.cs changed: set ClipStore.ProbeSource to \"{now}\", so clips kept by the probe before are seen again");
        }
    }
}
