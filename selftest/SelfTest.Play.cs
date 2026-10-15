using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for what play records and how a page under the strict rules reads:
    /// made-up events fed through the real hooks into a book of the test's own, the world and the
    /// character's book left alone; and a made-up character's pages read as a player sees them.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Made-up events through the real hooks, recorded into a book of the test's own
        /// (<see cref="CompendiumKeeper.RecordInto"/>): a hit of the character's on a loaded
        /// creature, on its weak spot, an armed creature's hit taken, a birth, a regrowth and a
        /// taming near by, a tree breaking with an item come to be, a map read. Nothing in the
        /// world changes, and neither the character's book nor the play counts do.
        /// </summary>
        private static IEnumerator CompendiumHooksFed(Probe p)
        {
            var player = Player.m_localPlayer;
            var real = CompendiumKeeper.Book;
            if (!p.Check(player != null && real != null, "a character is played and its book read")) yield break;
            p.Note("what play recorded " + CompendiumKeeper.Play.Told());
            var version = real.Version;
            var pairs = real.PairsVersion;
            var learned = real.Learned;
            var counted = CompendiumKeeper.Play.Told();
            var book = new CompendiumBook();
            CompendiumKeeper.RecordInto(book);

            // A hit of the character's on the nearest loaded creature: how its damage landed, then its weak spot.
            var creature = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None).Where(c => c != null && !c.IsPlayer())
                .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (creature == null) p.Note("no creature is loaded to hit");
            else
            {
                var key = Utils.GetPrefabName(creature.gameObject);
                var hit = new HitData();
                hit.m_damage.m_blunt = 10f;
                hit.m_damage.m_fire = 10f;
                hit.SetAttacker(player);
                TogetherWatch.Hit(creature, hit);
                p.Check(ResistWords.Types.Any(t => book.Witnessed(key, EntryKeys.Damage(t))), $"a hit on {key} tells how its damage landed");
                var spots = creature.m_weakSpots;
                if (spots != null && spots.Length > 0 && spots[0] != null)
                {
                    hit.m_weakSpot = 0;
                    TogetherWatch.Hit(creature, hit);
                    p.Check(book.Witnessed(key, EntryKeys.WeakSpot(spots[0].gameObject.name)), $"a hit on its weak spot finds it ({spots[0].gameObject.name})");
                }
                else p.Note($"{key} has no weak spot to hit");
            }

            // An armed creature's hit taken: its attack's damage found out.
            var armed = UnityEngine.Object.FindObjectsByType<Humanoid>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && !h.IsPlayer() && h.GetCurrentWeapon()?.m_dropPrefab != null);
            if (armed == null) p.Note("no armed creature is loaded to be hit by");
            else
            {
                CompendiumKeeper.HitBy(armed);
                var weapon = armed.GetCurrentWeapon().m_dropPrefab.name;
                p.Check(book.FoundOutOf(Utils.GetPrefabName(armed.gameObject)) == weapon, $"a hit taken from {Utils.GetPrefabName(armed.gameObject)} finds out its attack ({weapon})");
            }

            // Deeds near by: a birth (one not pregnant now), a regrowth, a taming.
            var near = Sighting.PersonReach;
            var parent = UnityEngine.Object.FindObjectsByType<Procreation>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c != null && Vector3.Distance(c.transform.position, player.transform.position) <= near && !DeedWatch.Pregnant(c));
            if (parent == null) p.Note("no tame creature that breeds is near to see born");
            else
            {
                DeedWatch.Procreated(parent, wasPregnant: true);
                p.Check(book.Witnessed(Utils.GetPrefabName(parent.gameObject), EntryKeys.Did(Deed.Born)), "a birth seen near by shows how long it is pregnant");
            }
            var bush = UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b != null && Vector3.Distance(b.transform.position, player.transform.position) <= near);
            if (bush == null) p.Note("no pickable is near to see grow back");
            else
            {
                DeedWatch.SetPicked(bush, wasPicked: true, picked: false);
                p.Check(book.Witnessed(Utils.GetPrefabName(bush.gameObject), EntryKeys.Did(Deed.GrewBack)), "a pickable seen growing back shows how long that takes");
            }
            // A taming: one creature near by told wild, then tame; every other as the game has it.
            var tame = UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None)
                .Select(t => t != null ? t.GetComponent<Character>() : null)
                .FirstOrDefault(c => c != null && Vector3.Distance(c.transform.position, player.transform.position) <= near);
            if (tame == null) p.Note("no tameable creature is near to tame");
            else
            {
                CompendiumKeeper.Tamings(player, c => c != tame && c.IsTamed());
                CompendiumKeeper.Tamings(player, c => c == tame || c.IsTamed());
                p.Check(book.Witnessed(Utils.GetPrefabName(tame.gameObject), EntryKeys.Did(Deed.Tamed)), "a creature near by seen wild, then tame, shows how long taming takes");
            }
            p.Note("growing up is seen in play only, as a young creature has to be gone for it");

            // A tree breaking with an item come to be meanwhile: counted as what it gave.
            var tree = Nearest<TreeBase>(player);
            var item = Nearest<ItemDrop>(player);
            if (tree == null || item == null) p.Note("no tree and item are loaded to count a break with");
            else
            {
                YieldWatch.Begin(tree);
                YieldWatch.ItemMade(item);
                YieldWatch.End();
                var treeKey = Utils.GetPrefabName(tree.gameObject);
                p.Check(book.Tallied(Tally.Gave).Kills(treeKey) == 1 && book.Witnessed(treeKey, Utils.GetPrefabName(item.gameObject)), $"{treeKey} breaking is counted with what came of it");
            }

            // A pick with an item come to be: counted as what it gave, no break.
            var picked = Nearest<Pickable>(player);
            if (picked == null || item == null) p.Note("no pickable and item are loaded to count a pick with");
            else
            {
                YieldWatch.Begin(picked);
                YieldWatch.ItemMade(item);
                YieldWatch.End();
                var pickKey = Utils.GetPrefabName(picked.gameObject);
                p.Check(book.Tallied(Tally.Gave).Kills(pickKey) == 1 && !book.Witnessed(pickKey, EntryKeys.Did(Deed.Broke)), $"picking {pickKey} is counted as what it gave, and is no break");
            }

            // A chest the world fills, opened: counted the first time only.
            var chest = UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None)
                .Where(c => c != null && c.m_defaultItems?.m_drops != null && c.m_defaultItems.m_drops.Count > 0 && c.GetInventory() != null)
                .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (chest == null) p.Note("no chest the world fills is loaded to open");
            else
            {
                CompendiumKeeper.Opened(chest);
                CompendiumKeeper.Opened(chest);
                p.Check(book.Tallied(Tally.Held).Kills(Utils.GetPrefabName(chest.gameObject)) == 1, $"{Utils.GetPrefabName(chest.gameObject)} opened is counted the first time only");
            }

            // A spawner spawning near by, a hive collected from.
            var spawner = UnityEngine.Object.FindObjectsByType<SpawnArea>(FindObjectsSortMode.None)
                .FirstOrDefault(a => a != null && Vector3.Distance(a.transform.position, player.transform.position) <= near);
            if (spawner == null) p.Note("no spawner is near to see spawn");
            else
            {
                DeedWatch.Spawned(spawner, spawned: true);
                p.Check(book.Witnessed(Utils.GetPrefabName(spawner.gameObject), EntryKeys.Did(Deed.Spawned)), "a spawn seen near by shows its pool and pace");
            }
            var hive = Nearest<Beehive>(player);
            if (hive == null) p.Note("no beehive is loaded to collect from");
            else
            {
                DeedWatch.Collected(hive, player, held: true);
                p.Check(book.Witnessed(Utils.GetPrefabName(hive.gameObject), EntryKeys.Did(Deed.Collected)), "collecting from a beehive shows its pace");
            }

            // A map read: what it brings is news noted as off the map.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && Places.LabelOf(e) != null);
            if (place != null)
            {
                MapCatchUp.Apply(book, "self-test", new[] { place.Name });
                p.Check(book.FoundOutOf(place.Key) == EntryKeys.OnMap, $"a map read brings {place.Name} as news off the map");
            }

            CompendiumKeeper.RecordInto(null);
            p.Check(real.Version == version && real.PairsVersion == pairs && real.Learned == learned, "the character's book was left alone");
            p.Check(CompendiumKeeper.Play.Told() == counted, "and so were the play counts");
            yield return null;
        }

        /// <summary>
        /// A made-up character's pages read as a player sees them under the strict rules: the
        /// troll it fought keeps back its health, its attacks' damage, its weak spot and how many
        /// of its hide fall, says so faintly, and its You box tells what is still to find out;
        /// killed and counted, they show and Learned says what was found out. Its hide's page
        /// counts the troll's line, and a map read is counted in the notice.
        /// </summary>
        private static IEnumerator CompendiumStrictRead(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll");
            var hide = Pick(Kind.Item, "TrollHide");
            if (troll == null || hide == null)
            {
                p.Skip("there is no troll or troll hide");
                yield break;
            }
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            book.Raise(hide.Key, KnownLevel.Known);
            book.Witness(troll.Key, hide.Key);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            X.Select(troll);

            var facts = ScryPanel.Drawn(PanelPart.FactKeptBack);
            var still = ScryPanel.Drawn(PanelPart.StillToFindOut);
            var table = ScryPanel.Drawn(PanelPart.TableKeptBack);
            var spots = ScryPanel.Drawn(PanelPart.WeakSpotsLeftOut);
            var tally = ScryPanel.Drawn(PanelPart.TallyCounted);
            yield return Until(() => ScryPanel.Drawn(PanelPart.StillToFindOut) > still, 3);
            p.Check(X.LeavingOut && X.KeepsBack(EntryKeys.Did(Deed.Killed)) && ScryPanel.Drawn(PanelPart.FactKeptBack) > facts, "the troll's health reads ??? and says what shows it");
            p.Check(ScryPanel.Drawn(PanelPart.StillToFindOut) > still, "its You box tells what is still to find out");
            p.Check(ScryPanel.Drawn(PanelPart.TableKeptBack) > table, "its attacks keep back their damage, a faint line counting them");
            p.Check(ScryPanel.Drawn(PanelPart.WeakSpotsLeftOut) > spots, "its weak spot is left out and counted");
            p.Check(X.StrictAmount(Tally.Fell, troll.Name, hide.Name) == CompendiumWords.Hidden && ScryPanel.Drawn(PanelPart.TallyCounted) > tally,
                "how many hides it drops reads ??? until counted, a faint line saying what shows it");

            // Killed and its loot counted: its health and its hide's count show, and Learned says what was found out.
            book.FoundOut(troll.Key, EntryKeys.Did(Deed.Killed));
            book.Dropped(troll.Name, new[] { (hide.Name, 2) });
            view.Tell(book, CompendiumRule.Strict);
            yield return Until(() => !X.KeepsBack(EntryKeys.Did(Deed.Killed)), 3);
            p.Check(!X.KeepsBack(EntryKeys.Did(Deed.Killed)), "killed, its health shows");
            p.Check(X.StrictAmount(Tally.Fell, troll.Name, hide.Name) == "2 (1 of 1)", "counted, its hide shows how many fell and how often", X.StrictAmount(Tally.Fell, troll.Name, hide.Name));
            p.Check(X.FoundOutNote(troll) == "its health found out", "and Learned says what was found out", X.FoundOutNote(troll));

            // The hide's page tells the troll's line with the character's count.
            X.Select(hide);
            var line = Facts.For(hide).Where.FirstOrDefault(s => s.Record?.Giver == troll.Name);
            var counted = X.StrictSourceLine(line.Record);
            p.Check(counted == $"Dropped by {line.Record?.Shown}, 2 (1 of 1 kill)", "the hide's page counts the troll's line", counted);

            // A map read brings places as news, counted in the notice rather than named.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && Places.LabelOf(e) != null);
            if (place != null)
            {
                MapCatchUp.Apply(book, "self-test", new[] { place.Name });
                view.Tell(book, CompendiumRule.Strict);
                yield return Until(() => (ScryPanel.NewsLine ?? "").Contains("your explored map showed") || (ScryPanel.NewsLine ?? "").Contains("Your explored map showed"), 3);
                p.Check((ScryPanel.NewsLine ?? "").IndexOf("explored map showed 1 place", System.StringComparison.OrdinalIgnoreCase) >= 0, "the notice counts what the map brought", ScryPanel.NewsLine);
            }
            Session.ShowKnownInstead(null);
        }
    }
}
