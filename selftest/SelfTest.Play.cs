using System.Collections;
using System.Collections.Generic;
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
        /// <summary>A hit of the character's on the nearest loaded creature tells how its damage landed, then on its weak spot finds it; killed by it, it is counted; fought, the moment is kept.</summary>
        private static void CreatureHitTells(Probe p, CompendiumBook book, Player player, CompendiumBook real)
        {
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
                // The popup tells a hit no farther than 30 m: a creature loaded farther off tells nothing, as in play.
                var popup = TogetherWatch.PopupSeen(creature);
                if (popup) p.Check(ResistWords.Types.Any(t => book.Proved(key, EntryKeys.Damage(t))), $"a hit on {key} tells how its damage landed");
                else p.Note($"{key} stands {Numbers.Amount(Vector3.Distance(player.transform.position, creature.transform.position), 0)} m off, past the popup's reach: its hit tells nothing");
                var spots = creature.m_weakSpots;
                if (!popup) p.Note("its weak spot is not tried");
                else if (spots != null && spots.Length > 0 && spots[0] != null)
                {
                    hit.m_weakSpot = 0;
                    TogetherWatch.Hit(creature, hit);
                    p.Check(book.Proved(key, EntryKeys.WeakSpot(spots[0].gameObject.name)), $"a hit on its weak spot finds it ({spots[0].gameObject.name})");
                }
                else p.Note($"{key} has no weak spot to hit");

                // Killed by it: counted against it. Fought, the moment is kept whole, as play reads it.
                CompendiumKeeper.KilledBy(creature);
                p.Check(book.DeathsBy(key)?.Count == 1, $"a death dealt by {key} is counted against it");
                book.Now = real.Now;
                CompendiumKeeper.Fought(creature);
                var came = book.Came(key);
                p.Note($"fought {key}: " + (came == null ? "nothing kept" : $"day {Numbers.Count(came.Value.At.Day)}, {came.Value.At.Biome}, {came.Value.At.Time}, weather {came.Value.At.Weather ?? "none"}, in hand {came.Value.At.InHand ?? "nothing"}"));
                p.Check(came != null && came.Value.At.Day >= 0 && came.Value.At.Biome != null && came.Value.At.Time != DayPart.None && came.Value.At.Weather != null,
                    "coming to know it keeps the day, the biome, the time of day and the weather");
            }
        }

        /// <summary>What grows, what is picked up and what breaks open teaches as play would (<see cref="PlantGrowthTells"/>, <see cref="TreasurePickTells"/>, <see cref="BreakingOpenTells"/>).</summary>
        private static void ThingsInTheWorldTell(Probe p, CompendiumBook book, Player player)
        {
            PlantGrowthTells(p, book, player);
            TreasurePickTells(p, book, player);
            BreakingOpenTells(p, book, player);
            CookingSeenTells(p, book, player);
            BearingAgainTells(p, book);
            TameToldTells(p, book, player);
        }

        /// <summary>The game telling the character a tame creature near by is tame now: its taming found out.</summary>
        private static void TameToldTells(Probe p, CompendiumBook book, Player player)
        {
            var tame = UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None)
                .Select(t => t != null ? t.GetComponent<Character>() : null)
                .FirstOrDefault(c => c != null && c.IsTamed() && !c.IsPlayer() && Vector3.Distance(c.transform.position, player.transform.position) <= 30f);
            if (tame == null)
            {
                p.Note("no tame creature is near to be told tame");
                return;
            }
            CompendiumKeeper.ToldTamed(tame.m_name + " $hud_tamedone");
            p.Check(book.Proved(Utils.GetPrefabName(tame.gameObject), EntryKeys.Did(Deed.Tamed)), $"told {Utils.GetPrefabName(tame.gameObject)} is tame now, its taming shows");
        }

        /// <summary>A loaded pickable picked before and bearing again, looked at: its growing back found out; one never picked shows nothing. Fed into a book of its own, as the regrowth seen earlier in the run is in the run's.</summary>
        private static void BearingAgainTells(Probe p, CompendiumBook runs)
        {
            var book = new CompendiumBook();
            CompendiumKeeper.RecordInto(book);
            try
            {
                BearingAgainInto(p, book);
            }
            finally
            {
                CompendiumKeeper.RecordInto(runs);
            }
        }

        /// <summary>The bearing-again row's checks, into the book the hooks feed now.</summary>
        private static void BearingAgainInto(Probe p, CompendiumBook book)
        {
            var bushes = UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None).Where(b => b != null && b.m_respawnTimeMinutes > 0f && !b.GetPicked()).ToList();
            long PickedAt(Pickable b) => b.GetComponent<ZNetView>().OrNull()?.GetZDO()?.GetLong(ZDOVars.s_pickedTime) ?? 0L;
            var again = bushes.FirstOrDefault(b => PickedAt(b) > 1);
            var never = bushes.FirstOrDefault(b => PickedAt(b) == 0 && (again == null || Utils.GetPrefabName(b.gameObject) != Utils.GetPrefabName(again.gameObject)));
            if (never != null)
            {
                DeedWatch.SeenBearingAgain(never);
                p.Check(!book.Proved(Utils.GetPrefabName(never.gameObject), EntryKeys.Did(Deed.GrewBack)), $"{Utils.GetPrefabName(never.gameObject)}, never picked, shows nothing of its growing back");
            }
            if (again == null)
            {
                p.Note("no pickable picked before and bearing again is loaded");
                return;
            }
            DeedWatch.SeenBearingAgain(again);
            p.Check(book.Proved(Utils.GetPrefabName(again.gameObject), EntryKeys.Did(Deed.GrewBack)), $"{Utils.GetPrefabName(again.gameObject)}, picked before and bearing again, shows how long it takes to grow back");
        }

        /// <summary>Food seen turning done, then burnt, on the nearest loaded cooking station: how long cooking takes shown, the station seen giving each.</summary>
        private static void CookingSeenTells(Probe p, CompendiumBook book, Player player)
        {
            var station = Nearest<CookingStation>(player);
            var cooked = station.OrNull()?.m_conversion?.FirstOrDefault(c => c?.m_to != null)?.m_to;
            if (station == null || cooked == null)
            {
                p.Note("no cooking station is loaded to watch food cook on");
                return;
            }
            var key = Utils.GetPrefabName(station.gameObject);
            DeedWatch.SlotTurned(station, cooked.name);
            var burnt = station.m_overCookedItem != null ? station.m_overCookedItem.name : null;
            if (burnt != null) DeedWatch.SlotTurned(station, burnt);
            p.Check(book.Proved(key, EntryKeys.Did(Deed.Finished)) && book.Witnessed(key, cooked.name, Tie.Gives) && (burnt == null || book.Witnessed(key, burnt, Tie.Gives)),
                $"food seen cooking on {key} shows how long it takes, and the station giving {cooked.name}" + (burnt != null ? $" and, left too long, {burnt}" : ""));
        }

        /// <summary>A thing that breaks, near by, destroyed by the character's hit: its health shown, whatever it gave, as a deposit's shell giving way to its vein shows its own.</summary>
        private static void BreakingOpenTells(Probe p, CompendiumBook book, Player player)
        {
            var thing = UnityEngine.Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None)
                .FirstOrDefault(d => d != null && Vector3.Distance(d.transform.position, player.transform.position) <= Sighting.PersonReach && InSight(d));
            if (thing == null)
            {
                p.Note("no thing that breaks is near to break open");
                return;
            }
            var hit = new HitData();
            hit.SetAttacker(player);
            YieldWatch.BrokenByHit(thing, hit);
            var key = Utils.GetPrefabName(thing.gameObject);
            p.Check(book.Proved(key, EntryKeys.Did(Deed.Broke)) && book.Tallied(Tally.Gave).Kills(key) == 0, $"{key} destroyed by the character's hit shows its health, counting no yield of its own");
        }

        /// <summary>A treasure lying in the world picked up by the character: seen together with the item it gives.</summary>
        private static void TreasurePickTells(Probe p, CompendiumBook book, Player player)
        {
            var treasure = UnityEngine.Object.FindObjectsByType<PickableItem>(FindObjectsSortMode.None)
                .Where(t => t != null && t.m_itemPrefab != null).OrderBy(t => (t.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (treasure == null)
            {
                p.Note("no treasure is loaded to pick up");
                return;
            }
            var key = Utils.GetPrefabName(treasure.gameObject);
            var item = Utils.GetPrefabName(treasure.m_itemPrefab.gameObject);
            YieldWatch.TreasurePicked(treasure, player, picked: true);
            p.Check(book.Witnessed(key, item, Tie.Gives), $"{key} picked up is seen together with the {item} it gives");
        }

        /// <summary>A crop or sapling near by seen growing into what it becomes: its growing found out, the two seen together; a young creature growing up is left to play.</summary>
        private static void PlantGrowthTells(Probe p, CompendiumBook book, Player player)
        {
            p.Note("growing up is seen in play only, as a young creature has to be gone for it");
            var near = Sighting.PersonReach;
            var plant = UnityEngine.Object.FindObjectsByType<Plant>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c != null && c.m_grownPrefabs != null && c.m_grownPrefabs.Length > 0 && c.m_grownPrefabs[0] != null
                    && Vector3.Distance(c.transform.position, player.transform.position) <= near && InSight(c));
            if (plant == null) p.Note("no plant is near to see grow");
            else
            {
                var plantKey = Utils.GetPrefabName(plant.gameObject);
                var grown = Utils.GetPrefabName(plant.m_grownPrefabs[0]);
                DeedWatch.Grown(plant, plant.m_grownPrefabs[0]);
                p.Check(book.Proved(plantKey, EntryKeys.Did(Deed.Grew)) && book.Witnessed(plantKey, grown, Tie.Gives), $"{plantKey} seen growing into {grown} shows how long that takes, the two seen together");
                // Its planter is found among the players by the id the game keeps on it, so one the character planted grows by its hand.
                var creator = plant.GetComponent<Piece>().OrNull()?.GetCreator() ?? 0L;
                if (creator == player.GetPlayerID()) p.Check(WorldEvents.BuiltBy(plant.GetComponent<Piece>()).Contains(ZNet.GetUID()), $"{plantKey}, planted by the character, grows by its hand");
                else p.Note($"{plantKey} was not planted by the character: its planter untried");
            }
        }

        /// <summary>A hit of the character's on the nearest loaded thing that breaks tells how its damage landed, as a creature's does; one on a rock's pieces knows the rock (<see cref="PiecesHitKnowTheirRock"/>).</summary>
        private static void ResourceHitTells(Probe p, CompendiumBook book, Player player)
        {
            PiecesHitKnowTheirRock(p, book, player);
            var thing = UnityEngine.Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None).Where(d => d != null)
                .OrderBy(d => (d.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (thing == null)
            {
                p.Note("nothing that breaks is loaded to hit");
                return;
            }
            var key = Utils.GetPrefabName(thing.gameObject);
            var hit = new HitData { m_toolTier = 10 };
            hit.m_damage.m_blunt = 10f;
            hit.m_damage.m_fire = 10f;
            hit.SetAttacker(player);
            ResourceHitWatch.Hit(thing, hit, thing.m_damages, thing.m_minToolTier);
            p.Check(ResistWords.Types.Any(t => book.Proved(key, EntryKeys.Damage(t))), $"a hit on {key}, a thing that breaks, tells how its damage landed");
            // What is in hand is paired as what broke it only where the hit was its own swing.
            var weapon = player.GetCurrentWeapon();
            var tool = weapon?.m_dropPrefab.OrNull();
            if (tool == null || weapon.m_shared.m_skillType == Skills.SkillType.None) p.Note("no tool in hand: what breaks it untried");
            else
            {
                p.Check(!book.Witnessed(key, tool.name, Tie.BrokenWith), $"a hit not swung with {tool.name} does not pair it as what broke {key}");
                hit.m_skill = weapon.m_shared.m_skillType;
                ResourceHitWatch.Hit(thing, hit, thing.m_damages, thing.m_minToolTier);
                p.Check(book.Witnessed(key, tool.name, Tie.BrokenWith), $"a swing of {tool.name} strong enough pairs it as what broke {key}");
            }
        }

        /// <summary>
        /// The pieces a rock breaks into when struck whole bring the rock along once hit, where it
        /// alone breaks into them: Yagluth's stone fingers into theirs, and a hit of the character's
        /// on the nearest loaded pieces of one rock makes that rock known, as mining its pieces.
        /// </summary>
        private static void PiecesHitKnowTheirRock(Probe p, CompendiumBook book, Player player)
        {
            var pieces = X.Catalog.Where(e => e.Kind == Kind.Resource).Select(e => (Pieces: e.Name, From: CompendiumKeeper.BrokenFrom(e.Name))).Where(r => r.From.Count > 0).ToList();
            var shared = pieces.Where(r => r.From.Count > 1).ToList();
            p.Note($"{Numbers.Count(pieces.Count)} kinds of pieces a rock breaks into, {Numbers.Count(shared.Count)} of them shared by several rocks, which none of them is known by: "
                + string.Join("; ", shared.Take(12).Select(r => r.Pieces + " from " + string.Join("/", r.From))));
            if (X.Find("RockFinger_frac") != null)
                p.Check(string.Join("/", CompendiumKeeper.BrokenFrom("RockFinger_frac")) == "RockFinger", "a stone finger at Yagluth's altar alone breaks into its pieces",
                    string.Join("/", CompendiumKeeper.BrokenFrom("RockFinger_frac")));
            var one = new HashSet<string>(pieces.Where(r => r.From.Count == 1).Select(r => r.Pieces));
            var thing = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(m => (m is MineRock || m is MineRock5) && one.Contains(Utils.GetPrefabName(m.gameObject)))
                .OrderBy(m => (m.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (thing == null)
            {
                p.Note("no pieces of a rock are loaded to hit");
                return;
            }
            var key = Utils.GetPrefabName(thing.gameObject);
            var rock = CompendiumKeeper.BrokenFrom(key)[0];
            var hit = new HitData { m_toolTier = 10 };
            hit.m_damage.m_pickaxe = 10f;
            hit.SetAttacker(player);
            if (thing is MineRock5 vein) ResourceHitWatch.Hit(vein, hit, vein.m_damageModifiers, vein.m_minToolTier);
            else if (thing is MineRock lump) ResourceHitWatch.Hit(lump, hit, lump.m_damageModifiers, lump.m_minToolTier);
            p.Check(book.Level(key) == KnownLevel.Known && book.Level(rock) == KnownLevel.Known && book.Came(rock)?.Way == CameBy.MinedPieces,
                $"a hit on {key} makes it known, and {rock}, which alone breaks into it, as mining its pieces", $"{book.Level(key)}, {book.Level(rock)} by {book.Came(rock)?.Way}");
        }

        /// <summary>
        /// One rule for every event: one beyond sight counts only where the character did it, and
        /// once however often it is told; one within reach under the ground, no line from the eye
        /// reaching it, is not seen.
        /// </summary>
        private static void DeathsOutOfSight(Probe p, CompendiumBook book, Player player, long[] me)
        {
            const string Made = "ScrySelfTestCreature";
            var far = player.transform.position + Vector3.right * (Sighting.PersonReach * 2f);
            CompendiumKeeper.Tell(WorldEvents.Make(Happened.Died, Made, far));
            p.Check(book.Tallied(Tally.Fell).Kills(Made) == 0 && !book.Proved(Made, EntryKeys.Did(Deed.Killed)), "a death beyond sight that the character had no hand in records nothing");
            var done = WorldEvents.Make(Happened.Died, Made, far, doers: me);
            CompendiumKeeper.Tell(done);
            CompendiumKeeper.Tell(done);
            p.Check(book.Tallied(Tally.Fell).Kills(Made) == 1 && book.Proved(Made, EntryKeys.Did(Deed.Killed)), "one the character had a hand in counts, once however often it is told, and shows that very creature's health");
            const string Buried = "ScrySelfTestBuried";
            var under = player.transform.position + Vector3.down * 12f;
            CompendiumKeeper.Tell(WorldEvents.Make(Happened.Died, Buried, under));
            p.Check(book.Tallied(Tally.Fell).Kills(Buried) == 0 && !CompendiumSight.SeesDeed(under.x, under.y, under.z),
                "a death within reach under the ground, no line from the eye to it, records nothing");
        }

        /// <summary>Whether a thing near by is in sight of the eye, as a deed done to it must be to be seen (<see cref="CompendiumSight.SeesDeed"/>).</summary>
        private static bool InSight(Component thing)
        {
            var at = thing.transform.position;
            return CompendiumSight.SeesDeed(at.x, at.y, at.z);
        }

        /// <summary>A way from a point the character sees a deed in so far, of eight around it, east first; east where it sees none, as a test inside walls has no clear way.</summary>
        private static Vector3 ClearWay(Vector3 from, float distance)
        {
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI / 4f;
                var way = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var at = from + way * distance;
                if (CompendiumSight.SeesDeed(at.x, at.y, at.z)) return way;
            }
            return Vector3.right;
        }

        /// <summary>
        /// A birth near by, of a tame creature that breeds and is not pregnant now: it shows how
        /// long it is pregnant, and the young it brought, which the game names by waking it last,
        /// told to the keeper as the birth names it (nothing is spawned, nor sent to other games),
        /// is known, seen born.
        /// </summary>
        private static void BirthSeen(Probe p, CompendiumBook book, Player player)
        {
            var parent = UnityEngine.Object.FindObjectsByType<Procreation>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c != null && Vector3.Distance(c.transform.position, player.transform.position) <= Sighting.PersonReach && !DeedWatch.Pregnant(c) && InSight(c));
            if (parent == null)
            {
                p.Note("no tame creature that breeds is near to see born");
                return;
            }
            var key = Utils.GetPrefabName(parent.gameObject);
            DeedWatch.Procreated(parent, wasPregnant: true);
            p.Check(book.Proved(key, EntryKeys.Did(Deed.Born)), "a birth seen near by shows how long it is pregnant");
            var young = parent.m_offspring != null ? Utils.GetPrefabName(parent.m_offspring) : null;
            if (young == null)
            {
                p.Note($"{key} names no young");
                return;
            }
            CompendiumKeeper.Tell(WorldEvents.Make(Happened.Born, key, parent.transform.position, with: young));
            p.Check(book.Level(young) == KnownLevel.Known && book.Came(young)?.Way == CameBy.Born, $"the young a birth near by brought ({young}) is known, seen born", $"{book.Level(young)}, {book.Came(young)?.Way.ToString() ?? "no way kept"}");
        }

        /// <summary>
        /// Made-up events through the real hooks, recorded into a book of the test's own
        /// (<see cref="CompendiumKeeper.RecordInto"/>): a hit of the character's on a loaded
        /// creature, on its weak spot, an armed creature's hit taken, a birth, a regrowth and a
        /// taming near by, a tree breaking by the character's hit with an item come to be, a death
        /// beyond sight done and not done and told twice, a map read. Nothing in the world
        /// changes, and neither the character's book nor the play counts do.
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
            var me = new[] { ZNet.GetUID() };

            DeathsOutOfSight(p, book, player, me);

            CreatureHitTells(p, book, player, real);
            ResourceHitTells(p, book, player);

            // An armed creature's hit taken: its attack's damage found out.
            var armed = UnityEngine.Object.FindObjectsByType<Humanoid>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && !h.IsPlayer() && h.GetCurrentWeapon()?.m_dropPrefab != null);
            if (armed == null) p.Note("no armed creature is loaded to be hit by");
            else
            {
                CompendiumKeeper.HitBy(armed);
                var weapon = armed.GetCurrentWeapon().m_dropPrefab.name;
                p.Check(book.FoundOutOf(Utils.GetPrefabName(armed.gameObject)) == EntryKeys.Attack(weapon), $"a hit taken from {Utils.GetPrefabName(armed.gameObject)} finds out its attack ({weapon})");
            }

            // Deeds near by: a birth (one not pregnant now), a regrowth, a taming.
            var near = Sighting.PersonReach;
            BirthSeen(p, book, player);
            var bush = UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b != null && Vector3.Distance(b.transform.position, player.transform.position) <= near && InSight(b));
            if (bush == null) p.Note("no pickable is near to see grow back");
            else
            {
                DeedWatch.SetPicked(bush, wasPicked: true, picked: false);
                p.Check(book.Proved(Utils.GetPrefabName(bush.gameObject), EntryKeys.Did(Deed.GrewBack)), "a pickable seen growing back shows how long that takes");
            }
            // A taming: one creature near by told wild, then tame; every other as the game has it.
            var tame = UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None)
                .Select(t => t != null ? t.GetComponent<Character>() : null)
                .FirstOrDefault(c => c != null && Vector3.Distance(c.transform.position, player.transform.position) <= near && InSight(c));
            if (tame == null) p.Note("no tameable creature is near to tame");
            else
            {
                CompendiumKeeper.Tamings(player, c => c != tame && c.IsTamed());
                CompendiumKeeper.Tamings(player, c => c == tame || c.IsTamed());
                p.Check(book.Proved(Utils.GetPrefabName(tame.gameObject), EntryKeys.Did(Deed.Tamed)), "a creature near by seen wild, then tame, shows how long taming takes");
            }
            ThingsInTheWorldTell(p, book, player);

            // A tree breaking by the character's hit with an item come to be meanwhile: counted as what it gave, however far.
            var tree = Nearest<TreeBase>(player);
            var item = Nearest<ItemDrop>(player);
            if (tree == null || item == null) p.Note("no tree and item are loaded to count a break with");
            else
            {
                YieldWatch.Begin(tree, me);
                YieldWatch.ItemMade(item);
                YieldWatch.End();
                var treeKey = Utils.GetPrefabName(tree.gameObject);
                p.Check(book.Tallied(Tally.Gave).Kills(treeKey) == 1 && book.Witnessed(treeKey, Utils.GetPrefabName(item.gameObject)), $"{treeKey} breaking is counted with what came of it");
            }

            // A pick of the character's with an item come to be: counted as what it gave, no break.
            var picked = Nearest<Pickable>(player);
            if (picked == null || item == null) p.Note("no pickable and item are loaded to count a pick with");
            else
            {
                YieldWatch.Begin(picked, me);
                YieldWatch.ItemMade(item);
                YieldWatch.End();
                var pickKey = Utils.GetPrefabName(picked.gameObject);
                p.Check(book.Tallied(Tally.Gave).Kills(pickKey) == 1 && !book.Proved(pickKey, EntryKeys.Did(Deed.Broke)), $"picking {pickKey} is counted as what it gave, and is no break");

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
                .FirstOrDefault(a => a != null && Vector3.Distance(a.transform.position, player.transform.position) <= near && InSight(a));
            if (spawner == null) p.Note("no spawner is near to see spawn");
            else
            {
                DeedWatch.Spawned(spawner, spawned: true);
                p.Check(book.Proved(Utils.GetPrefabName(spawner.gameObject), EntryKeys.Did(Deed.Spawned)), "a spawn seen near by shows its pool and pace");
            }
            var hive = Nearest<Beehive>(player);
            if (hive == null) p.Note("no beehive is loaded to collect from");
            else
            {
                DeedWatch.Collected(hive, player, held: true);
                p.Check(book.Proved(Utils.GetPrefabName(hive.gameObject), EntryKeys.Did(Deed.Collected)), "collecting from a beehive shows its pace");
            }

            // A map read keeps what it brings as found in the character's ground, never in its book.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && Places.LabelOf(e) != null);
            if (place != null)
            {
                var level = book.Level(place.Key);
                MapCatchUp.Apply(book, "self-test", new[] { place.Name });
                p.Check(book.Level(place.Key) == level && book.Found("self-test", WorldSource.Explored).Contains(place.Name), $"a map read keeps {place.Name} as found in your ground, the book as it was", $"{book.Level(place.Key)}");
            }

            CompendiumKeeper.RecordInto(null);
            p.Check(real.Version == version && real.PairsVersion == pairs && real.Learned == learned, "the character's book was left alone");
            p.Check(CompendiumKeeper.Play.Told() == counted, "and so were the play counts");
            yield return null;
        }

        /// <summary>The hide's page counts the troll's line of where it comes from, after the kill, and draws the troll as a chip with that count.</summary>
        private static void HideCounted(Probe p, Entry hide, Entry troll)
        {
            var line = Facts.For(hide).Where.FirstOrDefault(s => s.Record?.Giver == troll.Name);
            var counted = X.StrictSourceLine(line.Record);
            p.Check(counted == $"Dropped by {line.Record?.Shown}, 2 (1 of 1 kill)", "the hide's page counts the troll's line", counted);
            // Drawn as the troll's own chip, as under the loose rules, its count after its name.
            var chip = ScryPanel.WhereRowsForTest(X, hide).SelectMany(r => r.Items).FirstOrDefault(i => i.Prefab == troll.Name);
            p.Check(chip.Amount == "2 (1 of 1 kill)", "and draws the troll as a chip with that count", chip.Name + " " + chip.Amount);
        }

        /// <summary>The item page's title row counts the lines of where it comes from among its finds, the troll's line found out.</summary>
        private static void WhereCounted(Probe p) =>
            p.Check(ScryPanel.FindsForTest.Of > 0 && ScryPanel.FindsForTest.Found > 0, "its title row counts the lines of where it comes from, the troll's found out", ScryPanel.FindsChipForTest);

        /// <summary>
        /// A made-up character's pages read as a player sees them under the strict rules: the
        /// troll it fought keeps back its health, its attacks' damage, its weak spot and how many
        /// of its hide fall, says so faintly, and its You box tells what is still to find out;
        /// killed and counted, they show and Learned says what was found out, while what its
        /// stars change waits for a starred one to fall. Its hide's page counts the troll's line,
        /// a boar's page keeps back how long taming takes until one is tamed, and a map read is
        /// counted in the notice. Each page is pictured as it reads.
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
            // Its tiles are drawn only in view: the page is brought to them first.
            yield return null;
            yield return GoTo("overview");

            var facts = ScryPanel.Drawn(PanelPart.FactKeptBack);
            var still = ScryPanel.Drawn(PanelPart.StillToFindOut);
            var table = ScryPanel.Drawn(PanelPart.TableKeptBack);

            var tally = ScryPanel.Drawn(PanelPart.TallyCounted);
            yield return Until(() => ScryPanel.Drawn(PanelPart.StillToFindOut) > still, 3);
            p.Check(X.LeavingOut && X.KeepsBack(EntryKeys.Did(Deed.Killed)) && ScryPanel.Drawn(PanelPart.FactKeptBack) > facts, "the troll's health shows as a smudge, what shows it on hover");
            p.Check(ScryPanel.Drawn(PanelPart.StillToFindOut) > still, "its You box tells what is still to find out");
            p.Check(ScryPanel.Drawn(PanelPart.TableKeptBack) > table && ScryPanel.Drawn(PanelPart.AttacksWait) > 0, "its attacks not yet seen or taken fold into one faint line counting them");
            p.Check(ScryPanel.StillForTest.Any(l => l.StartsWith("Hit its weak spot", System.StringComparison.Ordinal)), "its weak spot is left out, the You box saying to hit it", string.Join("; ", ScryPanel.StillForTest));
            p.Check(X.StrictAmount(Tally.Fell, troll.Name, hide.Name) == CompendiumWords.Hidden && ScryPanel.Drawn(PanelPart.TallyCounted) > tally,
                "how many hides it drops shows a smudge until counted, its hover saying what shows it");
            var before = ScryPanel.FindsForTest;
            p.Check(before.Of > before.Found && (ScryPanel.FindsChipForTest?.Contains(" of ") ?? false), "its title row tells how much of what it keeps back was found out", ScryPanel.FindsChipForTest ?? "no chip");
            yield return PictureOf(p, "overview", "strict Troll, nothing found out");
            ScryPanel.TablesDrawnForTest.Clear();
            ScryPanel.KeepTablesForTest = true;
            var smudges = ScryPanel.Drawn(PanelPart.Smudge);
            yield return PictureOf(p, "fight", "strict Troll's fight, nothing found out");
            ScryPanel.KeepTablesForTest = false;
            p.Check(ScryPanel.Drawn(PanelPart.Smudge) > smudges, "what the fight keeps back shows as smudges, never as text");
            // Its damage kept back reads ???, its columns no wider than their headings need.
            var attacks = Facts.For(troll).Rows.FirstOrDefault(r => r.Title == CombatWords.AttacksTitle);
            // With every attack waiting, the table draws its title and the one line that counts them, no columns.
            if (attacks?.AttackCells != null && ScryPanel.Drawn(PanelPart.AttacksWait) > 0 && !ScryPanel.TablesDrawnForTest.ContainsKey(attacks.Title)) p.Note("every attack of the troll waits: no column of its table is drawn to measure");
            else if (attacks?.AttackCells != null && ScryPanel.TablesDrawnForTest.TryGetValue(attacks.Title, out var drawn))
            {
                var wide = attacks.AttackCells.Where(c => c < drawn.Columns.Length && drawn.Columns[c] > drawn.Headings[c] + 1f).Select(c => $"{attacks.Columns[c]} {Numbers.Fixed(drawn.Columns[c], 0)} for a heading of {Numbers.Fixed(drawn.Headings[c], 0)}").ToList();
                p.Check(wide.Count == 0, "a column of damage kept back is as wide as its heading, not its hidden figures", string.Join("; ", wide));
            }
            else p.Check(false, "the troll's attacks table was drawn in its fight");
            yield return PictureOf(p, "loot", "strict Troll's drops, nothing counted");

            // Killed and its loot counted: its health and its hide's count show, and Learned says what was found out.
            book.FoundOut(troll.Key, EntryKeys.Did(Deed.Killed));
            book.Dropped(troll.Name, new[] { (hide.Name, 2) });
            view.Tell(book, CompendiumRule.Strict);
            yield return Until(() => !X.KeepsBack(EntryKeys.Did(Deed.Killed)) && X.StrictAmount(Tally.Fell, troll.Name, hide.Name) == "2 (1 of 1)", 3);
            p.Check(!X.KeepsBack(EntryKeys.Did(Deed.Killed)), "killed, its health shows");
            p.Check(X.StrictAmount(Tally.Fell, troll.Name, hide.Name) == "2 (1 of 1)", "counted, its hide shows how many fell and how often", X.StrictAmount(Tally.Fell, troll.Name, hide.Name));
            p.Check(X.LearnedNote(troll) == "its health found out", "and Learned says what was found out", X.LearnedNote(troll));
            p.Check(X.KeepsBack(EntryKeys.Did(Deed.StarredFell)), "what its stars change waits for a starred one to fall");
            yield return Until(() => ScryPanel.FindsForTest.Found > before.Found, 3);
            p.Check(ScryPanel.FindsForTest.Found > before.Found, "and its title row counts more found out", $"{ScryPanel.FindsChipForTest} after {Numbers.Count(before.Found)} of {Numbers.Count(before.Of)}");
            yield return PictureOf(p, "overview", "strict Troll, killed");
            yield return PictureOf(p, "fight", "strict Troll's fight, killed");
            yield return PictureOf(p, "loot", "strict Troll's drops, counted");

            // The hide's page tells the troll's line with the character's count.
            X.Select(hide);
            HideCounted(p, hide, troll);
            yield return StrictGameShownRead(p, book, view, hide);
            yield return StrictSightingsRead(p, book, view, troll);
            yield return StrictStarsRead(p, book, view, troll, hide);
            // A plain word of a text is no name kept back, though an effect never met goes by it ("Fire").
            var fires = ScryPanel.AsDrawn(X, "fires Troll throw projectile");
            p.Check(!fires.StartsWith(CompendiumWords.Hidden, System.StringComparison.Ordinal), "a word of a text is no name kept back: \"fires\" stays", fires);
            // The hide's own page again: the readings between went back to the troll's.
            X.Select(hide);
            yield return null;
            yield return PictureOf(p, "from", "strict TrollHide, where it comes from");
            WhereCounted(p);

            // A tame creature's page keeps back how long taming takes until one is tamed.
            var boar = Pick(Kind.Creature, "Boar");
            var tamed = EntryKeys.Did(Deed.Tamed);
            if (boar != null && Facts.For(boar).Proofs.TryGetValue("Tames in", out var proof) && proof == tamed)
            {
                book.Raise(boar.Key, KnownLevel.Known);
                view.Tell(book, CompendiumRule.Strict);
                X.Select(boar);
                yield return null;
                p.Check(X.KeepsBack(tamed), "a boar's page keeps back how long taming takes");
                yield return PictureOf(p, "taming", "strict Boar's taming, none tamed");
                book.FoundOut(boar.Key, tamed);
                view.Tell(book, CompendiumRule.Strict);
                yield return Until(() => !X.KeepsBack(tamed), 3);
                p.Check(!X.KeepsBack(tamed), "until one is tamed");
                yield return PictureOf(p, "taming", "strict Boar's taming, one tamed");
            }
            else p.Note("no boar whose page tells how long taming takes");

            // A map read shows its places as Ground you explored says, seen as it starts, marked as that source's.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && Places.LabelOf(e) != null && view.Of(e) < KnownLevel.Seen);
            if (place != null)
            {
                MapCatchUp.Apply(book, "self-test", new[] { place.Name });
                view.InWorld("self-test");
                view.Tell(book, CompendiumRule.Strict);
                p.Check(view.Of(place) == KnownLevel.Seen && view.ByWorld(place) == WorldSource.Explored && book.Level(place.Key) < KnownLevel.Seen,
                    "a place the map brings shows as seen from your explored ground, not written into the book", $"{place.Name}: {view.Of(place)}, book {book.Level(place.Key)}");
            }
            Session.ShowKnownInstead(null);
        }

        /// <summary>
        /// What the game's menus show comes with the thing under the strict rules: a troll hide's
        /// uses name a known thing made of it and a wood wall's building its cost and tool, nothing paired.
        /// </summary>
        private static IEnumerator StrictGameShownRead(Probe p, CompendiumBook book, CompendiumView view, Entry hide)
        {
            // What it is used for is the game's crafting menu: its rows name what was met, no pair asked.
            var uses = Facts.For(hide).UseRows;
            var made = uses.SelectMany(r => r.Items).Select(i => i.Prefab).FirstOrDefault(prefab => !string.IsNullOrEmpty(prefab) && X.Find(prefab) != null);
            if (made != null)
            {
                book.Raise(made, KnownLevel.Known);
                view.Tell(book, CompendiumRule.Strict);
                yield return Until(() => !X.LeavesOut(made, gameShown: true), 3);
                p.Check(uses.All(r => r.GameShown) && !X.LeavesOut(made, gameShown: true) && X.LeavesOut(made), $"what the hide is used for names {made}, known and never paired, as the crafting menu shows it");
                p.Check(X.RecipeListed(null) && !X.RecipeListed("$item_no_such_recipe_" + Numbers.Count(UnityEngine.Random.Range(1000, 9999))), "a recipe the game does not list for the character keeps its rows, read live from the character");
                var usesTopic = ScryPanel.TopicsForTest.FirstOrDefault(topic => topic.Heading == "What it is used for").Id;
                if (usesTopic != null) yield return PictureOf(p, usesTopic, "strict TrollHide, what it is used for as the menu shows it");
            }
            else p.Note("nothing in the catalog is made of troll hide");
            // A piece's cost and tool come with the piece, as the hammer's menu shows them.
            var wall = Pick(Kind.Piece, "woodwall");
            var hammer = Pick(Kind.Item, "Hammer");
            var wood = Pick(Kind.Item, "Wood");
            if (wall != null && hammer != null && wood != null)
            {
                book.Raise(wall.Key, KnownLevel.Known);
                book.Raise(hammer.Key, KnownLevel.Known);
                book.Raise(wood.Key, KnownLevel.Known);
                view.Tell(book, CompendiumRule.Strict);
                X.Select(wall);
                yield return Until(() => X.Selected == wall && !X.LeavesOut(hammer.Key, gameShown: true), 3);
                var building = Facts.For(wall);
                p.Check(building.GameShown.Contains("Built with") && building.Rows.Any(r => r.GameShown && r.Items.Any(i => i.Prefab == wood.Name)) && !X.LeavesOut(hammer.Key, gameShown: true) && !X.LeavesOut(wood.Name, gameShown: true),
                    "a wood wall's cost and tool come with it under the strict rules, never paired");
                yield return PictureOf(p, "building", "strict wood wall's building, nothing paired");
                X.Select(hide);
                yield return null;
            }
            else p.Note("no wood wall, hammer or wood to try a piece's building with");
        }

        /// <summary>
        /// Under the strict rules the stage offers a creature's stars as it was seen with up close,
        /// and a creature only seen opens on what sight gave: its stars and gear seen, where it was seen.
        /// </summary>
        private static IEnumerator StrictSightingsRead(Probe p, CompendiumBook book, CompendiumView view, Entry troll)
        {
            X.Select(troll);
            yield return Until(() => X.Selected == troll, 3);
            p.Check(string.Join(",", X.StarsOffered(troll).Select(level => Numbers.Count(level))) == "1" && X.StarLevelFor(troll.Name, 3) == 1, "with none seen up close, the stage offers the troll without stars alone and stands a rolled one so", string.Join(",", X.StarsOffered(troll).Select(level => Numbers.Count(level))));
            book.Sighted(troll.Name, 2, null);
            view.Tell(book, CompendiumRule.Strict);
            yield return Until(() => X.StarsOffered(troll).Count == 2, 3);
            p.Check(string.Join(",", X.StarsOffered(troll).Select(level => Numbers.Count(level))) == "1,3" && X.StarLevelFor(troll.Name, 3) == 3 && X.StarLevelFor(troll.Name, 2) == 1,
                "seen with two stars up close, the stage offers no stars and two, a one-star roll standing without", string.Join(",", X.StarsOffered(troll).Select(level => Numbers.Count(level))));
            yield return GoTo("overview");
            yield return PictureOf(p, "overview", "strict Troll, seen with two stars up close");
            yield return PictureOf(p, "adjust", "strict Troll's adjust, the stars seen offered");
            yield return StrictAttackClipsRead(p, book, view, troll);

            var boar = Pick(Kind.Creature, "Boar");
            var meadows = X.Find(EntryKeys.For(Kind.Biome, "Meadows"));
            if (boar == null || meadows == null)
            {
                p.Note("no boar or meadows to read a page only seen with");
                yield break;
            }
            book.Raise(boar.Key, KnownLevel.Seen);
            book.Sighted(boar.Key, 0, null);
            book.Sighted(boar.Key, 1, null);
            book.Witness(boar.Key, meadows.Key);
            view.Tell(book, CompendiumRule.Strict);
            yield return Until(() => X.LevelOf(boar) == KnownLevel.Seen, 3);
            X.Select(boar);
            var seenLines = ScryPanel.Drawn(PanelPart.SeenLines);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SeenLines) > seenLines, 3);
            var lines = X.SeenLines(boar);
            p.Check(ScryPanel.Drawn(PanelPart.SeenLines) > seenLines && lines.Count == 2 && lines[0].Value == "no stars, 1 star" && lines[1].Value == meadows.ShownName,
                "a boar only seen opens on what sight gave: its stars seen up close and where it was seen", string.Join(" | ", lines.Select(l => l.Label + ": " + l.Value)));
            yield return Picture(p, "strict Boar, only seen, what sight gave");
            X.Select(troll);
            yield return null;
        }

        /// <summary>
        /// Under the strict rules an attack's clips wait for the attack, as its damage does, one chip
        /// counting them, until the attack hit the character or was seen begun up close.
        /// </summary>
        private static IEnumerator StrictAttackClipsRead(Probe p, CompendiumBook book, CompendiumView view, Entry troll)
        {
            var attackRow = Facts.For(troll).Rows.FirstOrDefault(r => r.Title == CombatWords.AttacksTitle);
            var attackItems = attackRow != null ? EntryKeys.ItemsAttacking(attackRow.Lines.Select(l => l.Link)).ToList() : new List<string>();
            yield return Until(() => Previews.ClipAttackItems().Count > 0, 8);
            var clipItems = Previews.ClipAttackItems();
            var first = attackItems.FirstOrDefault(i => clipItems.Values.Contains(i));
            if (first == null)
            {
                p.Note($"no attack clip of the troll was worked out to read: {Numbers.Count(clipItems.Count)} clip items, attacks {string.Join(", ", attackItems)}");
                yield break;
            }
            yield return GoTo("animations");
            var kept = ScryPanel.Drawn(PanelPart.ClipKeptBack);
            yield return Until(() => ScryPanel.Drawn(PanelPart.ClipKeptBack) > kept, 3);
            p.Check(X.KeepsBackAttackClip(first) && ScryPanel.Drawn(PanelPart.ClipKeptBack) > kept, $"the troll's attack clips wait as one chip counting them until each attack was taken or seen begun up close ({first})");
            yield return PictureOf(p, "animations", "strict Troll's animations, the attacks' clips kept back");
            book.FoundOut(troll.Name, EntryKeys.Attack(first));
            view.Tell(book, CompendiumRule.Strict);
            yield return Until(() => !X.KeepsBackAttackClip(first), 3);
            p.Check(!X.KeepsBackAttackClip(first) && X.KeepsBackAttackClip(attackItems.FirstOrDefault(i => i != first) ?? first) == (attackItems.Count > 1), "an attack seen begun up close frees its clips alone");
            yield return GoTo("overview");
        }

        /// <summary>
        /// Under the strict rules the stars table keeps back by the cell: killed, the health without
        /// stars shows while each starred column waits for one with that many stars to fall, and a
        /// trophy's chance by stars is the character's own count at those stars.
        /// </summary>
        private static IEnumerator StrictStarsRead(Probe p, CompendiumBook book, CompendiumView view, Entry troll, Entry hide)
        {
            X.Select(troll);
            yield return Until(() => X.Selected == troll, 3);
            var table = Facts.For(troll).Rows.FirstOrDefault(r => r.Title == CombatWords.StarsTitle);
            if (table == null)
            {
                p.Note("the troll has no stars table");
                yield break;
            }
            p.Check(table.CellProofs.ContainsKey((0, 1)) && table.CellProofs.ContainsValue(EntryKeys.StarsFell(1)) && table.LineProofs.Count == 0,
                "the stars table keeps back by the cell: health without stars until a kill, each starred column until one with that many stars fell");
            p.Check(!X.KeepsBack(EntryKeys.Did(Deed.Killed)) && X.KeepsBack(EntryKeys.StarsFell(1)) && X.KeepsBack(EntryKeys.StarsFell(2)), "killed, its health without stars shows while the starred columns wait");
            book.Dropped(troll.Name, new[] { (hide.Name, 3) }, 2);
            book.FoundOut(troll.Name, EntryKeys.StarsFell(1));
            view.Tell(book, CompendiumRule.Strict);
            yield return Until(() => !X.KeepsBack(EntryKeys.StarsFell(1)), 3);
            p.Check(!X.KeepsBack(EntryKeys.StarsFell(1)) && X.KeepsBack(EntryKeys.StarsFell(2)), "one with one star fallen, the one-star column shows and the two-star one waits");
            var trophy = table.StarItems.Values.FirstOrDefault();
            if (trophy != null)
            {
                p.Check(X.StrictStarShare(troll.Name, trophy, 1) == "0 of 1 kill" && X.StrictStarShare(troll.Name, trophy, 2) == CompendiumWords.Hidden,
                    "its trophy's chance at one star is the character's own count, at two stars a smudge", X.StrictStarShare(troll.Name, trophy, 1));
            }
            else p.Note("no drop of the troll's gets likelier with stars");
            p.Check(X.LearnedNote(troll) == "what 1 star changes found out", "and Learned says what was found out", X.LearnedNote(troll));
            yield return PictureOf(p, "fight", "strict Troll's fight, one star fallen");
            yield return PictureOf(p, "overview", "strict Troll, one star fallen, what is still to find out");
        }
    }
}
