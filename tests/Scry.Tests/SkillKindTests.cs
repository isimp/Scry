using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SkillKindTests
    {
        // Every skill the character can have is an entry of its own, a page telling what it
        // changes. It is no prefab, so it has a key of its own ("skill:" and its name in the
        // game's list), a tab of its own, and is listed by what it is for. It is the character's
        // own, so the compendium shows it once the character has learned it, at level 1, and
        // until then neither shows it nor names it anywhere.

        private static Entry E(string name, int type = 0) => new Entry { Kind = Kind.Skill, Name = name, Source = new SkillSource { Type = type, Name = name } };

        private const int Swords = 1;
        private const int Run = 102;

        [Fact]
        public void ASkillIsKeptUnderAKeyOfItsOwn()
        {
            Assert.Equal("skill:Swords", EntryKeys.For(Kind.Skill, "Swords"));
            Assert.Equal("Swords", EntryKeys.Split("skill:Swords", out var kind));
            Assert.Equal(Kind.Skill, kind);
            Assert.True(EntryKeys.HasOwnNamespace(Kind.Skill));
            Assert.Equal("skill:1795429391", E("1795429391").Key);
        }

        [Fact]
        public void SkillsHaveATabAndASearchTermOfTheirOwn()
        {
            Assert.Equal("Skills", Kinds.Label(Kind.Skill));
            Assert.True(Search.KindMatches(Kind.Skill, "skill"));
            Assert.False(Search.KindMatches(Kind.Item, "skill"));
        }

        [Fact]
        public void SkillsAreListedByWhatTheyAreFor()
        {
            var fighting = Groups.Skill("Swords");
            var magic = Groups.Skill("BloodMagic");
            var moving = Groups.Skill("Run");
            var making = Groups.Skill("Crafting");
            var mods = Groups.Skill("1795429391");
            Assert.Equal("Fighting", fighting.Name);
            Assert.Equal(fighting, Groups.Skill("Bows"));
            Assert.Equal(fighting, Groups.Skill("Blocking"));
            Assert.Equal("Magic", magic.Name);
            Assert.Equal(magic, Groups.Skill("ElementalMagic"));
            Assert.Equal("Moving", moving.Name);
            Assert.Equal(moving, Groups.Skill("Ride"));
            Assert.Equal("Gathering and making", making.Name);
            Assert.Equal(making, Groups.Skill("Pickaxes"));
            Assert.Equal(making, Groups.Skill("WoodCutting"));
            Assert.Equal(making, Groups.Skill("Fishing"));
            Assert.Equal("Added by mods", mods.Name);
            Assert.True(fighting.Order < magic.Order && magic.Order < moving.Order && moving.Order < making.Order && making.Order < mods.Order);
        }

        [Fact]
        public void ASkillOffersNoAnimationsAndIsNotCountedLikeTheWorldsThings()
        {
            Assert.False(Kinds.OffersClips(Kind.Skill));
            Assert.False(CompendiumView.Counted(Kind.Skill));
        }

        [Fact]
        public void WithTheCompendiumOnASkillShowsOnlyOnceTheCharacterHasLearnedIt()
        {
            var troll = new Entry { Kind = Kind.Creature, Name = "Troll" };
            var swords = E("Swords", Swords);
            var run = E("Run", Run);
            var view = new CompendiumView(new EntryCatalog(new[] { troll, swords, run }));
            view.SkillsLearned(type => type == Swords, 1);
            foreach (var rule in new[] { CompendiumRule.Loose, CompendiumRule.Strict })
            {
                view.Tell(null, rule);
                Assert.Equal(KnownLevel.Unknown, view.Of(troll));
                Assert.Equal(KnownLevel.Known, view.Of(swords));
                Assert.Equal(KnownLevel.Unknown, view.Of(run));
            }
            view.Tell(null, CompendiumRule.Off);
            Assert.Equal(KnownLevel.Known, view.Of(run));

            // Without the character's skills told, none is learned.
            var none = new CompendiumView(new EntryCatalog(new[] { swords }));
            none.Tell(null, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, none.Of(swords));
        }

        [Fact]
        public void ASkillNotLearnedIsNeitherHeardOfNorBroughtAlongByTheFloor()
        {
            var sword = new Entry { Kind = Kind.Item, Name = "SwordIron" };
            var axe = new Entry { Kind = Kind.Item, Name = "AxeBronze" };
            var run = E("Run", Run);
            var cutting = E("WoodCutting", 104);
            sword.Links.Add(new Link { Group = "Skill", Target = run.Key });
            axe.Links.Add(new Link { Group = "Skill", Target = cutting.Key });
            var view = new CompendiumView(new EntryCatalog(new[] { sword, axe, run, cutting }));
            view.SkillsLearned(_ => false, 1);
            view.Milestones(new Dictionary<string, int> { { sword.Key, 1 }, { run.Key, 1 }, { cutting.Key, 1 } }, 1);
            var book = new CompendiumBook();
            book.Raise(sword.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, view.Of(sword));
            Assert.Equal(KnownLevel.Unknown, view.Of(run));

            // Learned on top, as play goes on, names no skill either.
            book.Raise(axe.Key, KnownLevel.Known);
            Assert.True(view.OnlyLearnedSince(book, CompendiumRule.Loose));
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, view.Of(axe));
            Assert.Equal(KnownLevel.Unknown, view.Of(cutting));
        }

        [Fact]
        public void ASkillLearnedShowsAtTheNextTellingAndAnUnchangedOneTellsNothingAgain()
        {
            var sword = new Entry { Kind = Kind.Item, Name = "SwordIron" };
            var axe = new Entry { Kind = Kind.Item, Name = "AxeBronze" };
            var run = E("Run", Run);
            var view = new CompendiumView(new EntryCatalog(new[] { sword, axe, run }));
            var learned = new HashSet<int>();
            var book = new CompendiumBook();
            book.Raise(sword.Key, KnownLevel.Known);
            view.SkillsLearned(learned.Contains, 1);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, view.Of(run));

            var told = view.Version;
            view.SkillsLearned(learned.Contains, 1);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(told, view.Version);

            // Learned while the book learned more too: told whole, not only what the book raised.
            learned.Add(Run);
            book.Raise(axe.Key, KnownLevel.Known);
            view.SkillsLearned(learned.Contains, 2);
            Assert.False(view.OnlyLearnedSince(book, CompendiumRule.Loose));
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, view.Of(run));
            Assert.Equal(KnownLevel.Known, view.Of(axe));

            // Lost again, as a death can take a level 1 back below it.
            learned.Clear();
            view.SkillsLearned(learned.Contains, 3);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, view.Of(run));
        }

        [Fact]
        public void APageLeavesOutWhatNamesASkillNotLearnedAndKeepsOneLearnedUnderEitherRules()
        {
            var sword = new Entry { Kind = Kind.Item, Name = "SwordIron" };
            var swords = E("Swords", Swords);
            var run = E("Run", Run);
            var explorer = new Explorer(new[] { sword, swords, run }.ToList(), new Favourites(Path.Combine(Path.GetTempPath(), "scry-skill-kind-" + System.Guid.NewGuid().ToString("N"), "favourites.txt")));
            var book = new CompendiumBook();
            book.Raise(sword.Key, KnownLevel.Known);
            var view = new CompendiumView(explorer.Entries);
            view.SkillsLearned(type => type == Swords, 1);
            foreach (var rule in new[] { CompendiumRule.Loose, CompendiumRule.Strict })
            {
                view.Tell(book, rule);
                explorer.Know(view);
                explorer.Select(sword);
                Assert.Equal(rule == CompendiumRule.Strict, explorer.LeavingOut);
                // A fact naming a skill learned stays, never one naming a skill not learned; its link goes nowhere.
                Assert.False(explorer.LeavesOut(swords.Key));
                Assert.True(explorer.LeavesOut(run.Key));
                Assert.True(explorer.Reaches(swords.Key));
                Assert.False(explorer.Reaches(run.Key));
                // A faint line told at a skill shows only at one learned.
                Assert.False(explorer.NotMet(swords.Key));
                Assert.True(explorer.NotMet(run.Key));
                // The Skills tab counts the one learned.
                Assert.Equal(1, explorer.FoundOf(Kind.Skill));
            }
            explorer.Select(swords);
            Assert.False(explorer.LeavingOut);

            // Peeking, the page shown is whole: nothing is left out.
            explorer.Select(sword);
            explorer.Peeking = true;
            Assert.False(explorer.NotMet(run.Key));
            Assert.False(explorer.LeavesOut(run.Key));
            Assert.True(explorer.Reaches(run.Key));
        }

        [Fact]
        public void ASkillsPageTellsWhatItChangesHowItRisesAndWhatItWorksOn()
        {
            var pairs = new[] { ("Trained by", "skill"), ("Digging the ground", "skill"), ("On death", "skill"), ("In this world", "world settings") };
            var rows = new[] { (SkillPageWords.ChangesTitle, "skill"), (SkillPageWords.TrainedWithTitle, "skill"), (SkillPageWords.AddsLevelsTitle, "skill"), (SkillPageWords.StationsTitle, "skill") };
            var plan = FactLayout.Plan(Kind.Skill, pairs, rows, new[] { FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            Assert.Equal(new[] { "What it changes", "How it rises", "What it works on", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "row By level", "Digging the ground" }, plan[0].Bits.Select(Name));
            Assert.Equal(new[] { "Trained by", "On death", "In this world", "row Effects adding levels" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "row Trained with", "row Speeds making at" }, plan[2].Bits.Select(Name));
        }
    }
}
