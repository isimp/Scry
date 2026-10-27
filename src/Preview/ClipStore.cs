using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scry
{
    /// <summary>
    /// What hidden copies of animators were seen to play (<see cref="TriggerProbe"/>), kept in the
    /// cache folder from one session to the next (<see cref="KeptClips"/>), so a page's clips are
    /// known at once the next time. Each probe is kept with the stamp of what it was seen with: the
    /// probe's own code, the game's version, the mods loaded and the animator, so any of them
    /// changing has it seen again. Read on a worker thread once first asked, written there as each
    /// probe ends, and forgotten by <c>/scry forget clips</c> or the Animations section's button.
    /// </summary>
    internal static class ClipStore
    {
        private const string FileName = "clips.dat";

        /// <summary>
        /// The probe's own code, by a hash of <c>TriggerProbe.cs</c>: what it sees changes with it.
        /// A unit test holds it to the file and tells the new value when the file changes.
        /// </summary>
        public const string ProbeSource = "1cb1226444e36a58";

        private static readonly FileQueue Files = new FileQueue();
        private static KeptClips _kept;
        private static Task<KeptClips> _reading;
        private static string _stamp;

        private static string FilePath => Path.Combine(Settings.CacheFolder, FileName);

        /// <summary>Whether what was kept has been read; asked first, it starts being read.</summary>
        public static bool Ready
        {
            get
            {
                if (_kept != null) return true;
                if (_reading == null)
                {
                    var path = FilePath;
                    _reading = Files.Then(() =>
                    {
                        var file = FileSwap.ToRead(path);
                        return KeptClips.Load(file != null ? File.ReadAllText(file) : null);
                    });
                }
                if (!_reading.IsCompleted) return false;
                if (_reading.Status == TaskStatus.RanToCompletion) _kept = _reading.Result;
                else
                {
                    Faults.Tell(Feature.Animations, "reading the clips kept", _reading.Exception?.GetBaseException());
                    _kept = new KeptClips();
                }
                _reading = null;
                return true;
            }
        }

        /// <summary>Waits for what was kept to be read, for an answer wanted at once.</summary>
        public static void WaitReady()
        {
            if (Ready) return;
            // A reading that failed is told by Ready, not thrown here.
            ((System.IAsyncResult)_reading)?.AsyncWaitHandle.WaitOne();
            _ = Ready;
        }

        /// <summary>What everything kept now was seen with but the animator: the probe's code, the game's version and the mods loaded, each by its id and version.</summary>
        private static string Stamp
        {
            get
            {
                if (_stamp != null) return _stamp;
                var mods = BepInEx.Bootstrap.Chainloader.PluginInfos.Values
                    .Where(i => i?.Metadata != null).Select(i => (i.Metadata.GUID ?? "", i.Metadata.Version?.ToString() ?? "")).ToList();
                _stamp = PlaceStamps.Of(ProbeSource, mods, global::Version.GetVersionString());
                return _stamp;
            }
        }

        /// <summary>What a probe of this key saw, kept with this animator and everything else as now; false where none is.</summary>
        public static bool TryTake(string key, string controller, out Probed seen)
        {
            seen = null;
            return Ready && _kept.TryGet(key, KeptClips.StampWith(Stamp, controller), out seen);
        }

        /// <summary>Keeps what a probe saw as it is now, and writes everything kept on a worker thread.</summary>
        public static void Keep(string key, string controller, Probed seen)
        {
            if (!Ready) return;
            _kept.Put(key, KeptClips.StampWith(Stamp, controller), seen);
            var copy = _kept.CopyAll();
            var path = FilePath;
            Files.Then("saving the clips seen", () => FileSwap.Write(path, aside => File.WriteAllText(aside, copy.Save())));
        }

        /// <summary>Forgets every probe kept and removes the file; how many there were.</summary>
        public static int Forget()
        {
            var count = Ready ? _kept.Count : 0;
            _kept = new KeptClips();
            var path = FilePath;
            Files.Then("forgetting the clips kept", () => FileSwap.Remove(path));
            return count;
        }

        /// <summary>How many probes are kept; for the self-test.</summary>
        [ForTest]
        public static int Count => _kept?.Count ?? 0;

        /// <summary>Whether a probe of this key is kept, whatever it was seen with; for the self-test.</summary>
        [ForTest]
        public static bool Holds(string key) => _kept != null && _kept.Keys.Contains(key);

        /// <summary>Tells any reading or writing that failed; called each frame.</summary>
        public static void Update() => Faults.TellEach(Feature.Animations, Files.Failures());
    }
}
