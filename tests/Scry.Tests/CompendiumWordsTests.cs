using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumWordsTests
    {
        [Fact]
        public void ADamageGridKeepingTypesBackSaysFaintlyHowMany()
        {
            Assert.Equal("1 more not yet hit with", CompendiumWords.NotYetHitWith(1));
            Assert.Equal("3 more not yet hit with", CompendiumWords.NotYetHitWith(3));
            Assert.Null(CompendiumWords.NotYetHitWith(0));
            Assert.Equal("Fire: how it lands shows once you hit it with fire", CompendiumWords.NotYetHitWithTip("Fire"));
        }

        // A page not yet open names something not met only by its kind, and says how much the
        // character knows and what would open it.

        private static readonly Kind[] Counted = { Kind.Creature, Kind.Item, Kind.Piece, Kind.Resource, Kind.Spawner, Kind.StatusEffect, Kind.Location, Kind.Raid, Kind.Biome };

        [Fact]
        public void SomethingNotMetIsNamedOnlyByItsKindEachKindInItsOwnWords()
        {
            Assert.Equal("Unknown creature", CompendiumWords.Unknown(Kind.Creature));
            Assert.Equal("Unknown place", CompendiumWords.Unknown(Kind.Location));
            Assert.Equal(Counted.Length, Counted.Select(CompendiumWords.Unknown).Distinct().Count());
        }

        [Fact]
        public void AKindsTabCountsWhatWasFoundOfAllThereIs()
        {
            Assert.Equal("Creatures  <color=#aabbcc>37/1,112</color>", CompendiumWords.Tab("Creatures", 37, 1112, "aabbcc"));
        }

        [Fact]
        public void AListOfWhatThereIsSaysHowManyOfItWereFound()
        {
            // A mod's page lists what it adds; only what the character has seen is shown.
            Assert.Equal("Adds items (1,200), 3 found", CompendiumWords.Found("Adds items (1,200)", 3));
        }

        [Fact]
        public void TheModeNamesItsRulesAndTellsWhatEachShows()
        {
            Assert.Equal(new[] { "Off", "Loose", "Strict" }, CompendiumWords.Rules.Select(CompendiumWords.RuleName));
            // Captioned as the origin switch is, its rules beside it.
            Assert.Equal("Compendium", CompendiumWords.ModeCaption);
            Assert.Equal("Loose: list only what this character has seen, a page open once it has fought, held, built, used or entered it, and count in each kind's tab what was found of all there is", CompendiumWords.StepTip(CompendiumRule.Loose));
            Assert.Equal("Show only what this character has come to know, by the rules chosen for it here; each character keeps its own", CompendiumWords.ModeTip);
            Assert.Equal("show everything. What this character comes to know is kept all the same, so turning the compendium on later shows everything met meanwhile", CompendiumWords.RuleTip(CompendiumRule.Off));
            Assert.Equal("list only what this character has seen, a page open once it has fought, held, built, used or entered it, and count in each kind's tab what was found of all there is", CompendiumWords.RuleTip(CompendiumRule.Loose));
            Assert.Equal("list the same as Loose, but a page shows of how its entry connects (drops, places, recipes, attacks) only what this character saw go together, keeps back the names of what it has not seen, and the numbers the game never shows until you find them out", CompendiumWords.RuleTip(CompendiumRule.Strict));
        }

        [Fact]
        public void PeekingWarnsThatEverythingShowsAndUntilWhen()
        {
            Assert.Equal("Show everything for now, not only what this character has met; it stops when the panel closes", CompendiumWords.PeekTip(false));
            Assert.Equal("Show only what this character has met again", CompendiumWords.PeekTip(true));
            Assert.Equal("Peeking: everything shows, not only what this character has met, until the panel closes", CompendiumWords.Peeking);
            Assert.Equal("Stop", CompendiumWords.StopPeeking);
        }

        [Fact]
        public void TheNoticeNamesTheFirstThingsLearnedAndCountsTheRest()
        {
            Assert.Equal("Newly learned: Troll", CompendiumWords.NewlyLearned(new[] { "Troll" }));
            Assert.Equal("Newly learned: Troll, Neck and Boar", CompendiumWords.NewlyLearned(new[] { "Troll", "Neck", "Boar" }));
            Assert.Equal("Newly learned: Troll, Neck, Boar and 1,200 more", CompendiumWords.NewlyLearned(new[] { "Troll", "Neck", "Boar" }.Concat(Enumerable.Repeat("x", 1200)).ToArray()));
            Assert.Equal("Show", CompendiumWords.ShowLearned);
            Assert.Equal("Put this away until more is learned", CompendiumWords.PutNewsAway);
        }

        [Fact]
        public void CatchingUpFromTheMapSaysWhatItDidAndWhyNot()
        {
            Assert.Equal("the explored map will be read again for this character in this world", CompendiumWords.MapAgain);
            Assert.Equal("the explored map can be read only in a world", CompendiumWords.MapNoWorld);
            Assert.Equal("caught up from the explored map: 1,200 kinds of place lie in explored ground, 3 of them new to the compendium", CompendiumWords.MapRead(1200, 3, fromServer: false));
            Assert.Equal("caught up from the explored map with the server's answer: 12 kinds of place lie in explored ground, 0 of them new to the compendium", CompendiumWords.MapRead(12, 0, fromServer: true));
            Assert.Equal("answered a player's explored map: 12 kinds of place lie in it", CompendiumWords.MapAnswered(12));
            Assert.Equal("the server did not answer on the explored map (it has no Scry, or one too old): places count as the character meets them", CompendiumWords.MapNoServerScry);
            Assert.Equal("Read the map again", CompendiumWords.ReadMap);
            Assert.Equal("Places in the ground this character explored count as seen, read again each session", CompendiumWords.MapBar);
            Assert.Equal("Counts the kinds of place in the ground this character explored in this world as seen, as on its first visit with Scry", CompendiumWords.ReadMapTip);
            Assert.Equal("on a dedicated server Scry only answers players' explored maps, for their compendium", CompendiumWords.ServerPart);
        }

        [Fact]
        public void TheFloorSaysHowFarItReachesAndWhatRaisingOrLoweringItDoes()
        {
            Assert.Equal("Floor: none", CompendiumWords.Floor(0));
            Assert.Equal("Floor: Black forest", CompendiumWords.Floor(2));
            // In the title row, a caption and the biome stepped between arrows.
            Assert.Equal(("Floor", "none", "Black forest"), (CompendiumWords.FloorCaption, CompendiumWords.FloorName(0), CompendiumWords.FloorName(2)));
            // What it shows that the character never came to know is marked, on its row and page.
            Assert.Equal("Shown by the Floor: Black forest, not yet come to know by this character", CompendiumWords.ByFloor(2));
            // A link to such a thing says so above where it goes.
            Assert.Equal("Shown by the Floor: Black forest, not yet come to know by this character\nGo to Troll", CompendiumWords.FloorTip(2, "Go to Troll"));
            Assert.Equal("Show everything of the Swamp as known: Bonemass fell for this character", CompendiumWords.RaiseFloor(3, "Bonemass"));
            Assert.Equal("Show everything of the Swamp as known", CompendiumWords.RaiseFloor(3, null));
            Assert.Equal("Show the Meadows and what came before as known, not the Black forest", CompendiumWords.LowerFloor(1));
            Assert.Equal("Show only what this character has met again", CompendiumWords.LowerFloor(0));
            Assert.Equal("The next tier opens once The Elder falls for this character", CompendiumWords.FloorWaits("The Elder"));
        }

        [Fact]
        public void ATitleWhoseItemsWereLeftOutLosesItsCount()
        {
            // Under the strict rules nothing tells how much was left out.
            Assert.Equal("Played by", CompendiumWords.Uncounted("Played by (1,200)"));
            Assert.Equal("Drops", CompendiumWords.Uncounted("Drops"));
            Assert.Equal("Raids (in the Plains)", CompendiumWords.Uncounted("Raids (in the Plains)"));
            Assert.Equal("Drops (12a", CompendiumWords.Uncounted("Drops (12a"));
            Assert.Equal("???", CompendiumWords.Hidden);
        }

        [Fact]
        public void WhatTheStrictRulesLeaveOutOfASectionIsToldAtItsEndByWhatTheSectionIsAbout()
        {
            // Drops not seen fall, attacks not seen hit, places a creature lives not met in, givers not seen give it.
            Assert.Equal("2 more not yet seen fall", CompendiumWords.LeftOutOf("loot", 2));
            // What a fight section leaves out is no attack (an attack's damage reads ??? instead): seen together, as anything else.
            Assert.Equal("1 more not yet seen together", CompendiumWords.LeftOutOf("fight", 1));
            Assert.Equal("1 more place it lives not yet met in", CompendiumWords.LeftOutOf("lives", 1));
            Assert.Equal("3 more places it lives not yet met in", CompendiumWords.LeftOutOf("lives", 3));
            Assert.Equal("2 more not yet seen give it", CompendiumWords.LeftOutOf("from", 2));
            // Any other section, or one with no name of its own, says it of being seen together.
            Assert.Equal("4 more not yet seen together", CompendiumWords.LeftOutOf("making", 4));
            Assert.Equal("1,200 more not yet seen together", CompendiumWords.LeftOutOf(null, 1200));
        }

        [Fact]
        public void ASectionTheStrictRulesLeftNothingOutOfTellsNothing()
        {
            Assert.Null(CompendiumWords.LeftOutOf("loot", 0));
            Assert.Null(CompendiumWords.LeftOutOf("lives", -1));
        }

        [Fact]
        public void TheListOfWhatWasLearnedLatelySaysWhatItHolds()
        {
            Assert.Equal("Learned", CompendiumWords.LearnedTab);
            Assert.Equal("Show what this character learned lately, newest first", CompendiumWords.LearnedTip(false));
            Assert.Equal("Showing what this character learned lately, newest first", CompendiumWords.LearnedTip(true));
        }

        [Fact]
        public void AClosedPageSaysHowMuchIsKnownAndWhatWouldOpenIt()
        {
            Assert.Equal("Not met yet", CompendiumWords.Closed(KnownLevel.Unknown));
            Assert.Equal("Heard of, not met yet", CompendiumWords.Closed(KnownLevel.HeardOf));
            Assert.Equal("Seen, not yet come to know", CompendiumWords.Closed(KnownLevel.Seen));
            // Every way that makes it known, by one verb for a page: come to know.
            Assert.Equal("Fight it, tame it or hold its trophy to come to know it.", CompendiumWords.HowToKnow(Kind.Creature));
            Assert.Equal("Hold it, make it, eat it, unlock its recipe or read its card to come to know it.", CompendiumWords.HowToKnow(Kind.Item));
            Assert.Equal("Have it on you or choose it at its stone to come to know it.", CompendiumWords.HowToKnow(Kind.StatusEffect));
            Assert.Equal("It comes along once you come to know what it belongs to.", CompendiumWords.HowToKnow(Kind.Sound));
            Assert.Equal(Counted.Length, Counted.Select(CompendiumWords.HowToKnow).Distinct().Count());
            Assert.All(Counted, kind => Assert.EndsWith("to come to know it.", CompendiumWords.HowToKnow(kind)));
            // Under the strict rules a page known still keeps some back: said too.
            Assert.Equal("Some of its page shows only once you find it out.", CompendiumWords.ClosedStrict);
        }
    }
}
