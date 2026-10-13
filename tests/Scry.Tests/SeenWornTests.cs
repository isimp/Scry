using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SeenWornTests
    {
        // What equipping something did to you in your own play, whichever mod made it so: told on
        // its page by quality, kept from session to session.

        private static WornState State(float carry, float armour, Degree frost = Degree.Normal, params (string Name, string Shown)[] effects)
        {
            var state = new WornState { Carry = carry, Armour = armour };
            state.Resists["Frost"] = frost;
            state.Resists["Fire"] = Degree.Normal;
            foreach (var (name, shown) in effects) state.Effects[name] = shown;
            return state;
        }

        private static readonly Dictionary<string, float> NoModifiers = new Dictionary<string, float>();
        private static readonly string[] NoSets = new string[0];

        [Fact]
        public void APackPutOnTellsWhatItAddedToYou()
        {
            var before = State(300f, 10f, Degree.Normal, ("Rested", "Rested"));
            var after = State(340f, 12f, Degree.Resistant, ("Rested", "Rested"), ("SE_Pack_2", "Arctic Sherpa Pack level 2 effect"));
            var modifiers = new Dictionary<string, float> { ["m_movementModifier"] = -0.05f, ["m_runStaminaModifier"] = 0f };

            var change = WornChange.Between(before, after, modifiers, NoSets);

            Assert.Equal(40f, change.Carry);
            Assert.Equal(2f, change.Armour);
            Assert.Equal(new Dictionary<string, Degree> { ["Frost"] = Degree.Resistant }, change.Resists);
            Assert.Equal(new Dictionary<string, string> { ["SE_Pack_2"] = "Arctic Sherpa Pack level 2 effect" }, change.Effects);
            Assert.Equal(new Dictionary<string, float> { ["m_movementModifier"] = -0.05f }, change.Modifiers);
            Assert.False(change.IsNothing);
        }

        [Fact]
        public void ASetBonusThePieceCompletedIsNotPutDownToIt()
        {
            var before = State(300f, 10f);
            var after = State(300f, 14f, Degree.Normal, ("SetEffect_TrollArmor", "Troll set bonus"));

            var change = WornChange.Between(before, after, NoModifiers, new[] { "SetEffect_TrollArmor" });

            Assert.Empty(change.Effects);
            Assert.Equal(4f, change.Armour);
        }

        [Fact]
        public void AResistanceItTookAwayIsTold()
        {
            var change = WornChange.Between(State(300f, 0f, Degree.Resistant), State(300f, 0f, Degree.Normal), NoModifiers, NoSets);

            Assert.Equal(new Dictionary<string, Degree> { ["Frost"] = Degree.Normal }, change.Resists);
        }

        [Fact]
        public void WhatStayedAsItWasOrMovedByAHairIsNothing()
        {
            Assert.True(WornChange.Between(State(300f, 10f), State(300f, 10f), NoModifiers, NoSets).IsNothing);
            var hair = WornChange.Between(State(300f, 10f), State(300.2f, 10.01f), new Dictionary<string, float> { ["m_movementModifier"] = 0.001f }, NoSets);
            Assert.True(hair.IsNothing);
            Assert.Empty(hair.Modifiers);
            Assert.Equal(0f, hair.Carry);
            Assert.Equal(0f, hair.Armour);
        }

        [Fact]
        public void ArmourAloneIsAChange() =>
            Assert.False(WornChange.Between(State(300f, 10f), State(300f, 11f), NoModifiers, NoSets).IsNothing);

        // Kept by item and quality.

        private static WornChange Pack(float carry) => WornChange.Between(State(300f, 0f), State(300f + carry, 1f), NoModifiers, NoSets);

        [Fact]
        public void EachQualityIsKeptAndTheLatestSeenStands()
        {
            var seen = new SeenWorn();
            seen.Record("BackpackMountains", 2, Pack(40f));
            seen.Record("BackpackMountains", 1, Pack(20f));
            seen.Record("BackpackMountains", 2, Pack(45f));

            var of = seen.Of("BackpackMountains");
            Assert.Equal(new[] { 1, 2 }, of.Select(s => s.Quality));
            Assert.Equal(45f, of[1].Change.Carry);
            Assert.Empty(seen.Of("SwordIron"));
            Assert.Empty(seen.Of(null));
        }

        [Fact]
        public void WhatHasNoItemOrQualityIsNotKept()
        {
            var seen = new SeenWorn();
            seen.Record(null, 1, Pack(40f));
            seen.Record("", 1, Pack(40f));
            seen.Record("BackpackMountains", 0, Pack(40f));
            seen.Record("BackpackMountains", 1, null);

            Assert.False(seen.Changed);
            Assert.Empty(seen.Of("BackpackMountains"));
            Assert.Equal("", seen.Save());
        }

        [Fact]
        public void OnlyADifferenceFromWhatWasKeptIsAChange()
        {
            var seen = new SeenWorn();
            seen.Record("BackpackMountains", 2, Pack(40f));
            Assert.True(seen.Changed);
            seen.Save();
            Assert.False(seen.Changed);

            seen.Record("BackpackMountains", 2, Pack(40f));
            Assert.False(seen.Changed);
            seen.Record("BackpackMountains", 2, Pack(45f));
            Assert.True(seen.Changed);
        }

        [Fact]
        public void WhatWasSeenReadsBackTheSame()
        {
            var seen = new SeenWorn();
            var before = State(300f, 10f);
            var after = State(340.5f, 12f, Degree.VeryResistant, ("SE_Pack", "Pack\tlevel 2"));
            seen.Record("BackpackMountains", 2, WornChange.Between(before, after, new Dictionary<string, float> { ["m_movementModifier"] = -0.05f }, NoSets));
            seen.Record("CapeDeerHide", 1, WornChange.Between(before, before, NoModifiers, NoSets));

            var text = seen.Save();
            var back = SeenWorn.Load(text);

            Assert.Equal(text, back.Save());
            var pack = back.Of("BackpackMountains").Single().Change;
            Assert.Equal(40.5f, pack.Carry);
            Assert.Equal(2f, pack.Armour);
            Assert.Equal(Degree.VeryResistant, pack.Resists["Frost"]);
            Assert.Equal("Pack\tlevel 2", pack.Effects["SE_Pack"]);
            Assert.Equal(-0.05f, pack.Modifiers["m_movementModifier"]);
            Assert.True(back.Of("CapeDeerHide").Single().Change.IsNothing);
            Assert.False(back.Changed);
        }

        [Fact]
        public void ALineThatCannotBeReadIsPassedOver()
        {
            var back = SeenWorn.Load("worn\tA\t1\tseen\nworn\tA\tx\tcarry\t5\nworn\tA\t1\tcarry\tlots\nworn\tA\t1\tresist\tFrost\tSomewhat\n"
                + "worn\tA\t1\tresist\tFire\t42\nworn\tA\t0\tseen\nworn\tC\t1\tseen\textra\nnonsense\nworn\tB\t2\tarmour\t3\n");

            Assert.True(back.Of("A").Single().Change.IsNothing);
            Assert.Equal(3f, back.Of("B").Single().Change.Armour);
            Assert.Empty(back.Of("C"));
            Assert.Empty(SeenWorn.Load(null).Of("A"));
            Assert.Empty(SeenWorn.Load("").Of("A"));
        }

        // How it is told: a table, a column for each quality seen.

        [Fact]
        public void TheTableHasALineForEachThingChangedAndAColumnForEachQualitySeen()
        {
            var before = State(300f, 10f, Degree.Normal);
            var two = WornChange.Between(before, State(340f, 12f, Degree.Resistant, ("SE_Pack_2", "Pack level 2")),
                new Dictionary<string, float> { ["m_movementModifier"] = -0.05f, ["m_runStaminaModifier"] = -0.1f }, NoSets);
            var four = WornChange.Between(before, State(380f, 14f, Degree.Resistant), new Dictionary<string, float> { ["m_movementModifier"] = -0.05f }, NoSets);

            var (columns, lines) = WornWords.Table(new List<(int, WornChange)> { (2, two), (4, four) }, 4);

            Assert.Equal(new[] { "", "Level 2", "Level 4" }, columns);
            Assert.Equal(new[] { "Carry weight", "+40", "+80" }, lines[0]);
            Assert.Equal(new[] { "Armour", "+2", "+4" }, lines[1]);
            Assert.Equal(new[] { "Movement", "-5%", "-5%" }, lines[2]);
            Assert.Equal(new[] { "Run stamina", "-10%", "no change" }, lines[3]);
            Assert.Equal(new[] { "Frost damage", "resisted", "resisted" }, lines[4]);
            Assert.Equal(new[] { "Brings", "Pack level 2", "nothing" }, lines[5]);
            Assert.Equal(6, lines.Count);
        }

        [Fact]
        public void SomethingOfOneQualityHasOneColumnAndNothingChangedSaysSo()
        {
            var before = State(300f, 10f);
            var (columns, lines) = WornWords.Table(new List<(int, WornChange)> { (1, WornChange.Between(before, before, NoModifiers, NoSets)) }, 1);

            Assert.Equal(new[] { "", "Seen" }, columns);
            Assert.Equal(new[] { "Changed", "nothing" }, lines.Single());
        }

        [Fact]
        public void AResistanceTakenAwayReadsAsPlain()
        {
            var change = WornChange.Between(State(300f, 0f, Degree.Resistant), State(300f, 0f, Degree.Normal), NoModifiers, NoSets);
            var (_, lines) = WornWords.Table(new List<(int, WornChange)> { (1, change) }, 1);

            Assert.Equal(new[] { "Frost damage", "taken plainly" }, lines.Single());
        }
    }
}
