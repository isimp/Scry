using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class PanelWordingTests
    {
        // What the panel says around what it shows: where a click goes, a section's or group's
        // heading with its count and whether it is folded, the two states of a toggle, the list's
        // notes, the details card, the stage's chips and rulers, and the out list.

        [Fact]
        public void ALinkSaysWhereItGoes()
        {
            Assert.Equal("Go to Troll", PanelWords.GoTo("Troll"));
            Assert.Equal("Go to Troll\n(playing now)", PanelWords.GoTo("Troll", playingNow: true));
            Assert.Equal("Troll  ›", PanelWords.GoArrow("Troll"));
            Assert.Equal("Crypt hall\nClick to go to it", StageWords.RoomTip("Crypt hall", canGo: true));
            Assert.Equal("Crypt hall", StageWords.RoomTip("Crypt hall", canGo: false));
            Assert.Equal("Search for what is found in Burial chambers", LocationWords.SearchFoundIn("Burial chambers"));
        }

        [Fact]
        public void AHeadingCountsWhatItHoldsAndSaysWhetherItIsFolded()
        {
            Assert.Equal("EFFECTS  1,200", PanelWords.Heading("EFFECTS", 1200));
            Assert.Equal("EFFECTS  3  ▸", PanelWords.SectionTitle("EFFECTS  3", folded: true));
            Assert.Equal("EFFECTS  3  ▾", PanelWords.SectionTitle("EFFECTS  3", folded: false));
            Assert.Equal("Open this section\nShift-click: every section", PanelWords.SectionTip(folded: true));
            Assert.Equal("Fold this section away\nShift-click: every section", PanelWords.SectionTip(folded: false));
            Assert.Equal("▸ Meadows  3", ListWords.Group("Meadows  3", folded: true));
            Assert.Equal("▾ Meadows  3", ListWords.Group("Meadows  3", folded: false));
            Assert.Equal("Show this group", ListWords.GroupTip(folded: true));
            Assert.Equal("Fold this group away", ListWords.GroupTip(folded: false));
        }

        [Fact]
        public void FoldingEverySectionSaysWhichWayItGoes()
        {
            Assert.Equal("fold all", PanelWords.FoldAll(anyOpen: true));
            Assert.Equal("open all", PanelWords.FoldAll(anyOpen: false));
            Assert.Equal("Fold every section away", PanelWords.FoldAllTip(anyOpen: true));
            Assert.Equal("Open every section", PanelWords.FoldAllTip(anyOpen: false));
        }

        [Fact]
        public void AToggleSaysItsState()
        {
            Assert.Equal("On", PanelWords.OnOff(true));
            Assert.Equal("Off", PanelWords.OnOff(false));
            Assert.Equal("Resume", PanelWords.Pause(paused: true));
            Assert.Equal("Pause", PanelWords.Pause(paused: false));
            Assert.Equal("Full view", PanelWords.ViewButton(compact: true));
            Assert.Equal("Compact", PanelWords.ViewButton(compact: false));
            Assert.Equal("Stop it", MusicWords.Tip(playing: true));
            Assert.Equal("Play it", MusicWords.Tip(playing: false));
            Assert.Equal("Turning; click to hold it still", StageWords.SpinTip(spinning: true));
            Assert.Equal("Held still; click to turn it", StageWords.SpinTip(spinning: false));
        }

        [Fact]
        public void WaitingShowsItGoesOnWithDots()
        {
            Assert.Equal("Finding the clips it goes with.", PanelWords.Waiting("Finding the clips it goes with", 0f));
            Assert.Equal("Finding the clips it goes with..", PanelWords.Waiting("Finding the clips it goes with", 0.4f));
            Assert.Equal("Finding the clips it goes with...", PanelWords.Waiting("Finding the clips it goes with", 0.7f));
            Assert.Equal("Finding the clips it goes with.", PanelWords.Waiting("Finding the clips it goes with", 1.1f));
        }

        [Fact]
        public void SmallLinesSayWhatTheyAre()
        {
            Assert.Equal("Copied \"Troll\".", PanelWords.Copied("Troll"));
            Assert.Equal("In club:", PanelWords.In("club"));
            Assert.Equal("12 more", PanelWords.More(12));
            Assert.Equal("Lasts 40 s", PanelWords.Lasts(40));
            Assert.Equal("Play hit", PanelWords.PlayList("Hit"));
            Assert.Equal("12.3 ms", MonitorWords.Milliseconds(12.34));
        }

        [Fact]
        public void ThingsOfOneNameInARowAreToldApartByTheirPrefabs()
        {
            var named = Naming.TellApart(new[] { ("bjorn bite", "bjorn_bite"), ("bjorn bite", "bjorn_slam"), ("Club", "Club"), ("Troll", "Troll") });
            Assert.Equal(new[] { "bjorn bite (bjorn_bite)", "bjorn bite (bjorn_slam)", "Club", "Troll" }, named);
            Assert.Equal(new[] { "Troll", "Troll" }, Naming.TellApart(new[] { ("Troll", "Troll"), ("Troll", "Troll") }));
            Assert.Equal(new[] { "bjorn bite", "bjorn bite" }, Naming.TellApart(new[] { ("bjorn bite", "bjorn_bite"), ("bjorn bite", "bjorn_bite") }));
            Assert.Empty(Naming.TellApart(new (string, string)[0]));
        }

        [Fact]
        public void ANoteStaysLongEnoughToBeRead()
        {
            Assert.Equal(3.0f, NoteTime.Seconds("Copied \"Bjorn\"."), 3);
            Assert.Equal(2.5f, NoteTime.Seconds(""), 3);
            Assert.Equal(2.5f, NoteTime.Seconds(null), 3);
            Assert.True(NoteTime.Seconds("Playing Black forest location music; click it again to stop it.") > NoteTime.Seconds("Copied \"Bjorn\"."));
            Assert.Equal(10f, NoteTime.Seconds(string.Join(" ", new string('w', 1).PadRight(400, 'w').ToCharArray())), 3);
        }

        [Fact]
        public void TipsHoldTheirLinesOneUnderAnother()
        {
            Assert.Equal("a\nb", Naming.Lines("a", null, "b", ""));
            Assert.Equal("a", Naming.Lines("a"));
            Assert.Equal("x\ny", Naming.Lines(new List<string> { "x", "y" }));
            Assert.Equal("Hello.", Naming.Sentence("hello"));
        }

        [Fact]
        public void TheFootHintNamesOnlyTheKeysTheViewAndSettingsAllow()
        {
            Assert.Equal("Enter plays  ·  Ctrl+F searches  ·  Esc leaves a box", PanelWords.FootHint(full: true, walk: false, look: false));
            Assert.Equal("Keys walk when not typing  ·  right-drag outside to look", PanelWords.FootHint(full: false, walk: true, look: true));
            Assert.Equal("Enter plays", PanelWords.FootHint(full: false, walk: false, look: false));
        }

        [Fact]
        public void TheListsControlsSayWhatTheyShow()
        {
            Assert.Equal("Showing only favourites", ListWords.FavouritesTip(on: true));
            Assert.Equal("Show only favourites", ListWords.FavouritesTip(on: false));
            Assert.Equal("Showing what you looked at last, newest first", ListWords.RecentTip(on: true));
            Assert.Equal("Show what you looked at last, newest first", ListWords.RecentTip(on: false));
            Assert.Equal("Bring the list back", ListWords.ListButtonTip(hidden: true));
            Assert.Equal("Fold the list away, leaving the room to the details", ListWords.ListButtonTip(hidden: false));
            Assert.Equal("›", ListWords.ShowList(upright: true));
            Assert.Equal("Show the list  ▾", ListWords.ShowList(upright: false));
            Assert.Equal("Items  <color=#5a4526>1,200</color>", ListWords.Tab("Items", 1200, "5a4526"));
        }

        [Fact]
        public void AnEmptyListSaysWhy()
        {
            Assert.Equal("No favourites yet. Star something to keep it here.", ListWords.Nothing(noFavourites: true, nothingRecent: true));
            Assert.Equal("Nothing looked at yet. What you select is kept here.", ListWords.Nothing(noFavourites: false, nothingRecent: true));
            Assert.Equal("Nothing matches.", ListWords.Nothing(noFavourites: false, nothingRecent: false));
            Assert.Equal("Nothing in Items, showing all 30", ListWords.NothingIn("Items", 30));
            Assert.StartsWith("Reading this world's locations", LocationWords.NoPlacesYet(reading: true));
            Assert.StartsWith("Nothing is known to be in a location yet", LocationWords.NoPlacesYet(reading: false));
            Assert.Equal("What 1 mod adds", ModReportWords.Bar(1));
            Assert.Equal("What 3 mods add", ModReportWords.Bar(3));
            Assert.Equal("Hooks into what drop tables, chests and plants give.", ModReportWords.HooksLine(new[] { HookedRule.Loot }));
        }

        [Fact]
        public void TheTitleSaysWhereAThingComesFrom()
        {
            Assert.Equal("a mod loaded", DetailWords.From(new Entry { Name = "Bamboo", Kind = Kind.Mod }));
            Assert.Equal("from the game", DetailWords.From(new Entry { Name = "Troll", Kind = Kind.Creature, Origin = Origin.Vanilla }));
            Assert.Equal("", DetailWords.From(new Entry { Name = "Troll", Kind = Kind.Creature, Origin = Origin.Unknown }));
            Assert.Equal("added by a mod", DetailWords.From(new Entry { Name = "Bamboo_stick", Kind = Kind.Item, Origin = Origin.Mod }));
            Assert.Equal("added by Bamboozled", DetailWords.From(new Entry { Name = "Bamboo_stick", Kind = Kind.Item, Origin = Origin.Mod, ModName = "Bamboozled" }));
            Assert.Equal(UnsureWords.Marked("added by Bamboozled"),
                DetailWords.From(new Entry { Name = "Bamboo_stick", Kind = Kind.Item, Origin = Origin.Mod, ModName = "Bamboozled", ModClue = "its scripts" }));
            Assert.Equal("Bamboo_stick   ·   added by Bamboozled", DetailWords.Sub("Bamboo_stick", "added by Bamboozled"));
            Assert.Equal("Bamboo_stick", DetailWords.Sub("Bamboo_stick", ""));
            Assert.Equal("Go to Bamboozled: what it adds and changes", DetailWords.ModTip(null, "Bamboozled", known: true));
            Assert.Equal("clue\nShow everything Bamboozled added", DetailWords.ModTip("clue", "Bamboozled", known: false));
            Assert.Equal("Remove from favourites", DetailWords.StarTip(favourite: true));
            Assert.Equal("Add to favourites", DetailWords.StarTip(favourite: false));
        }

        [Fact]
        public void TheDetailsCardNamesThePrefabItsOriginAndWhatItIsMadeOf()
        {
            Assert.Equal("Prefab name: Troll", DetailWords.PrefabName("Troll"));
            Assert.Equal("Origin: the game", DetailWords.OriginLine(new Entry { Name = "Troll", Origin = Origin.Vanilla }));
            Assert.Equal("Origin: Bamboozled", DetailWords.OriginLine(new Entry { Name = "Bamboo_stick", Origin = Origin.Mod, ModName = "Bamboozled" }));
            Assert.Equal("Origin: a mod, not named", DetailWords.OriginLine(new Entry { Name = "Bamboo_stick", Origin = Origin.Mod }));
            Assert.Equal("Origin: unknown", DetailWords.OriginLine(new Entry { Name = "Troll", Origin = Origin.Unknown }));
            Assert.Equal("Star looks: 2", DetailWords.StarLooks(2));
            Assert.Equal("Made of: Animator, Collider ×3", DetailWords.MadeOf("Animator, Collider ×3"));
            Assert.Equal("3  Wood", DetailWords.Amounted("3", "Wood"));
            Assert.Equal("Wood", DetailWords.Amounted("", "Wood"));
        }

        [Fact]
        public void TheConsoleCommandSaysWhatItDoesAndWhereToPasteIt()
        {
            Assert.Equal("GIVE COMMAND", DetailWords.CommandHeading(give: true));
            Assert.Equal("SPAWN COMMAND", DetailWords.CommandHeading(give: false));
            Assert.Equal("Copied \"spawn Troll 1 1\". Paste it into the console (F5).", DetailWords.CopiedCommand("spawn Troll 1 1"));
        }

        [Fact]
        public void ACardSaysWhatAThingWithNothingOnTheStageIs()
        {
            Assert.Equal("One weather", BiomeWords.WeatherCount(1));
            Assert.Equal("4 weathers", BiomeWords.WeatherCount(4));
            Assert.Equal("Brings Troll, Greydwarf", RaidWords.Brings(new[] { "Troll", "Greydwarf" }));
            Assert.Equal("Brings nothing", RaidWords.Brings(new string[0]));
            Assert.Equal("Lasts 40 s", StatusEffectWords.Card(40f));
            Assert.Equal("No time limit of its own", StatusEffectWords.Card(0f));
            Assert.Equal("Loops", SoundWords.Clips(0, 0f, loops: true));
            Assert.Equal("No clips found", SoundWords.Clips(0, 0f, loops: false));
            Assert.Equal("1 clip, 2.4 s", SoundWords.Clips(1, 2.4f, loops: false));
            Assert.Equal("3 clips, one picked at random, 12.0 s, loops", SoundWords.Clips(3, 12f, loops: true));
        }

        [Fact]
        public void ASoundsVariantsAreNumberedAndTimed()
        {
            Assert.Equal("Play a random one", SoundWords.Play(variants: 3));
            Assert.Equal("Play", SoundWords.Play(variants: 1));
            Assert.Equal("1   sfx_hit_01", SoundWords.Variant(0, "sfx_hit_01"));
            Assert.Equal("sfx_hit_01\n1.23 s", SoundWords.VariantTip("sfx_hit_01", 1.234f));
            Assert.Equal("4.5 s", SoundWords.Clock(4.5f));
            Assert.Equal("1:15", SoundWords.Clock(75f));
        }

        [Fact]
        public void AClipsChipAndTipSayWhatItIsAndHowLong()
        {
            Assert.Equal("Attack1  ·  attack club", ClipWords.Row("Attack1", "attack club"));
            Assert.Equal("Attack Bite", ClipWords.Row("Attack Bite", "attack bite"));
            Assert.Equal("Swim Forward", ClipWords.Row("Swim Forward", "swims"));
            Assert.Equal("Sleeping", ClipWords.Row("Sleeping", "sleeps"));
            Assert.Equal("Swim", ClipWords.Row("Swim", "swims in"));
            Assert.Equal("Idle Swim  ·  in water", ClipWords.Row("Idle Swim", "in water"));
            Assert.Equal("Jump  ·  jumps, by name", ClipWords.Row("Jump", "jumps, by name"));
            Assert.Equal("Attack2  ·  attack club, second", ClipWords.Row("Attack2", "attack club, second"));
            Assert.Equal("Attack1", ClipWords.Row("Attack1", null));
            Assert.Equal("Attack1", ClipWords.Row("Attack1", ""));
            Assert.Equal("▶ Attack1", ClipWords.Now("Attack1"));
            Assert.Equal("Attack1\n1.5 s, loops\nPlaying on its own now", ClipWords.Tip("Attack1", 1.5f, loops: true, now: true));
            Assert.Equal("Attack1\n1.5 s", ClipWords.Tip("Attack1", 1.5f, loops: false, now: false));
            Assert.Equal("1.25 / 2.50 s", ClipWords.Readout(1.25f, 2.5f));
            Assert.Equal("With its clips:", ClipWords.With(2));
            Assert.Equal("With its clip:", ClipWords.With(1));
        }

        [Fact]
        public void ALinkToAnAnimationSaysWhichAndWhatClickingDoes()
        {
            Assert.Equal("Troll · Attack1", LinkWords.Animation("Troll", "Attack1"));
            Assert.Equal("Troll", LinkWords.Animation("Troll", ""));
            Assert.Equal("Go to Troll and play its Attack1 animation", LinkWords.AnimationTip("Troll", "Attack1"));
            Assert.Equal("Go to Troll", LinkWords.AnimationTip("Troll", ""));
        }

        [Fact]
        public void ALinkCarriesItsNoteWhereItIsShort()
        {
            Assert.Equal("Club · when worn", LinkWords.Chip("Club", new[] { "when worn" }));
            Assert.Equal("Club", LinkWords.Chip("Club", new[] { "when worn", "when held" }));
            Assert.Equal("Club", LinkWords.Chip("Club", new[] { "a note far longer than twenty-eight letters" }));
            Assert.Equal("Club", LinkWords.Chip("Club", new string[0]));
            Assert.Equal("Go to Club\nwhen worn\nwhen held", LinkWords.Tip("Club", new[] { "when worn", "when held" }));
            Assert.Equal("Go to Club", LinkWords.Tip("Club", new string[0]));
        }

        [Fact]
        public void TheStageSaysWhatItShowsAndWhatAClickDoes()
        {
            Assert.Equal("Squares of 1 m, lines every 5 m  ·  12 m tall, 2.5 × 3.0 m", StageWords.Grid(2.5f, 12.25f, 3f));
            Assert.Equal("Inside", StageWords.Inside(true));
            Assert.Equal("Outside", StageWords.Inside(false));
            Assert.StartsWith("The example dungeon", StageWords.InsideTip(inside: true));
            Assert.StartsWith("Its entrance", StageWords.InsideTip(inside: false));
            Assert.StartsWith("Puts away the creatures", StageWords.CreaturesTip(shown: true));
            Assert.StartsWith("Shows the creatures", StageWords.CreaturesTip(shown: false));
            Assert.StartsWith("Cut open over a floor", StageWords.RoofTip(cutting: true));
            Assert.StartsWith("Roof on", StageWords.RoofTip(cutting: false));
            Assert.Equal("Floor 2: click to open it", StageWords.FloorTip("Floor 2"));
            Assert.Equal("Click to put the roof on", StageWords.RulerTip(aboveRoof: true));
            Assert.StartsWith("Click or drag to cut here", StageWords.RulerTip(aboveRoof: false));
        }

        [Fact]
        public void AdjustingSaysTheChoicesInFigures()
        {
            Assert.Equal("No stars", CombatWords.StarChoice(0));
            Assert.Equal("1 star", CombatWords.StarChoice(1));
            Assert.Equal("3 stars", CombatWords.StarChoice(3));
            Assert.Equal("43 m/s", ProjectileWords.Speed(42.6f));
            Assert.Equal("Size ×1.50: click to change it", HeaderSliders.SizeTip(1.5f));
            Assert.Equal("Volume " + Numbers.Percent(0.5f) + ": click to change it", HeaderSliders.VolumeTip(0.5f));
        }

        [Fact]
        public void TheOutListCountsWhatIsOutAndWhatDidNotFit()
        {
            Assert.Equal("Clear world", OutWords.ClearWorld(0));
            Assert.Equal("Clear world", OutWords.ClearWorld(1));
            Assert.Equal("Clear world  3", OutWords.ClearWorld(3));
            Assert.Equal("Troll", OutWords.Row("Troll", 1));
            Assert.Equal("Troll  ×3", OutWords.Row("Troll", 3));
            Assert.Equal("and 5 more; Clear world takes them all", OutWords.More(5));
        }

        [Fact]
        public void ADungeonsExampleSaysItIsOneOfMany()
        {
            Assert.Equal("12 rooms. One way it can come out; each world lays out its own.", DungeonWords.ExampleNote("12 rooms", hasRooms: true));
            Assert.Equal("No rooms.", DungeonWords.ExampleNote("No rooms", hasRooms: false));
        }

        [Fact]
        public void AReportOfWhatIsOffNamesTheVersions()
        {
            Assert.Equal("Scry 0.2.0, Valheim 0.221.4: off: location music, drops seen in play",
                OffWords.Report("0.2.0", "0.221.4", new[] { Feature.LocationMusic, Feature.DropsSeenInPlay }));
        }
    }
}
