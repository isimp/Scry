using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumStrictTests
    {
        // Strict shows what a known entry is, and of how it connects only what the character saw
        // go together: the drops it saw fall, the places it met a creature in, an item made at a
        // station. What was not witnessed is left out silently; a page's own parts (its sounds,
        // effects, projectiles) always show; below the milestone floor a page shows whole.

        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature);
            public readonly Entry Boar = E("Boar", Kind.Creature);
            public readonly Entry Hide = E("TrollHide", Kind.Item);
            public readonly Entry Coins = E("Coins", Kind.Item);
            public readonly Entry Grunt = E("sfx_troll_grunt", Kind.Sound);
            public readonly Entry Squeal = E("sfx_boar_squeal", Kind.Sound);
            public readonly Entry Log = E("Troll_log", Kind.Item);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Troll.AddTermLink("drops", Hide);
                Troll.AddTermLink("drops", Coins);
                Grunt.UsedBy.Add("Troll");
                Squeal.UsedBy.Add("Boar");
                var all = new[] { Troll, Boar, Hide, Coins, Grunt, Squeal, Log }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Raise(Troll.Key, KnownLevel.Known);
                Book.Raise(Boar.Key, KnownLevel.Seen);
                Book.Raise(Hide.Key, KnownLevel.Known);
                Book.Raise(Coins.Key, KnownLevel.Known);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void AKnownCreaturesPageKeepsBackHowEachDamageLandsUntilTheCharacterHitItWithIt()
        {
            // The game's popup on a hit tells how its damage landed (Weak, Resistant, Immune, or
            // a plain number): a type so told shows, the rest read –– under the strict rules.
            var world = new World();
            world.Book.Prove(world.Troll.Key, EntryKeys.Damage("Fire"));
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.False(world.Explorer.HidesDamage("Fire"));
            Assert.True(world.Explorer.HidesDamage("Frost"));

            // Not under the loose rules. Anything that breaks keeps it back as a creature does, its
            // grid one a hit reveals as the page's facts say (Facts.Row.HitReveals).
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.HidesDamage("Frost"));
        }

        [Fact]
        public void AKnownPageLeavesOutWhatTheCharacterDidNotSeeGoTogether()
        {
            var world = new World();
            world.Book.Witness(world.Hide.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.False(world.Explorer.LeavesOut(world.Hide.Key));
            Assert.True(world.Explorer.LeavesOut(world.Coins.Key));
            Assert.False(world.Explorer.LeavesOut(world.Grunt.Key));
            Assert.False(world.Explorer.LeavesOut(world.Troll.Key));
            Assert.False(world.Explorer.LeavesOut("NotInTheCatalog"));
        }

        [Fact]
        public void AChainsStepSeenTogetherWhileItsPageIsShownIsNamedAtOnce()
        {
            // The egg's page shows the breeding chain with the hen left out; the hen is then seen
            // growing up from a chick, and the chain names it as soon as that is handed over.
            var hen = E("Hen", Kind.Creature);
            var chicken = E("Chicken", Kind.Creature);
            var egg = E("ChickenEgg", Kind.Item);
            var explorer = new Explorer(new[] { hen, chicken, egg }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            foreach (var e in new[] { hen, chicken, egg }) book.Raise(e.Key, KnownLevel.Known);
            book.Witness(chicken.Key, egg.Key);
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            var chain = new Chain { Title = ChainWords.Breeding, Steps = new List<string[]> { new[] { "Hen" }, new[] { "ChickenEgg" }, new[] { "Chicken" }, new[] { "Hen" } } };
            explorer.Select(egg);
            Assert.True(explorer.LeavesOut(chain, hen.Key));
            Assert.False(explorer.LeavesOut(chain, chicken.Key));

            book.Witness(chicken.Key, hen.Key);
            explorer.Know(view);
            Assert.False(explorer.LeavesOut(chain, hen.Key));
            // Another chain shown beside it answers for its own steps.
            var other = new Chain { Title = ChainWords.Breeding, Steps = new List<string[]> { new[] { "Hen" }, new[] { "ChickenEgg" } } };
            Assert.True(explorer.LeavesOut(other, hen.Key));
        }

        [Fact]
        public void ALineOfWhereSomethingSpawnsWaitsOnTheBiomeNotYetMetAndCountsByItOnceShown()
        {
            // A Troll's page: a world spawn line naming no boss, and one waiting for The Elder,
            // both in the Swamp; the Black Forest met, the Swamp not.
            var troll = E("Troll", Kind.Creature);
            var elder = E("gd_king", Kind.Creature);
            var forest = E("BlackForest", Kind.Biome);
            var swamp = E("Swamp", Kind.Biome);
            var explorer = new Explorer(new[] { troll, elder, forest, swamp }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            book.Raise(forest.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            explorer.Select(troll);

            // Hidden for its biome, a line waits on meeting that biome, so it counts as never met.
            Assert.Equal(swamp.Key, explorer.WaitsOn(null, new[] { swamp.Key }));
            Assert.True(explorer.NotMet(explorer.WaitsOn(null, new[] { swamp.Key })));
            // A boss not met is waited on first; one met waits on the biome still to meet.
            Assert.Equal(elder.Key, explorer.WaitsOn(elder.Key, new[] { swamp.Key }));
            book.Raise(elder.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            Assert.Equal(swamp.Key, explorer.WaitsOn(elder.Key, new[] { swamp.Key }));

            // Its biome met, the line counts by it, found, so the page's total stays the same.
            book.Raise(swamp.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            Assert.Equal(forest.Key, explorer.WaitsOn(null, new[] { forest.Key, swamp.Key }));
            Assert.False(explorer.NotMet(explorer.WaitsOn(null, new[] { swamp.Key })));
            Assert.Equal(elder.Key, explorer.WaitsOn(elder.Key, new[] { swamp.Key }));
            Assert.Null(explorer.WaitsOn(null, null));
        }

        [Fact]
        public void AChainReadsTheSameOnEveryPageOfIt()
        {
            // A hen's breeding chain, egg hatching and chick growing up seen, its laying not: the hen
            // is named for its growing up, the egg and chick for its hatching, on each page alike.
            var hen = E("Hen", Kind.Creature);
            var chicken = E("Chicken", Kind.Creature);
            var egg = E("ChickenEgg", Kind.Item);
            var explorer = new Explorer(new[] { hen, chicken, egg }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            foreach (var e in new[] { hen, chicken, egg }) book.Raise(e.Key, KnownLevel.Known);
            book.Witness(chicken.Key, egg.Key);
            book.Witness(chicken.Key, hen.Key);
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            var chain = new Chain { Title = ChainWords.Breeding, Steps = new List<string[]> { new[] { "Hen" }, new[] { "ChickenEgg" }, new[] { "Chicken" }, new[] { "Hen" } } };
            foreach (var page in new[] { hen, chicken, egg })
            {
                explorer.Select(page);
                Assert.All(new[] { hen, chicken, egg }, step => Assert.False(explorer.LeavesOut(chain, step.Key), $"{step.Key} on {page.Key}'s page"));
            }

            // Only the hatching seen: the hen, of no link seen, is left out on the egg's and the chick's pages alike.
            var hatched = new CompendiumBook();
            foreach (var e in new[] { hen, chicken, egg }) hatched.Raise(e.Key, KnownLevel.Known);
            hatched.Witness(chicken.Key, egg.Key);
            view.Tell(hatched, CompendiumRule.Strict);
            explorer.Know(view);
            foreach (var page in new[] { chicken, egg })
            {
                explorer.Select(page);
                Assert.True(explorer.LeavesOut(chain, hen.Key), $"Hen on {page.Key}'s page");
                Assert.False(explorer.LeavesOut(chain, page == egg ? chicken.Key : egg.Key));
            }
            // On its own page a thing is never left out; under the loose rules nothing met is.
            explorer.Select(hen);
            Assert.False(explorer.LeavesOut(chain, hen.Key));
            view.Tell(hatched, CompendiumRule.Loose);
            explorer.Know(view);
            explorer.Select(egg);
            Assert.False(explorer.LeavesOut(chain, hen.Key));
        }

        [Fact]
        public void ABossCalledAtItsAltarLinksItsOfferingThroughTheAltarsPlace()
        {
            // Yagluth's chain: the totem offered at the altar of its place, the boss, its trophy.
            var totem = E("GoblinTotem", Kind.Item);
            var place = E("GoblinKing", Kind.Location);
            var boss = E("GoblinKing", Kind.Creature);
            var trophy = E("TrophyGoblinKing", Kind.Item);
            var stone = E("goblinking_totemholder", Kind.Piece);
            var all = new[] { totem, place, boss, trophy, stone };
            var explorer = new Explorer(all.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var chain = new Chain { Title = ChainWords.Summoning, Altar = 1, Steps = new List<string[]> { new[] { totem.Key }, new[] { place.Key }, new[] { boss.Key }, new[] { trophy.Key } } };
            CompendiumBook Book(IEnumerable<(string, string)> pairs)
            {
                var book = new CompendiumBook();
                foreach (var e in all) book.Raise(e.Key, KnownLevel.Known);
                foreach (var (a, b) in pairs) book.Witness(a, b);
                return book;
            }
            bool Shown(CompendiumBook book, Entry page, Entry step)
            {
                view.Tell(book, CompendiumRule.Strict);
                explorer.Know(view);
                explorer.Select(page);
                return !explorer.LeavesOut(chain, step.Key);
            }

            // A call now ties the offering to the altar's place as well.
            var called = BossCall.Pairs(stone.Key, totem.Key, boss.Key, place.Key).ToList();
            Assert.Contains((totem.Key, place.Key), called);
            Assert.True(Shown(Book(called), boss, totem));
            Assert.True(Shown(Book(called), trophy, totem));

            // A book from before keeps only the offering with the boss and the boss with its place: read through the boss, the offering shows.
            var before = new[] { (stone.Key, boss.Key), (totem.Key, boss.Key), (stone.Key, totem.Key), (boss.Key, place.Key) };
            Assert.True(Shown(Book(before), boss, totem));
            Assert.True(Shown(Book(before), place, totem));

            // Not the offering never seen with the boss, nor through a place the boss was never seen in.
            Assert.False(Shown(Book(new[] { (boss.Key, place.Key) }), boss, totem));
            Assert.False(Shown(Book(new[] { (totem.Key, boss.Key) }), boss, totem));
            // Nor on a chain with no altar step: there the step after the offering is all that links it.
            var plain = new Chain { Title = chain.Title, Steps = chain.Steps };
            view.Tell(Book(before), CompendiumRule.Strict);
            explorer.Know(view);
            explorer.Select(boss);
            Assert.True(explorer.LeavesOut(plain, totem.Key));
        }

        /// <summary>Yagluth, whose fall sets a key the Bog Witch's and Haldor's wares and a raid wait for, with the book and rules of a page.</summary>
        private sealed class Fallen
        {
            public readonly Entry Boss = E("GoblinKing", Kind.Creature);
            public readonly Entry Witch = E("BogWitch", Kind.Creature);
            public readonly Entry Haldor = E("Haldor", Kind.Creature);
            public readonly Entry Spice = E("SpicePlains", Kind.Item);
            public readonly Entry Egg = E("ChickenEgg", Kind.Item);
            public readonly Entry Seekers = E("army_seekers", Kind.Raid);
            public readonly Entry Goblin = E("Goblin", Kind.Creature);
            public readonly Entry Fight = E("boss_goblinking", Kind.Raid);
            public readonly Entry OtherFight = E("boss_bonemass", Kind.Raid);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();
            public readonly UnlockBook Unlocks = new UnlockBook();

            public Fallen()
            {
                var all = new[] { Boss, Witch, Haldor, Spice, Egg, Seekers, Goblin, Fight, OtherFight };
                Explorer = new Explorer(all.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries) { Unlocks = Unlocks, FallSets = key => key == Boss.Key ? "defeated_goblinking" : null };
                Unlocks.Add("defeated_goblinking", Unlock.Sells, Spice.Name, Witch.Name);
                Unlocks.Add("defeated_goblinking", Unlock.Sells, Egg.Name, Haldor.Name);
                Unlocks.Add("defeated_goblinking", Unlock.RaidStarts, Seekers.Key);
                Unlocks.Add("defeated_goblinking", Unlock.Spawns, Goblin.Name);
                // A raid comes to be known only by living through it.
                foreach (var e in all) Book.Raise(e.Key, e.Kind == Kind.Raid ? KnownLevel.Seen : KnownLevel.Known);
            }

            /// <summary>Whether a page shows a name under the strict rules.</summary>
            public bool Shows(Entry page, Entry named)
            {
                View.Tell(Book, CompendiumRule.Strict);
                Explorer.Know(View);
                Explorer.Select(page);
                return !Explorer.LeavesOut(named.Key);
            }
        }

        [Fact]
        public void AWareSeenOnTheStoreThatSellsItOnceABossFellLinksTheBossAndTheWare()
        {
            // A store lists a ware waiting for a boss only once the boss fell: seeing it on the
            // store that sells it so shows the boss's fall and the ware went together.
            var world = new Fallen();
            Assert.False(world.Shows(world.Boss, world.Spice));
            world.Book.Witness(world.Witch.Key, world.Spice.Key);
            Assert.True(world.Shows(world.Boss, world.Spice));
            Assert.True(world.Shows(world.Spice, world.Boss));

            // Not a ware seen on a store that sells it without waiting for the boss.
            Assert.False(world.Shows(world.Boss, world.Egg));
            world.Book.Witness(world.Witch.Key, world.Egg.Key);
            Assert.False(world.Shows(world.Boss, world.Egg));
            world.Book.Witness(world.Haldor.Key, world.Egg.Key);
            Assert.True(world.Shows(world.Boss, world.Egg));

            // Nor a ware another fall opens, nor with a creature whose fall sets no key.
            world.View.FallSets = key => null;
            Assert.False(world.Shows(world.Boss, world.Spice));
        }

        /// <summary>Yagluth's lightning beam, a lightning staff and a mead, each tied to the effect it gives, with the book and rules of a page.</summary>
        private sealed class Struck
        {
            public readonly Entry Boss = E("GoblinKing", Kind.Creature);
            public readonly Entry Beam = E("GoblinKing_Beam", Kind.Item);
            public readonly Entry Nova = E("GoblinKing_Nova", Kind.Item);
            public readonly Entry Staff = E("StaffLightning", Kind.Item);
            public readonly Entry Mead = E("MeadPoisonResist", Kind.Item);
            public readonly Entry Lightning = E("Lightning", Kind.StatusEffect);
            public readonly Entry Resist = E("PoisonResist", Kind.StatusEffect);
            public readonly Entry Burning = E("Burning", Kind.StatusEffect);
            public readonly Entry FireSword = E("SwordFire", Kind.Item);
            // Yagluth's meteors: the attack spawns what drops them, and each falling meteor burns.
            public readonly Entry Meteors = E("GoblinKing_Meteors", Kind.Item);
            public readonly Entry Dropper = E("GoblinKing_meteor_spawner", Kind.Other);
            public readonly Entry Meteor = E("projectile_meteor", Kind.Projectile);
            // A Writhan's death blast, and the frost orbs a staff's shield sends out.
            public readonly Entry Writhan = E("Writhan", Kind.Creature);
            public readonly Entry Blast = E("writhan_explosion", Kind.Other);
            public readonly Entry Poison = E("Poison", Kind.StatusEffect);
            public readonly Entry Shield = E("Staff_FrostOrbs", Kind.StatusEffect);
            public readonly Entry Orb = E("staff_FrostOrbs_projectile", Kind.Projectile);
            public readonly Entry Frost = E("Frost", Kind.StatusEffect);
            // A fire pit burning what stands in it, and a creature freezing what is near.
            public readonly Entry FirePit = E("fire_pit", Kind.Piece);
            public readonly Entry Wisp = E("FrostWisp", Kind.Creature);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public Struck()
            {
                Boss.Attacks = new[] { (Beam.Name, new[] { "lightning" }), (Nova.Name, new[] { "lightning" }), (Meteors.Name, new string[0]) };
                Writhan.Attacks = new[] { (EntryKeys.DeathAttack, new[] { "poison" }) };
                SpawnedBy(Dropper, Meteors);
                SpawnedBy(Meteor, Dropper);
                Writhan.DeathBlast = Blast.Name;
                SpawnedBy(Orb, Shield);
                Burning.Links.Add(new Link { Group = LinkWords.GivenBy, Target = Meteor.Key, Notes = { CombatWords.OfDamage("fire") }, Back = true });
                Poison.Links.Add(new Link { Group = LinkWords.GivenBy, Target = Blast.Key, Notes = { CombatWords.OfDamage("poison") }, Back = true });
                Frost.Links.Add(new Link { Group = LinkWords.GivenBy, Target = Orb.Key, Notes = { CombatWords.OfDamage("frost") }, Back = true });
                Burning.Links.Add(new Link { Group = LinkWords.GivenBy, Target = FirePit.Key, Notes = { CombatWords.OfDamage("fire") }, Back = true });
                Frost.Links.Add(new Link { Group = LinkWords.GivenBy, Target = Wisp.Key, Notes = { CombatWords.OfDamage("frost") }, Back = true });
                // As the catalog notes them: an item's damage gives the effect it causes, a mead the effect it puts on.
                foreach (var item in new[] { Beam, Nova, Staff })
                {
                    Lightning.Links.Add(new Link { Group = LinkWords.GivenBy, Target = item.Key, Notes = { CombatWords.OfDamage("lightning") }, Back = true });
                }
                Burning.Links.Add(new Link { Group = LinkWords.GivenBy, Target = FireSword.Key, Notes = { CombatWords.OfDamage("fire") }, Back = true });
                Resist.Links.Add(new Link { Group = LinkWords.GivenBy, Target = Mead.Key, Notes = { "m_consumeStatusEffect" }, Back = true });
                var all = new[] { Boss, Beam, Nova, Staff, Mead, Lightning, Resist, Burning, FireSword, Meteors, Dropper, Meteor, Writhan, Blast, Poison, Shield, Orb, Frost, FirePit, Wisp };
                Explorer = new Explorer(all.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Raise(Boss.Key, KnownLevel.Known);
                Book.Raise(Lightning.Key, KnownLevel.Known);
                Book.Raise(Resist.Key, KnownLevel.Known);
                Book.Raise(Burning.Key, KnownLevel.Known);
                Book.Raise(Writhan.Key, KnownLevel.Known);
                Book.Raise(Poison.Key, KnownLevel.Known);
                Book.Raise(Frost.Key, KnownLevel.Known);
                // Seen falling, as the book holds what was seen near a place: no more than seen.
                Book.Raise(Meteor.Key, KnownLevel.Seen);
                Book.Raise(Blast.Key, KnownLevel.Seen);
                Book.Raise(Orb.Key, KnownLevel.Seen);
            }

            /// <summary>Notes what spawns a thing, as the catalog links it back.</summary>
            private static void SpawnedBy(Entry thing, Entry by)
            {
                thing.Links.Add(new Link { Group = LinkWords.SpawnedBy, Target = by.Key, Back = true });
            }

            /// <summary>The level an entry shows by under the strict rules.</summary>
            public KnownLevel Level(Entry entry)
            {
                View.Tell(Book, CompendiumRule.Strict);
                return View.Of(entry);
            }

            /// <summary>Whether a page shows a name under the strict rules.</summary>
            public bool Shows(Entry page, Entry named)
            {
                View.Tell(Book, CompendiumRule.Strict);
                Explorer.Know(View);
                Explorer.Select(page);
                return !Explorer.LeavesOut(named.Key);
            }
        }

        [Fact]
        public void ACreaturesAttackTakenOrSeenBegunIsMet()
        {
            // An attack is no thing of its own to pick up: it is met once it hit the character, or
            // was seen begun, from any creature carrying it.
            var world = new Struck();
            // Named on its creature's page, it is heard of only.
            Assert.True(world.Level(world.Beam) < KnownLevel.Seen);
            world.Book.FoundOut(world.Boss.Key, EntryKeys.Attack(world.Beam.Name));
            Assert.Equal(KnownLevel.Known, world.Level(world.Beam));
            // Not another attack of the same creature, nor one proved on a creature not carrying it.
            Assert.True(world.Level(world.Nova) < KnownLevel.Seen);
            world.Book.FoundOut("Troll", EntryKeys.Attack(world.Nova.Name));
            Assert.True(world.Level(world.Nova) < KnownLevel.Seen);
        }

        [Fact]
        public void AnEffectShowsTheKnownThingsWhoseDamageGivesIt()
        {
            // The game puts lightning on whatever lightning damage hits (Character.AddLightningDamage),
            // and shows an item's damage on its tooltip and an attack's once found out: what that
            // damage gives shows with it on both pages.
            var world = new Struck();
            Assert.False(world.Shows(world.Lightning, world.Beam));
            world.Book.FoundOut(world.Boss.Key, EntryKeys.Attack(world.Beam.Name));
            Assert.True(world.Shows(world.Lightning, world.Beam));
            Assert.True(world.Shows(world.Beam, world.Lightning));
            Assert.False(world.Shows(world.Lightning, world.Nova));

            // A weapon once held, as the game then shows its damage.
            Assert.False(world.Shows(world.Lightning, world.Staff));
            world.Book.Raise(world.Staff.Key, KnownLevel.Seen);
            Assert.False(world.Shows(world.Lightning, world.Staff));
            world.Book.Raise(world.Staff.Key, KnownLevel.Known);
            Assert.True(world.Shows(world.Lightning, world.Staff));
            // Only with the effects its own damage gives.
            Assert.False(world.Shows(world.Burning, world.Staff));

            // Not what gives an effect another way, as a mead drunk: that still waits to be seen together.
            world.Book.Raise(world.Mead.Key, KnownLevel.Known);
            Assert.False(world.Shows(world.Resist, world.Mead));
        }

        [Fact]
        public void AnEffectShowsWhatAMetThingSpawnsWhoseOwnDamageGivesIt()
        {
            // A falling meteor or a lingering blast deals its own damage: it goes with what it
            // gives once what spawns it is met, through what spawns that.
            var world = new Struck();
            Assert.False(world.Shows(world.Burning, world.Meteor));
            world.Book.FoundOut(world.Boss.Key, EntryKeys.Attack(world.Meteors.Name));
            Assert.True(world.Shows(world.Burning, world.Meteor));
            Assert.True(world.Shows(world.Meteor, world.Burning));

            // A death that hurts, once seen up close.
            Assert.False(world.Shows(world.Poison, world.Blast));
            world.Book.FoundOut(world.Writhan.Key, EntryKeys.Attack(EntryKeys.DeathAttack));
            Assert.True(world.Shows(world.Poison, world.Blast));

            // An effect had that sends it out, as a staff's shield its orbs.
            Assert.False(world.Shows(world.Frost, world.Orb));
            world.Book.Raise(world.Shield.Key, KnownLevel.Seen);
            Assert.False(world.Shows(world.Frost, world.Orb));
            world.Book.Raise(world.Shield.Key, KnownLevel.Known);
            Assert.True(world.Shows(world.Frost, world.Orb));
        }

        [Fact]
        public void AnEffectShowsAKnownPieceOrCreatureWhoseOwnAreaGivesIt()
        {
            // A fire pit burns what stands in its flames, a creature's cold hurts what comes near:
            // each goes with what it gives once known, as an item does.
            var world = new Struck();
            Assert.False(world.Shows(world.Burning, world.FirePit));
            world.Book.Raise(world.FirePit.Key, KnownLevel.Seen);
            Assert.False(world.Shows(world.Burning, world.FirePit));
            world.Book.Raise(world.FirePit.Key, KnownLevel.Known);
            Assert.True(world.Shows(world.Burning, world.FirePit));
            Assert.True(world.Shows(world.FirePit, world.Burning));
            Assert.False(world.Shows(world.Frost, world.FirePit));

            // Not a part of the game's own known by itself, as the meteors' dropper: only through
            // what spawns it.
            world.Book.Raise(world.Dropper.Key, KnownLevel.Known);
            Assert.False(world.Shows(world.Burning, world.Meteor));

            // Nor one seen only, though a met attack spawns it, as a creature summoned: it waits to be known itself.
            world.Wisp.Links.Add(new Link { Group = LinkWords.SpawnedBy, Target = world.Meteors.Key, Back = true });
            world.Book.FoundOut(world.Boss.Key, EntryKeys.Attack(world.Meteors.Name));
            world.Book.Raise(world.Wisp.Key, KnownLevel.Seen);
            Assert.False(world.Shows(world.Frost, world.Wisp));
            world.Book.Raise(world.Wisp.Key, KnownLevel.Known);
            Assert.True(world.Shows(world.Frost, world.Wisp));
        }

        [Fact]
        public void ABossesFightLivedThroughLinksTheBossAndItsFight()
        {
            // The game plays a boss's fight only while its health bar shows, so the fight lived
            // through went together with that boss: its page shows the fight, the fight's the boss.
            var world = new Fallen();
            var fight = world.Fight;
            var other = world.OtherFight;
            world.View.FightOf = key => key == world.Boss.Key ? fight.Key : null;
            world.Book.Raise(other.Key, KnownLevel.Known, way: CameBy.LivedThrough);
            Assert.False(world.Shows(world.Boss, fight));
            world.Book.Raise(fight.Key, KnownLevel.Known, way: CameBy.LivedThrough);
            Assert.True(world.Shows(world.Boss, fight));
            Assert.True(world.Shows(fight, world.Boss));

            // Not another boss's fight, nor a creature that is no boss.
            Assert.False(world.Shows(world.Boss, other));
            Assert.False(world.Shows(world.Goblin, fight));
            Assert.False(world.Shows(fight, world.Goblin));
        }

        [Fact]
        public void ARaidLivedThroughLinksTheBossWhoseFallLetsItCome()
        {
            // The game starts a raid only while every key it waits for is set, so one lived through
            // went together with each fall it waits for.
            var world = new Fallen();
            Assert.False(world.Shows(world.Boss, world.Seekers));
            world.Book.Raise(world.Seekers.Key, KnownLevel.Known, way: CameBy.LivedThrough);
            Assert.True(world.Shows(world.Boss, world.Seekers));
            Assert.True(world.Shows(world.Seekers, world.Boss));
        }

        [Fact]
        public void ACreatureMetAfterABossFellIsNotLinkedToTheFall()
        {
            // Meeting a Fuling shows nothing of the key it may wait for: the spawn stays apart, and
            // under the strict rules its row has no way to show at all, so it is left out whole.
            var world = new Fallen();
            world.Book.Witness(world.Witch.Key, world.Spice.Key);
            Assert.False(world.Shows(world.Boss, world.Goblin));
            Assert.True(UnlockWays.SeenTogether(Unlock.Sells));
            Assert.True(UnlockWays.SeenTogether(Unlock.RaidStarts));
            Assert.False(UnlockWays.SeenTogether(Unlock.RaidEnds));
            Assert.False(UnlockWays.SeenTogether(Unlock.Spawns));
            Assert.False(UnlockWays.SeenTogether(Unlock.StopsSpawning));
        }

        [Fact]
        public void WhatTheGameShowsComesWithAThingOnlyOnceTheGameListsItsRecipe()
        {
            // The crafting menu and the hammer list a recipe only once the character knows it (the
            // station and every material known); an item's card names no station. So a recipe's
            // rows are the game's showing only while the game lists that recipe, read live from the
            // character; a row with no recipe of its own (a store's wares) is shown always.
            var world = new World().Rule(CompendiumRule.Strict);
            var explorer = world.Explorer;
            Assert.True(explorer.RecipeListed(null));
            Assert.True(explorer.RecipeListed("$item_bloodpudding"));
            var listed = new HashSet<string> { "$item_bloodpudding" };
            explorer.RecipeListedBy = token => listed.Contains(token);
            Assert.True(explorer.RecipeListed("$item_bloodpudding"));
            Assert.False(explorer.RecipeListed("$item_troll_leather_chest"));
            Assert.True(explorer.RecipeListed(null));
            explorer.Select(world.Troll);
            Assert.False(explorer.LeavesOut(world.Hide.Key, explorer.RecipeListed("$item_bloodpudding")));
            Assert.True(explorer.LeavesOut(world.Hide.Key, explorer.RecipeListed("$item_troll_leather_chest")));
        }

        [Fact]
        public void AFactTheGameShowsNamesWhatWasMetWhateverWasSeenTogether()
        {
            // The game's menus show a recipe's materials and station, a piece's cost and tool and
            // what a known thing is made into the moment the thing is known: such a fact names what
            // the character has met under either rules, never waiting to be seen together, and
            // leaves out only what was never met.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.True(world.Explorer.LeavesOut(world.Hide.Key));
            Assert.False(world.Explorer.LeavesOut(world.Hide.Key, gameShown: true));
            Assert.False(world.Explorer.LeavesOut(world.Boar.Key, gameShown: true));
            Assert.True(world.Explorer.LeavesOut(world.Log.Key, gameShown: true));
            Assert.True(world.Explorer.LeavesOutAll(new[] { world.Hide.Key }));
            Assert.False(world.Explorer.LeavesOutAll(new[] { world.Hide.Key }, gameShown: true));
            Assert.True(world.Explorer.LeavesOutAll(new[] { world.Log.Key }, gameShown: true));

            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.LeavesOut(world.Hide.Key, gameShown: true));
            Assert.True(world.Explorer.LeavesOut(world.Log.Key, gameShown: true));
        }

        [Fact]
        public void UnderStrictASearchFindsByWhatTheGameShowsWithoutAPair()
        {
            // What a thing needs is in the game's crafting menu, so a needs: term finds a known
            // thing by a material met; what it drops waits to be seen fall.
            var world = new World();
            world.Hide.AddTermLink("needs", world.Coins);
            world.Rule(CompendiumRule.Strict);

            world.Explorer.Text = "needs:coins";
            Assert.Contains(world.Hide, world.Explorer.Results);
            world.Explorer.Text = "drops:coins";
            Assert.DoesNotContain(world.Troll, world.Explorer.Results);

            // Never by a material not met.
            world.Hide.AddTermLink("needs", world.Log);
            world.Explorer.Text = "needs:troll_log";
            Assert.DoesNotContain(world.Hide, world.Explorer.Results);
        }

        [Fact]
        public void SeeingTwoThingsGoTogetherTellsNoLevelAgain()
        {
            // A pair seen in play makes nothing more or less known, so the whole catalog is not
            // gone through again for it, as it is for something learned.
            var world = new World().Rule(CompendiumRule.Strict);
            var version = world.View.Version;

            world.Book.Witness(world.Coins.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(version, world.View.Version);

            world.Book.Raise(world.Squeal.Key, KnownLevel.Known);
            world.Rule(CompendiumRule.Strict);
            Assert.NotEqual(version, world.View.Version);
        }

        [Fact]
        public void UnderStrictAPairSeenShowsAtOnceOnThePageOpenAndInSearch()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);
            Assert.True(world.Explorer.LeavesOut(world.Coins.Key));
            var stamp = world.Explorer.KnowStamp;

            world.Book.Witness(world.Coins.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Strict);

            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));
            // What the panel keeps of the page by the explorer's stamp is made again.
            Assert.NotEqual(stamp, world.Explorer.KnowStamp);

            world.Explorer.Text = "drops:trollhide";
            Assert.DoesNotContain(world.Troll, world.Explorer.Results);
            world.Book.Witness(world.Hide.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Strict);
            Assert.Contains(world.Troll, world.Explorer.Results);
        }

        [Fact]
        public void UnderLooseAPairSeenChangesNothingShown()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var stamp = world.Explorer.KnowStamp;

            world.Book.Witness(world.Coins.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Loose);

            Assert.Equal(stamp, world.Explorer.KnowStamp);
        }

        [Fact]
        public void APartsPageKeepsTheOwnerItIsPartOf()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Grunt);

            Assert.True(world.Explorer.LeavingOut);
            Assert.False(world.Explorer.LeavesOut(world.Troll.Key));
            Assert.True(world.Explorer.LeavesOut(world.Coins.Key));
        }

        [Fact]
        public void UnderStrictASearchFindsAKnownEntryOnlyByWhatWasSeenGoTogether()
        {
            // The troll was seen to drop its hide, never coins; its kind and parts are plain to see.
            var world = new World();
            world.Troll.Biomes = new[] { "BlackForest", "Mountain" };
            world.Troll.Components = new[] { "Character" };
            var forest = E("BlackForest", Kind.Biome);
            var all = new[] { world.Troll, world.Hide, world.Coins, world.Grunt, forest }.ToList();
            var explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            world.Book.Witness(world.Troll.Key, world.Hide.Key);
            world.Book.Witness(world.Troll.Key, forest.Key);
            // A biome a creature was met in was entered, so known.
            world.Book.Raise(forest.Key, KnownLevel.Known);
            view.Tell(world.Book, CompendiumRule.Strict);
            explorer.Know(view);

            explorer.Text = "drops:trollhide";
            Assert.Contains(world.Troll, explorer.Results);
            explorer.Text = "drops:coins";
            Assert.DoesNotContain(world.Troll, explorer.Results);
            explorer.Text = "biome:blackforest";
            Assert.Contains(world.Troll, explorer.Results);
            explorer.Text = "biome:mountain";
            Assert.DoesNotContain(world.Troll, explorer.Results);
            explorer.Text = "has:character";
            Assert.Contains(world.Troll, explorer.Results);
            // A sound is listed only while peeking, the compendium listing the world a player meets.
            explorer.Peeking = true;
            explorer.Text = "playedby:troll";
            Assert.Contains(world.Grunt, explorer.Results);
            explorer.Peeking = false;

            // Below the floor it is found as the loose rules find it.
            view.Milestones(new Dictionary<string, int> { { world.Troll.Key, 2 } }, 2);
            view.Tell(world.Book, CompendiumRule.Strict);
            explorer.Know(view);
            explorer.Text = "drops:coins";
            Assert.Contains(world.Troll, explorer.Results);
        }

        [Fact]
        public void LooseKeepsWhatWasMetAndOffAndAClosedPageLeaveNothingOut()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Troll);
            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));

            world.Rule(CompendiumRule.Off);
            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));

            // The boar, only seen: its page is closed, nothing on it to leave out.
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Boar);
            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));
        }

        [Fact]
        public void AModsPageAndTheModThatAddedSomethingAreNeverLeftOut()
        {
            // A mod is always known; no pair is ever seen with one.
            var world = new World();
            var mod = E("Monstrum", Kind.Mod);
            var all = new[] { world.Troll, world.Coins, mod }.ToList();
            var explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            view.Tell(world.Book, CompendiumRule.Strict);
            explorer.Know(view);

            explorer.Select(world.Troll);
            Assert.True(explorer.LeavingOut);
            Assert.False(explorer.LeavesOut(mod.Key));
            Assert.True(explorer.LeavesOut(world.Coins.Key));

            explorer.Select(mod);
            Assert.False(explorer.LeavingOut);
            Assert.False(explorer.LeavesOut(world.Coins.Key));
        }

        [Fact]
        public void AStrictPageShownWholeByTheFloorSaysWhy()
        {
            // Kept back as the rules say, it is shown whole by nothing; below the floor, by the floor.
            var world = new World();
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);
            Assert.False(world.Explorer.ShownWhole(out _));
            world.View.Milestones(new Dictionary<string, int> { { world.Troll.Key, 2 } }, 2);
            world.Rule(CompendiumRule.Strict);
            Assert.True(world.Explorer.ShownWhole(out var source));
            Assert.Null(source);
            // Only the strict rules keep back, so only they show a page whole by a choice.
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.ShownWhole(out _));
        }

        [Fact]
        public void BelowTheFloorAPageShowsAsUnderTheLooseRules()
        {
            var world = new World();
            world.View.Milestones(new Dictionary<string, int> { { world.Troll.Key, 2 } }, 2);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.False(world.Explorer.LeavingOut);
            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));
        }

        [Fact]
        public void OnlyAKnownOwnerBringsItsPartsAlongUnderEitherRules()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, world.View.Of(world.Squeal));

            world.Rule(CompendiumRule.Strict);
            Assert.Equal(KnownLevel.Unknown, world.View.Of(world.Squeal));
            Assert.Equal(KnownLevel.Known, world.View.Of(world.Grunt));
            Assert.True(world.View.Owns(world.Troll, world.Grunt));
            Assert.False(world.View.Owns(world.Grunt, world.Troll));
        }

        [Fact]
        public void WhatWasSeenGoTogetherIsKeptEitherWayRound()
        {
            var book = new CompendiumBook();
            Assert.True(book.Witness("Troll", "TrollHide"));
            Assert.True(book.Witnessed("TrollHide", "Troll"));
            Assert.False(book.Witnessed("Troll", "Coins"));
            book.Save();

            Assert.False(book.Witness("TrollHide", "Troll"));
            Assert.False(book.Changed);
            Assert.False(book.Witness("Troll", "Troll"));
            Assert.False(book.Witness("", "Troll"));

            // A new pair is noticed by what shows the strict rules, without telling every level again.
            var version = book.Version;
            var pairs = book.PairsVersion;
            book.Witness("Odd\tname", "Troll");
            Assert.NotEqual(pairs, book.PairsVersion);
            Assert.Equal(version, book.Version);
            var back = CompendiumBook.Load(book.Save());
            Assert.True(back.Witnessed("Troll", "TrollHide"));
            Assert.True(back.Witnessed("Troll", "Odd\tname"));
            Assert.Equal(2, back.Pairs);
            Assert.Equal(0, CompendiumBook.Load("saw\tTroll\nsaw\t\tTroll\nsaw\tTroll\tTroll").Pairs);
        }
    }
}
