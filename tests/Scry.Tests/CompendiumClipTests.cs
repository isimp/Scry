using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    /// <summary>
    /// A creature's attack animations under the strict rules: a clip of an attack waits for the
    /// attack's own proof, the one its damage waits for, which a hit taken or the attack seen
    /// begun up close gives; and which attack a creature's animation trigger starts.
    /// </summary>
    public class CompendiumClipTests
    {
        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature);
            public readonly Entry Boar = E("Boar", Kind.Creature);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                var all = new[] { Troll, Boar }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                foreach (var entry in all) Book.Raise(entry.Key, KnownLevel.Known);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void AnAttacksClipWaitsForTheAttackAsItsDamageDoes()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);
            Assert.True(world.Explorer.KeepsBackAttackClip("troll_punch"));
            Assert.True(world.Explorer.KeepsBackAttackClip("troll_throw"));
            Assert.False(world.Explorer.KeepsBackAttackClip(null));

            // The proof a hit taken or a swing seen up close gives frees that attack's clips alone.
            world.Book.FoundOut(world.Troll.Key, EntryKeys.Attack("troll_punch"));
            Assert.False(world.Explorer.KeepsBackAttackClip("troll_punch"));
            Assert.True(world.Explorer.KeepsBackAttackClip("troll_throw"));

            // Peeking, the loose rules and the compendium off keep nothing back.
            world.Explorer.PeekPage();
            Assert.False(world.Explorer.KeepsBackAttackClip("troll_throw"));
            world.Explorer.Peeking = false;
            Assert.True(world.Explorer.KeepsBackAttackClip("troll_throw"));
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.KeepsBackAttackClip("troll_throw"));
            world.Rule(CompendiumRule.Off);
            Assert.False(world.Explorer.KeepsBackAttackClip("troll_throw"));
        }

        [Fact]
        public void ATriggerNamesTheAttackItemItStartsUnlessTwoItemsShareIt()
        {
            // The game starts an attack by the trigger its item names, a chain's later steps by
            // that trigger numbered; two items of one creature going by one trigger tell neither.
            var troll = AttackTriggers.Map(new[] { ("troll_punch", "punch", 0), ("troll_log_swing_h", "swing_logh", 1), ("troll_throw", "throw", 0), ("troll_groundslam", "attack", 0) });
            Assert.Equal("troll_punch", AttackTriggers.ItemFor(troll, "punch"));
            Assert.Equal("troll_log_swing_h", AttackTriggers.ItemFor(troll, "swing_logh"));
            Assert.Equal("troll_groundslam", AttackTriggers.ItemFor(troll, "attack"));
            Assert.Null(AttackTriggers.ItemFor(troll, "attack1"));
            Assert.Null(AttackTriggers.ItemFor(troll, "walk"));
            Assert.Null(AttackTriggers.ItemFor(troll, null));
            Assert.Null(AttackTriggers.ItemFor(null, "punch"));

            var chain = AttackTriggers.Map(new[] { ("skeleton_sword", "attack", 3), ("skeleton_bow", "attack_bow", 0) });
            Assert.Equal("skeleton_sword", AttackTriggers.ItemFor(chain, "attack"));
            Assert.Equal("skeleton_sword", AttackTriggers.ItemFor(chain, "attack1"));
            Assert.Equal("skeleton_sword", AttackTriggers.ItemFor(chain, "attack2"));
            Assert.Null(AttackTriggers.ItemFor(chain, "attack3"));
            Assert.Equal("skeleton_bow", AttackTriggers.ItemFor(chain, "attack_bow"));

            var draugr = AttackTriggers.Map(new[] { ("draugr_axe", "attack", 3), ("draugr_sword", "attack", 3), ("draugr_bow", "attack_bow", 0), ("", "attack", 0), ("draugr_kick", "", 0) });
            Assert.Null(AttackTriggers.ItemFor(draugr, "attack"));
            Assert.Null(AttackTriggers.ItemFor(draugr, "attack1"));
            Assert.Equal("draugr_bow", AttackTriggers.ItemFor(draugr, "attack_bow"));
        }
    }
}
