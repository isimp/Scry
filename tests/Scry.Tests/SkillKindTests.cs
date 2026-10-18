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
        // own, so the compendium never keeps it back, as it never keeps back a mod.

        private static Entry E(string name) => new Entry { Kind = Kind.Skill, Name = name };

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
        public void ASkillIsAlwaysKnownAndItsLinksAreNeverLeftOut()
        {
            var troll = new Entry { Kind = Kind.Creature, Name = "Troll" };
            var swords = E("Swords");
            var view = new CompendiumView(new EntryCatalog(new[] { troll, swords }));
            view.Tell(null, CompendiumRule.Strict);
            Assert.Equal(KnownLevel.Unknown, view.Of(troll));
            Assert.Equal(KnownLevel.Known, view.Of(swords));

            var sword = new Entry { Kind = Kind.Item, Name = "SwordIron" };
            var all = new[] { sword, swords }.ToList();
            var explorer = new Explorer(all, new Favourites(Path.Combine(Path.GetTempPath(), "scry-skill-kind-" + System.Guid.NewGuid().ToString("N"), "favourites.txt")));
            var book = new CompendiumBook();
            book.Raise(sword.Key, KnownLevel.Known);
            var strict = new CompendiumView(explorer.Entries);
            strict.Tell(book, CompendiumRule.Strict);
            explorer.Know(strict);
            explorer.Select(sword);
            Assert.True(explorer.LeavingOut);
            Assert.False(explorer.LeavesOut(swords.Key));
            explorer.Select(swords);
            Assert.False(explorer.LeavingOut);
        }

        [Fact]
        public void ASkillsPageTellsWhatItChangesHowItRisesAndWhatItWorksOn()
        {
            var pairs = new[] { ("Raised by", "skill"), ("Digging the ground", "skill"), ("On death", "skill"), ("In this world", "world settings") };
            var rows = new[] { (SkillPageWords.ChangesTitle, "skill"), (SkillPageWords.TrainedWithTitle, "skill"), (SkillPageWords.AddsLevelsTitle, "skill"), (SkillPageWords.StationsTitle, "skill") };
            var plan = FactLayout.Plan(Kind.Skill, pairs, rows, new[] { FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            Assert.Equal(new[] { "What it changes", "How it rises", "What it works on", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "row By level", "Digging the ground" }, plan[0].Bits.Select(Name));
            Assert.Equal(new[] { "Raised by", "On death", "In this world", "row Effects adding levels" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "row Trained with", "row Speeds making at" }, plan[2].Bits.Select(Name));
        }
    }
}
