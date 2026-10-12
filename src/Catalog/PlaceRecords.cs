using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SoftReferenceableAssets;

namespace Scry
{
    /// <summary>
    /// What the places were read to hold, kept from one session to the next in <c>places.dat</c>
    /// under Scry's cache folder (<see cref="KeptPlaces"/>), with the stamp each of the world's
    /// places has now (<see cref="PlaceStamps"/>): both found on a worker thread on entering a
    /// world, and the file written on one once places are read (<see cref="FileQueue"/>), aside
    /// and then swapped into place (<see cref="FileSwap"/>). Every place read is kept, this
    /// world's and others', so a world with other places leaves theirs be.
    /// </summary>
    internal static class PlaceRecords
    {
        static PlaceRecords() => WorldCaches.Register(nameof(PlaceRecords), Forget);

        private const string FileName = "places.dat";

        private static readonly FileQueue Files = new FileQueue();
        private static KeptPlaces _kept = new KeptPlaces();
        private static Task<(KeptPlaces Kept, string[] Stamps)> _finding;

        /// <summary>The world's places, in the order their stamps are found.</summary>
        private static readonly List<(string Prefab, bool Room)> World = new List<(string, bool)>();

        /// <summary>The stamp of each of the world's places, once found.</summary>
        private static readonly Dictionary<(string Prefab, bool Room), string> Stamps = new Dictionary<(string, bool), string>();

        private static string FilePath => Path.Combine(Settings.CacheFolder, FileName);

        /// <summary>Whether what was kept is still being found.</summary>
        public static bool Finding => _finding != null;

        /// <summary>Whether the file is still being found or written.</summary>
        public static bool Busy => Files.Busy;

        /// <summary>Every place kept, from the cache folder and the readings since.</summary>
        public static KeptPlaces Kept => _kept;

        /// <summary>Forgets what is held, on leaving a world; what is being written is written still.</summary>
        public static void Forget()
        {
            _finding = null;
            _kept = new KeptPlaces();
            World.Clear();
            Stamps.Clear();
        }

        /// <summary>
        /// Starts finding what was kept, and the stamp of each of the world's places, by its prefab,
        /// whether a room and the asset the game loads it from, on entering a world (<see cref="Take"/>).
        /// </summary>
        public static void Begin(IReadOnlyList<(string Prefab, bool Room, AssetID Asset)> places)
        {
            Forget();
            World.AddRange(places.Select(p => (p.Prefab, p.Room)));
            var path = FilePath;
            var manifest = Path.Combine(SoftReferenceableAssets.Shared.GetBuildDirectory(), "manifest");
            var ids = places.Select(p => p.Asset).ToArray();
            var build = typeof(PlaceRecords).Assembly.ManifestModule.ModuleVersionId.ToString("N");
            var mods = BepInEx.Bootstrap.Chainloader.PluginInfos.Values
                .Where(i => i?.Metadata != null).Select(i => (i.Metadata.GUID ?? "", i.Metadata.Version?.ToString() ?? "")).ToList();
            _finding = Files.Then(() => (Read(path), StampsOf(manifest, ids, build, mods)));
        }

        /// <summary>
        /// Takes up what was found, once it is, and gives each of the world's places' stamps in the
        /// order given to <see cref="Begin"/>; null while it is still being found, or nothing is.
        /// One that could not be found is told, and leaves nothing kept and no stamp known.
        /// </summary>
        public static string[] Take()
        {
            if (_finding == null || !_finding.IsCompleted) return null;
            var finding = _finding;
            _finding = null;
            var stamps = new string[World.Count];
            if (finding.Status == TaskStatus.RanToCompletion)
            {
                _kept = finding.Result.Kept;
                stamps = finding.Result.Stamps;
            }
            else
            {
                Faults.Tell(Feature.PlacesKept, "finding the places kept", finding.Exception?.GetBaseException());
                _kept = new KeptPlaces();
                for (var i = 0; i < stamps.Length; i++) stamps[i] = "";
            }
            for (var i = 0; i < World.Count; i++) Stamps[World[i]] = stamps[i];
            return stamps;
        }

        /// <summary>A place's stamp (<see cref="PlaceStamps"/>), for what else is kept of it; empty while not known.</summary>
        public static string StampOf(string prefab, bool room) => Stamps.TryGetValue((prefab, room), out var stamp) ? stamp : "";

        /// <summary>Keeps a place read, in the place of what was kept of it.</summary>
        public static void Put(PlaceRecord record) => _kept.Put(record);

        /// <summary>Writes every place kept to the file, as they are now, on a worker thread after anything still being found or written.</summary>
        public static void Write()
        {
            var kept = _kept.Copy();
            var path = FilePath;
            Files.Then("saving the places read", () => FileSwap.Write(path, aside => File.WriteAllText(aside, kept.Save())));
        }

        /// <summary>Tells any writing that failed; called each frame.</summary>
        public static void Update()
        {
            foreach (var (doing, error) in Files.Failures()) Faults.Tell(Feature.PlacesKept, doing, error);
        }

        /// <summary>What was kept in the cache folder; nothing for a file of another kind or none. On a worker thread.</summary>
        private static KeptPlaces Read(string path)
        {
            var file = FileSwap.ToRead(path);
            return KeptPlaces.Load(file != null ? File.ReadAllText(file) : null);
        }

        /// <summary>
        /// Each place's stamp: the bundle the game's own list of them names for it (its <c>SoftRef</c>
        /// manifest, read as <c>AssetBundleManifest.DeserializeFromDisk</c> reads it), by its file's
        /// size and when it was written, or "mod" for one the list does not have, which a mod adds;
        /// none where the list or the file cannot be read. On a worker thread.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string[] StampsOf(string manifestPath, AssetID[] ids, string build, List<(string, string)> mods)
        {
            var stamps = new string[ids.Length];
            var manifest = File.Exists(manifestPath) ? SoftReferenceableAssets.AssetBundleManifest.DeserializeFromDisk(manifestPath) : null;
            var folder = manifest?.GetAbsoluteBundlesDirectory();
            for (var i = 0; i < ids.Length; i++)
            {
                var source = "";
                if (manifest != null)
                {
                    if (!manifest.m_assetToLocationMap.TryGetValue(ids[i], out var at)) source = "mod";
                    else
                    {
                        var file = new FileInfo(Path.Combine(folder, at.m_bundleName));
                        if (file.Exists) source = PlaceStamps.Bundle(at.m_bundleName, file.Length, file.LastWriteTimeUtc.Ticks);
                    }
                }
                stamps[i] = PlaceStamps.Of(build, mods, source);
            }
            return stamps;
        }
    }
}
