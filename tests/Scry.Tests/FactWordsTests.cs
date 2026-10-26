using System.IO;
using Xunit;

namespace Scry.Tests
{
    public class FactWordsTests
    {
        // What a page says around its facts: the parts that could not be read, values shown as
        // they are, and the links a value carries that play music or open a website rather than
        // going to an entry.

        [Fact]
        public void APartThatCouldNotBeReadIsNamedAtTheEnd()
        {
            Assert.Equal("the drops details", FactWords.Part("drops"));
            Assert.Equal("facts drops", FactWords.PartTiming("drops"));
            Assert.Equal("the drops, recipe details could not be read (the log says why)", FactWords.NotShown(new[] { "drops", "recipe" }));
        }

        [Fact]
        public void AValueIsShownAsItIs()
        {
            Assert.Equal("1,234.5", FactWords.Value(1234.5f));
            Assert.Equal("1,500", FactWords.Value(1500));
            Assert.Equal("yes", FactWords.Value(true));
            Assert.Equal("no", FactWords.Value(false));
            Assert.Equal("status effect", FactWords.Value(Kind.StatusEffect));
            Assert.Equal("Moder", FactWords.Value("Moder"));
        }

        [Fact]
        public void AChoiceIsShownByItsNameAndNotAtAllByANumber()
        {
            Assert.Equal("Status effect", FactWords.Choice(Kind.StatusEffect));
            Assert.Equal("Read only, Hidden", FactWords.Choice(FileAttributes.ReadOnly | FileAttributes.Hidden));
            Assert.Null(FactWords.Choice((Kind)999));
            Assert.Null(FactWords.Value((Kind)999));
        }

        [Fact]
        public void MusicIsPlayedByTheEntrysOwnLinkOrByName()
        {
            Assert.True(EntryKeys.PlaysMusic(EntryKeys.PlayMusic, out var own));
            Assert.Null(own);
            Assert.True(EntryKeys.PlaysMusic(EntryKeys.PlayMusicNamed("Meadows_day"), out var named));
            Assert.Equal("Meadows_day", named);
            Assert.False(EntryKeys.PlaysMusic("se:Rested", out _));

            // An attack's line in a creature's attacks table plays its animation, by the item it is made with.
            Assert.True(EntryKeys.PlaysAttack(EntryKeys.PlayAttack("troll_groundslam"), out var item, out var second));
            Assert.Equal("troll_groundslam", item);
            Assert.False(second);
            Assert.False(EntryKeys.PlaysAttack("troll_groundslam", out _, out _));
            Assert.False(EntryKeys.PlaysMusic(EntryKeys.PlayAttack("troll_groundslam"), out _));
            // A weapon's second attack, played on the person wearing it.
            Assert.True(EntryKeys.PlaysAttack(EntryKeys.PlayAttack("AtgeirBlackmetal", secondary: true), out item, out second));
            Assert.Equal("AtgeirBlackmetal", item);
            Assert.True(second);
            Assert.Equal("Play this attack", ClipWords.PlayAttackTip(known: true, playing: false));
            Assert.Equal("Stop this attack", ClipWords.PlayAttackTip(known: true, playing: true));
            Assert.Equal("Its animations are still being worked out; this plays once they are", ClipWords.PlayAttackTip(known: false, playing: false));
            // Once they are worked out, an attack with no clip of its own is no link, and says so on
            // its line, its tip telling why: the Seeker Queen's slap starts by a trigger its animator
            // lacks, the Writhan's blast by one that leads nowhere (Attack.Start pulls it all the same).
            Assert.Equal("Slap  ·  no animation", ClipWords.NoAnimation("Slap"));
            Assert.Equal("The game starts it by \"attack_slash0\", which this creature's animator does not have, so it plays no animation",
                ClipWords.NoAnimationTip("attack_slash0", inAnimator: false));
            Assert.Equal("The game starts it by \"attack_explode_test\", which leads this creature's animator to no animation",
                ClipWords.NoAnimationTip("attack_explode_test", inAnimator: true));
            Assert.Equal("No animation of its own was found for this attack", ClipWords.NoAnimationTip(null, inAnimator: false));
            // Under the strict rules an attack's clips wait as one chip counting them.
            Assert.Equal("1 clip waits  \u00B7  see it attack, up close or taken", ClipWords.ClipsWait(1));
            Assert.Equal("6 clips wait  \u00B7  see it attack, up close or taken", ClipWords.ClipsWait(6));
            // The attacks table folds the attacks not yet seen or taken into one line the same way.
            Assert.Equal("1 attack waits  \u00B7  see it attack, up close or taken", CombatWords.AttacksWait(1));
            Assert.Equal("3 attacks wait  \u00B7  see it attack, up close or taken", CombatWords.AttacksWait(3));
            // A weapon shown on its own is put on the person first.
            Assert.Equal("Put it on the person and play this attack", ClipWords.PlayAttackTip(known: false, playing: false, wearFirst: true));
            Assert.False(EntryKeys.PlaysMusic(null, out _));
        }

        [Fact]
        public void AWebsiteLinkOpensItsAddress()
        {
            Assert.Equal("https://example.org/mod", EntryKeys.WebsiteOf(EntryKeys.Website("https://example.org/mod")));
            Assert.Null(EntryKeys.WebsiteOf("se:Rested"));
            Assert.Null(EntryKeys.WebsiteOf(null));
        }


        [Fact]
        public void ADoorSaysItsKeyAndWhetherOpeningUsesItUp()
        {
            Assert.Equal("Crypt key, used up", BuildWords.OpenedWith("Crypt key", usedUp: true));
            Assert.Equal("Crypt key", BuildWords.OpenedWith("Crypt key", usedUp: false));
        }

        [Fact]
        public void ATrophySaysThePowerItGivesOnItsBossStone() => Assert.Equal("gives Eikthyr", ItemWords.Gives("Eikthyr"));

        [Fact]
        public void AnyTextCanBeginWithACapital()
        {
            Assert.Equal("Health ×2", Naming.Capital("health ×2"));
            Assert.Equal("", Naming.Capital(""));
            Assert.Null(Naming.Capital(null));
        }
    }
}
