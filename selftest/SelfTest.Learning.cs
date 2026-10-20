using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for the ways a player meets things, as the strict rules then show them, and for the hooks they come through.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Each way of meeting a place, a rune stone and a station's upgrades, fed through the
        /// keeper's own entry points the hooks call, into a book of the test's own: what it makes
        /// known, and what the strict rules then show of how the things go together. Standing in a
        /// place pairs it with the biome there; at a dungeon's entrance it is seen, inside known; a
        /// rune stone read keeps its text as read whatever its label; a station in use, an upgrade
        /// built near it and a station built near its upgrades pair the station with each. The
        /// character's book is left alone. Rows needing a stone or a station with upgrades loaded
        /// near by are left out where there is none.
        /// </summary>
        private static IEnumerator LearningPathsTaught(Probe p)
        {
            var player = Player.m_localPlayer;
            var real = CompendiumKeeper.Book;
            if (!p.Check(player != null && real != null, "a character is played and its book read")) yield break;
            var version = real.Version;
            var pairs = real.PairsVersion;
            var at = player.transform.position;
            var biome = X.Find(EntryKeys.For(Kind.Biome, Heightmap.FindBiome(at).ToString()));
            CompendiumBook Fresh()
            {
                var book = new CompendiumBook();
                CompendiumKeeper.RecordInto(book);
                return book;
            }
            bool Shows(CompendiumBook book, Entry a, Entry b)
            {
                var view = new CompendiumView(X.Entries);
                view.Tell(book, CompendiumRule.Strict);
                return view.Related(a, b);
            }

            // A place stood in, known and paired with the biome there.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && e.Source is PlaceSource source && !source.IsRoom && source.Contents?.Dungeon == null);
            if (place != null && biome != null)
            {
                var book = Fresh();
                CompendiumKeeper.StoodIn(place.Key, hasInside: false, inside: false, at);
                p.Check(book.Level(place.Key) == KnownLevel.Known && Shows(book, place, biome), $"standing in {place.Name} makes it known, its biome shown under the strict rules", $"{book.Level(place.Key)}, paired {book.Witnessed(place.Key, biome.Key)}");
            }
            else p.Note("no place or no biome where the character stands: standing in a place untried");

            // A dungeon seen at its entrance, known once inside, paired with the biome either way.
            var crypt = Crypt();
            if (crypt != null && biome != null)
            {
                var book = Fresh();
                CompendiumKeeper.StoodIn(crypt.Key, hasInside: true, inside: false, at);
                var outside = book.Level(crypt.Key);
                CompendiumKeeper.StoodIn(crypt.Key, hasInside: true, inside: true, at);
                p.Check(outside == KnownLevel.Seen && book.Level(crypt.Key) == KnownLevel.Known && Shows(book, crypt, biome), $"at {crypt.Name}'s entrance it is seen, inside known, its biome shown under the strict rules", $"{outside}, then {book.Level(crypt.Key)}");
            }

            // A rune stone read: the text it gave kept as read, whatever its label.
            var stone = Object.FindObjectsByType<RuneStone>(FindObjectsSortMode.None).OrderBy(s => (s.transform.position - at).sqrMagnitude).FirstOrDefault();
            if (stone == null) stone = ZNetScene.instance.m_prefabs.Where(o => o != null).Select(o => o.GetComponentInChildren<RuneStone>(true)).FirstOrDefault(s => s != null);
            if (stone != null)
            {
                var book = Fresh();
                CompendiumKeeper.ReadRunes(stone);
                var texts = (stone.m_randomTexts ?? new List<RuneStone.RandomRuneText>()).Select(t => t.m_text).Append(stone.m_text).Where(t => !string.IsNullOrEmpty(t)).ToList();
                var view = stone.GetComponentInParent<ZNetView>();
                p.Check(texts.Any(book.HasRead) && (view == null || book.Level(Utils.GetPrefabName(view.gameObject)) == KnownLevel.Known),
                    $"a rune stone read ({stone.name}) keeps the text it gave as read, whatever the game keeps of it, the stone known", string.Join(", ", texts.Take(3)));
            }
            else p.Note("no rune stone loaded or among the prefabs: reading one untried");

            // A station and its upgrades: in use, an upgrade built near it, it built near them.
            // Each station loaded with its own upgrades, the nearest with any taken.
            var stations = new List<(CraftingStation Station, List<StationExtension> Upgrades)>();
            foreach (var each in Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None))
            {
                if (each == null) continue;
                var found = new List<StationExtension>();
                StationExtension.FindExtensions(each, each.transform.position, found);
                if (found.Count > 0) stations.Add((each, found));
            }
            var (station, near) = stations.OrderBy(t => (t.Station.transform.position - at).sqrMagnitude).FirstOrDefault();
            if (station != null && near != null && near.Count > 0)
            {
                var stationEntry = X.Find(Utils.GetPrefabName(station.gameObject));
                var upgrade = near[0];
                var upgradeEntry = X.Find(Utils.GetPrefabName(upgrade.gameObject));
                p.Note($"{stationEntry?.Name ?? station.name} with {Numbers.Count(near.Count)} upgrades near it");
                if (p.Check(stationEntry != null && upgradeEntry != null, "the station and its upgrade are in the catalog"))
                {
                    var used = Fresh();
                    CompendiumKeeper.UsedStation(station);
                    p.Check(near.All(e => used.Witnessed(stationEntry.Key, Utils.GetPrefabName(e.gameObject))) && Shows(used, stationEntry, upgradeEntry),
                        $"{stationEntry.Name} in use is paired with each upgrade near it, shown under the strict rules", string.Join(", ", near.Select(e => Utils.GetPrefabName(e.gameObject))));
                    var built = Fresh();
                    var upgradePiece = GamePrefabs.Named(upgradeEntry.Key).OrNull()?.GetComponent<Piece>();
                    if (upgradePiece != null) CompendiumKeeper.Placed(upgradePiece, upgrade.transform.position, null);
                    p.Check(upgradePiece != null && built.Level(upgradeEntry.Key) == KnownLevel.Known && built.Witnessed(stationEntry.Key, upgradeEntry.Key),
                        $"{upgradeEntry.Name} built near {stationEntry.Name} is known and paired with it", $"{built.Level(upgradeEntry.Key)}");
                    var placed = Fresh();
                    var stationPiece = GamePrefabs.Named(stationEntry.Key).OrNull()?.GetComponent<Piece>();
                    if (stationPiece != null) CompendiumKeeper.Placed(stationPiece, station.transform.position, null);
                    p.Check(stationPiece != null && placed.Witnessed(stationEntry.Key, upgradeEntry.Key), $"{stationEntry.Name} built near its upgrades is paired with them");
                }
            }
            else p.Note("no crafting station with upgrades loaded near by: its rows untried");

            CompendiumKeeper.RecordInto(null);
            p.Check(real.Version == version && real.PairsVersion == pairs, "the character's book was left alone");
        }

        /// <summary>
        /// Every hook of Scry's acting on a game method's answer can see that answer come true: none
        /// hooks a method whose code answers only false (<see cref="IlShape.OnlyFalse"/>), as hooks on
        /// RuneStone.Interact and MapTable.OnRead once did, which never acted. The hooks are told,
        /// and how those two read now.
        /// </summary>
        private static IEnumerator HookGatesCanOpen(Probe p)
        {
            var looked = new List<string>();
            var dead = new List<string>();
            foreach (var original in Harmony.GetAllPatchedMethods())
            {
                if (!(original is MethodInfo method) || method.ReturnType != typeof(bool)) continue;
                var info = Harmony.GetPatchInfo(original);
                if (info == null) continue;
                foreach (var patch in info.Postfixes)
                {
                    if (patch.owner != About.Guid || !patch.PatchMethod.GetParameters().Any(q => q.Name == "__result")) continue;
                    var name = $"{method.DeclaringType?.Name}.{method.Name}";
                    looked.Add(name);
                    if (IlShape.OnlyFalse(method.GetMethodBody()?.GetILAsByteArray())) dead.Add(name);
                }
            }
            p.Note("hooks acting on the game's answer: " + string.Join(", ", looked));
            var interact = AccessTools.Method(typeof(RuneStone), nameof(RuneStone.Interact));
            var read = AccessTools.Method(typeof(MapTable), "OnRead", new[] { typeof(Switch), typeof(Humanoid), typeof(ItemDrop.ItemData), typeof(bool) });
            foreach (var (name, game) in new[] { ("RuneStone.Interact", interact), ("MapTable.OnRead", read) })
            {
                p.Note(IlShape.OnlyFalse(game?.GetMethodBody()?.GetILAsByteArray())
                    ? name + " is read as answering only false: a hook acting on its answer would never act"
                    : name + " can answer true now");
            }
            p.Check(looked.Count > 0 && dead.Count == 0, "every hook acting on a game method's answer can see it come true", string.Join(", ", dead));
            yield break;
        }
    }
}
