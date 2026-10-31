using Xunit;

namespace Scry.Tests
{
    public class CookingSlotsTests
    {
        // Every game draws a cooking station's slots from what the game running it keeps of each:
        // raw, done or burnt. Food seen turning done or burnt on a station in sight shows how long
        // cooking takes and what the station gives, whoever runs it; food already done or burnt when
        // the station is first looked at was not seen turning, so it tells nothing.

        [Fact]
        public void FoodSeenTurningDoneIsToldOnce()
        {
            var slots = new CookingSlots<string>();
            Assert.Null(slots.Turned("grill", 0, CookedState.NotDone));
            Assert.Equal(CookedState.Done, slots.Turned("grill", 0, CookedState.Done));
            Assert.Null(slots.Turned("grill", 0, CookedState.Done));
        }

        [Fact]
        public void FoodAlreadyDoneOrBurntWhenFirstLookedAtTellsNothing()
        {
            var slots = new CookingSlots<string>();
            Assert.Null(slots.Turned("grill", 0, CookedState.Done));
            Assert.Null(slots.Turned("grill", 1, CookedState.Burnt));
        }

        [Fact]
        public void FoodLeftTooLongIsToldAsBurnt()
        {
            var slots = new CookingSlots<string>();
            slots.Turned("grill", 0, CookedState.NotDone);
            slots.Turned("grill", 0, CookedState.Done);
            Assert.Equal(CookedState.Burnt, slots.Turned("grill", 0, CookedState.Burnt));
            Assert.Null(slots.Turned("grill", 0, CookedState.Burnt));
        }

        [Fact]
        public void TakenOffAndCookedAgainIsToldAgain()
        {
            var slots = new CookingSlots<string>();
            slots.Turned("grill", 0, CookedState.NotDone);
            slots.Turned("grill", 0, CookedState.Done);
            Assert.Null(slots.Turned("grill", 0, CookedState.NotDone));
            Assert.Equal(CookedState.Done, slots.Turned("grill", 0, CookedState.Done));
        }

        [Fact]
        public void EachSlotOfEachStationGoesOnItsOwn()
        {
            var slots = new CookingSlots<string>();
            slots.Turned("grill", 0, CookedState.NotDone);
            slots.Turned("grill", 1, CookedState.Done);
            slots.Turned("oven", 0, CookedState.NotDone);
            Assert.Null(slots.Turned("grill", 1, CookedState.Done));
            Assert.Equal(CookedState.Done, slots.Turned("grill", 0, CookedState.Done));
            Assert.Equal(CookedState.Burnt, slots.Turned("oven", 0, CookedState.Burnt));
        }

        [Fact]
        public void ASlotIsReadByTheKeysTheGameKeepsItUnder()
        {
            Assert.Equal("slotstatus0", CookingSlots<int>.StateKey(0));
            Assert.Equal("slot2", CookingSlots<int>.ItemKey(2));
        }

        [Fact]
        public void AStationThatLeftSightIsLookedAtAnewWhenBack()
        {
            // Walked off while the meat was raw, back once it is done: it was not seen turning.
            var slots = new CookingSlots<string>();
            slots.Turned("grill", 0, CookedState.NotDone);
            slots.Turned("grill", 1, CookedState.NotDone);
            slots.Forget("grill", 2);
            Assert.Null(slots.Turned("grill", 0, CookedState.Done));
            Assert.Null(slots.Turned("grill", 1, CookedState.Burnt));
            Assert.Equal(CookedState.Burnt, slots.Turned("grill", 0, CookedState.Burnt));
        }

        [Fact]
        public void OnlySoManySlotsAreRememberedTheLongestUnseenForgottenFirst()
        {
            var slots = new CookingSlots<int>();
            for (var i = 1; i <= CookingSlots<int>.Kept + 1; i++) slots.Turned(i, 0, CookedState.NotDone);
            Assert.Equal(CookingSlots<int>.Kept, slots.Count);
            // The first station was forgotten, so it is looked at anew; the second and the last are still remembered.
            Assert.Equal(CookedState.Done, slots.Turned(2, 0, CookedState.Done));
            Assert.Equal(CookedState.Done, slots.Turned(CookingSlots<int>.Kept + 1, 0, CookedState.Done));
            Assert.Null(slots.Turned(1, 0, CookedState.Done));
        }
    }
}
