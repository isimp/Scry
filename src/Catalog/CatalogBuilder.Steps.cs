using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The reading of the catalog as its steps, in order: the startup check, the prefabs one at a
    /// time, where things live and which mod added them, the status effects and raids, the
    /// interface's sounds, the effects the prefabs play and what their helpers play, the entries,
    /// what things leave behind, the links, the groups, the places, biomes and mods, and the mods'
    /// icons. Each yields what is read next, where the job may stop for the frame.
    /// </summary>
    internal static partial class CatalogBuilder
    {
        /// <summary>What the steps read into and hand on.</summary>
        private sealed class Reading
        {
            public readonly CatalogJob Job;
            public readonly Dictionary<string, Found> Registered = new Dictionary<string, Found>(StringComparer.Ordinal);
            public readonly Dictionary<string, Found> Effects = new Dictionary<string, Found>(StringComparer.Ordinal);
            public readonly List<Entry> Entries = new List<Entry>();
            public readonly List<Component> Components = new List<Component>();
            public readonly List<Leftover> Leftovers = new List<Leftover>();
            public List<GameObject> Prefabs = new List<GameObject>();

            public Reading(CatalogJob job) => Job = job;
        }

        /// <summary>
        /// The reading of the catalog, a piece at a time, each piece followed by what is being read
        /// next. Nothing is shown until the end: an entry's kind, its users, its origin, what it
        /// leaves behind and its links are only known once everything is read. Each prefab is
        /// looked through once, and what it tells is read from that for its own details, where it
        /// comes from and its links; one that cannot be read loses only its own part.
        /// </summary>
        internal static IEnumerator<string> Steps(CatalogJob job)
        {
            CatalogTiming.Reset();
            var collections = GC.CollectionCount(0);
            var read = new Reading(job);

            Timed("startup check", () => Guard.Run("the startup check", Compatibility.Check));
            yield return "Reading the prefabs";

            Timed("setup", () => Register(read));
            yield return "Reading recipes";

            Timed("setup", () =>
            {
                Knowledge.Begin();
                Relations.Begin(read.Registered.Keys);
                IndexRecipes();
            });
            foreach (var step in ReadPrefabs(read)) yield return step;

            // Where things live and which mod added them, before any entry is made.
            foreach (var step in Knowledge.Finish(read.Prefabs)) yield return "Reading " + step;

            Timed("status effects", () => StatusEffects(read));
            Timed("raids", () => Raids(read.Entries));
            yield return "Reading the interface's sounds";

            Timed("interface", () => GatherInterface(read.Effects));
            yield return "Reading effects";

            foreach (var step in WalkEffects(read)) yield return step;
            foreach (var step in MakeEntries(read)) yield return step;
            yield return "Pairing leftovers";

            Timed("leftovers", () => Guard.Run("pairing what things leave behind", () => Leftovers.Pair(read.Entries, read.Leftovers)));
            yield return "Linking entries";

            LinkBook book = null;
            Timed("links finished", () => Guard.Run("linking entries", () => book = Relations.Finish(read.Prefabs)));
            yield return "Linking entries";

            var linking = book == null ? null : Stepped(() => book.ApplyInSteps(read.Entries, 400), "links applied", "linking entries");
            if (linking != null) foreach (var done in linking) yield return $"Linking entries: {Numbers.Count(done)}";

            yield return "Grouping entries";
            foreach (var done in Stepped(() => Grouping.Apply(read.Entries, 400), "grouping", "grouping entries"))
            {
                yield return $"Grouping entries: {Numbers.Count(done)}";
            }

            // Locations and rooms take their names from creatures' (a Fuling camp), so they come
            // last but for the mods, which come once every entry knows the mod that added it.
            Timed("locations", () => Guard.Run("the locations and dungeon rooms", PlaceEntries.Add, read.Entries));
            Timed("biomes", () => Guard.Run("the biomes", Biomes, read.Entries));
            Timed("mods", () => Guard.Run("the mods' own entries", Mods, read.Entries));
            foreach (var step in ModIcons(read)) yield return step;

            job.Entries = read.Entries;
            Log.Note($"Scry's catalog, by part (ms): {CatalogTiming.Report()}; {Numbers.Count(GC.CollectionCount(0) - collections)} garbage collections meanwhile.");
            Faults.TellSkipped();
        }

        /// <summary>The scene's prefabs, each once by its name (<see cref="FirstOfName"/>), networked and not.</summary>
        private static void Register(Reading read)
        {
            var scene = ZNetScene.instance;
            EffectLinks.Clear();
            Localized.Clear();
            foreach (var prefab in FirstOfName.Each(scene.m_prefabs.Concat(scene.m_nonNetViewPrefabs), p => p != null ? p.name : null))
            {
                read.Registered[prefab.name] = new Found { Prefab = prefab };
            }
            read.Prefabs = read.Registered.Values.Select(f => f.Prefab).ToList();
        }

        /// <summary>A prefab at a time: some are large, and a few read together could take a frame's share.</summary>
        private static IEnumerable<string> ReadPrefabs(Reading read)
        {
            yield return $"Reading prefabs: 0 of {Numbers.Count(read.Registered.Count)}";
            var done = 0;
            var progress = "";
            foreach (var pair in read.Registered)
            {
                read.Job.Piece = pair.Key;
                ReadPrefab(pair.Key, pair.Value, read.Effects, read.Components, read.Leftovers);
                if (++done % 16 == 1) progress = $"Reading prefabs: {Numbers.Count(done)} of {Numbers.Count(read.Registered.Count)}";
                yield return progress;
            }
            read.Components.Clear();
        }

        /// <summary>
        /// Every status effect the object database has, each an entry of its own with what it
        /// plays; one listed twice once, the first, the one the game finds (<see cref="FirstOfName"/>).
        /// </summary>
        private static void StatusEffects(Reading read)
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            foreach (var effect in FirstOfName.Each(db.m_StatusEffects, e => e != null ? e.name : null))
            {
                Guard.Each("status effect entries", effect.name, () =>
                {
                    var origin = Origins.StatusEffects.Of(effect.name);
                    var shown = Localize(effect.m_name);
                    Gather(effect, "status effect " + effect.name, origin, read.Effects, "se:" + effect.name, shown.Length > 0 ? shown : effect.name);
                    Relations.ReadStatusEffect(effect);

                    read.Entries.Add(new Entry
                    {
                        Name = effect.name,
                        DisplayName = shown,
                        Kind = Kind.StatusEffect,
                        Origin = origin,
                        Source = effect,
                        Icon = effect.m_icon,
                        Components = new[] { effect.GetType().Name },
                        ModName = Knowledge.ModName(effect.name),
                        ModClue = Knowledge.ModClue(effect.name),
                    });
                });
            }
        }

        /// <summary>
        /// The effects the prefabs play. Effects can point at further effects (a hit effect with
        /// an area of its own), so the walk continues until nothing new turns up. Then what a
        /// prefab's helpers play is read as the prefab's (the spawn effects of what a staff's
        /// projectile leaves to raise its summon, the snow a shovel moves), and what that adds is
        /// walked in turn. A helper that turned out to be an effect of its own is read only as that.
        /// </summary>
        private static IEnumerable<string> WalkEffects(Reading read)
        {
            var described = new HashSet<string>(StringComparer.Ordinal);
            var walked = 0;
            var helpersRead = false;
            bool grew;
            do
            {
                grew = false;
                foreach (var found in new List<Found>(read.Effects.Values))
                {
                    var name = found.Prefab.name;
                    if (read.Registered.ContainsKey(name) || !described.Add(name)) continue;
                    var started = CatalogTiming.Start();
                    read.Components.Clear();
                    Describe(found, name, Provenance.Combine(found.UserOrigins), read.Effects, read.Components);
                    CatalogTiming.Add("describe effects", started);
                    grew = true;
                    if (++walked % 4 == 0) yield return $"Reading effects: {Numbers.Count(walked)}";
                }

                if (!grew && !helpersRead)
                {
                    helpersRead = true;
                    var helpers = new List<Relations.Helper>();
                    Relations.TakeHelpers(helpers);
                    for (var i = 0; i < helpers.Count; i++)
                    {
                        var started = CatalogTiming.Start();
                        GatherHelper(helpers[i], read.Registered, read.Effects, read.Components);
                        CatalogTiming.Add("helpers", started);
                        if (i % 8 == 7) yield return $"Reading what prefabs spawn: {Numbers.Count(i + 1)} of {Numbers.Count(helpers.Count)}";
                    }
                    grew = true;
                }
            }
            while (grew);
            read.Components.Clear();
        }

        /// <summary>An entry for every registered prefab, then for every effect reached that is not one.</summary>
        private static IEnumerable<string> MakeEntries(Reading read)
        {
            var made = 0;
            var total = read.Registered.Count + read.Effects.Count;
            foreach (var pair in read.Registered)
            {
                MakeEntry(read.Entries, pair.Key, pair.Value, read.Effects, true);
                if (++made % 16 == 0) yield return $"Making entries: {Numbers.Count(made)} of about {Numbers.Count(total)}";
            }
            foreach (var pair in read.Effects)
            {
                if (read.Registered.ContainsKey(pair.Key)) continue;
                MakeEntry(read.Entries, pair.Key, pair.Value, read.Effects, false);
                if (++made % 16 == 0) yield return $"Making entries: {Numbers.Count(made)} of about {Numbers.Count(total)}";
            }
        }

        /// <summary>The mods' icons, a few at a time, as each is a picture to decode; once read, an icon is kept for the next world's catalog.</summary>
        private static IEnumerable<string> ModIcons(Reading read)
        {
            var icons = 0;
            foreach (var entry in read.Entries)
            {
                if (entry.Kind != Kind.Mod || !(entry.Source is ModSource mod) || mod.IconPath.Length == 0) continue;
                var started = CatalogTiming.Start();
                entry.Icon = ModFolders.Icon(mod.IconPath);
                CatalogTiming.Add("mods' icons", started);
                if (++icons % 4 == 0) yield return "Reading the mods' icons";
            }
        }

        /// <summary>A step timed as a part of the catalog's reading.</summary>
        private static void Timed(string part, Action step)
        {
            var started = CatalogTiming.Start();
            step();
            CatalogTiming.Add(part, started);
        }

        /// <summary>
        /// A step done a piece at a time, each piece timed as a part of the catalog's reading,
        /// handing on how far it has got; failing to begin or part way, it stops, told as what it was doing.
        /// </summary>
        private static IEnumerable<int> Stepped(Func<IEnumerable<int>> begin, string part, string doing)
        {
            var started = CatalogTiming.Start();
            IEnumerator<int> steps = null;
            Guard.Run(doing, () => steps = begin().GetEnumerator());
            CatalogTiming.Add(part, started);
            while (steps != null)
            {
                started = CatalogTiming.Start();
                var more = false;
                Guard.Run(doing, () => more = steps.MoveNext());
                CatalogTiming.Add(part, started);
                if (!more) yield break;
                yield return steps.Current;
            }
        }
    }
}
