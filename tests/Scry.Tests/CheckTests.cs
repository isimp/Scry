using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class IlShapeTests
    {
        // ldarg.0, ldfld <field>, ret
        private static readonly byte[] ReadField = { 0x02, 0x7B, 0x11, 0x00, 0x00, 0x04, 0x2A };

        [Fact]
        public void AMethodKeepsItsShapeWhenOnlyWhatItNamesMoves()
        {
            // The same code in a later build of the game, where the field's token has shifted.
            var moved = new byte[] { 0x02, 0x7B, 0x95, 0x02, 0x00, 0x04, 0x2A };

            Assert.Equal(IlShape.Of(ReadField), IlShape.Of(moved));
        }

        [Fact]
        public void DifferentStepsAreADifferentShape()
        {
            // ldarg.1 instead of ldarg.0.
            var other = new byte[] { 0x03, 0x7B, 0x11, 0x00, 0x00, 0x04, 0x2A };

            Assert.NotEqual(IlShape.Of(ReadField), IlShape.Of(other));
        }

        [Fact]
        public void AStepMoreIsADifferentShape()
        {
            var longer = new byte[] { 0x02, 0x7B, 0x11, 0x00, 0x00, 0x04, 0x00, 0x2A };

            Assert.NotEqual(IlShape.Of(ReadField), IlShape.Of(longer));
        }

        [Fact]
        public void TwoByteStepsAreReadWhole()
        {
            var equal = new byte[] { 0x02, 0x03, 0xFE, 0x01, 0x2A };   // ceq
            var greater = new byte[] { 0x02, 0x03, 0xFE, 0x02, 0x2A }; // cgt

            Assert.NotEqual(IlShape.Of(equal), IlShape.Of(greater));
        }

        [Fact]
        public void WhereASwitchJumpsToDoesNotMatterButWhatFollowsItDoes()
        {
            // switch with two targets, then ret.
            var switched = new byte[] { 0x45, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x2A };
            var elsewhere = new byte[] { 0x45, 0x02, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x2A };
            var more = new byte[] { 0x45, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x2A };

            Assert.Equal(IlShape.Of(switched), IlShape.Of(elsewhere));
            Assert.NotEqual(IlShape.Of(switched), IlShape.Of(more));
        }

        [Fact]
        public void ACoroutinesCodeIsFoundInTheTypeTheCompilerMakesForIt()
        {
            // A coroutine's own method only hands out its state machine, which never changes; its
            // steps are in the state machine the compiler names after it.
            Assert.True(IlShape.IsStateMachineOf("<Spawn>d__12", "Spawn"));
            Assert.True(IlShape.IsStateMachineOf("<ShakeAnimation>d__3", "ShakeAnimation"));
            Assert.False(IlShape.IsStateMachineOf("<SpawnAll>d__4", "Spawn"));
            Assert.False(IlShape.IsStateMachineOf("<Spawn>b__12_0", "Spawn"));
            Assert.False(IlShape.IsStateMachineOf("<>c", "Spawn"));
            Assert.False(IlShape.IsStateMachineOf("Spawn", "Spawn"));
            Assert.False(IlShape.IsStateMachineOf(null, "Spawn"));
        }

        [Fact]
        public void BrokenCodeHasNoShape()
        {
            Assert.Equal(0u, IlShape.Of(new byte[] { 0x7B, 0x11 }));
            Assert.Equal(0u, IlShape.Of(null));
        }
    }

    public class ChecklistTests
    {
        [Fact]
        public void WhenAllIsInPlaceOneLineSaysSo()
        {
            var list = new Checklist();
            list.Add("SpawnSystem.m_instances", Feature.WhereCreaturesSpawn, Found.Present);
            list.Add("VisEquipment.AttachItem", Feature.GearOnCreaturesAndPerson, Found.Present);

            var report = list.Report();

            Assert.Single(report);
            Assert.Contains("2", report[0]);
            Assert.False(list.AnyTrouble);
        }

        [Fact]
        public void AMissingPartSaysWhichFeatureIsOff()
        {
            var list = new Checklist();
            list.Add("SpawnSystem.m_instances", Feature.WhereCreaturesSpawn, Found.Missing);
            list.Add("VisEquipment.AttachItem", Feature.GearOnCreaturesAndPerson, Found.Present);

            var report = list.Report();

            Assert.True(list.AnyTrouble);
            var line = Assert.Single(report.Skip(1));
            Assert.Contains("SpawnSystem.m_instances", line);
            Assert.Contains("where creatures spawn is off", line);
        }

        [Fact]
        public void AChangedPartSaysItsFeatureMayBeSlightlyOff()
        {
            var list = new Checklist();
            list.Add("VisEquipment.AttachItem", Feature.GearOnCreaturesAndPerson, Found.Changed);

            var report = list.Report();

            Assert.Contains(report, l => l.Contains("VisEquipment.AttachItem") && l.Contains("gear on creatures and the person may be slightly off"));
        }

        [Fact]
        public void ThePanelIsToldOnlyTheFeaturesThatAreOffEachOnce()
        {
            // A changed part still works, perhaps a little off, and stays in the log; a missing one turns its feature off.
            var list = new Checklist();
            list.Add("A", Feature.GearOnCreaturesAndPerson, Found.Missing);
            list.Add("B", Feature.GearOnCreaturesAndPerson, Found.Missing);
            list.Add("C", Feature.Saddles, Found.Changed);
            list.Add("D", Feature.Footsteps, Found.Present);

            Assert.Equal(new[] { Feature.GearOnCreaturesAndPerson }, list.FeaturesOff);
        }

        [Fact]
        public void TheSummaryCountsWhatIsMissingAndWhatChanged()
        {
            var list = new Checklist();
            list.Add("A", Feature.Saddles, Found.Missing);
            list.Add("B", Feature.Footsteps, Found.Changed);
            list.Add("C", Feature.FireLooks, Found.Changed);
            list.Add("D", Feature.DoorLooks, Found.Present);

            Assert.Contains("1 missing", list.Report()[0]);
            Assert.Contains("2 changed", list.Report()[0]);
        }
    }
}
