using System;
using Xunit;

namespace Scry.Tests
{
    public class SeaHoldTests
    {
        // On the ocean's ground the sea holds what the game holds in it: a creature that swims
        // keeps its swim depth under the surface, a Leviathan rides the surface by its float
        // offset, and the sea under them is as deep as the world places them, so the camera
        // looks down onto open water rather than standing under its surface. What the sea does
        // not hold stands on its floor in the shallows, the water no higher than its middle.

        [Fact]
        public void ASerpentSwimsAsFarUnderTheSurfaceAsTheGameHoldsIt()
        {
            var serpent = SeaHold.Swims(swimDepth: 1.16f, placed: null);
            Assert.Equal(SeaHeld.Swimming, serpent.Held);
            Assert.Equal(1.16f, serpent.SurfaceOverRoot, 3);
        }

        [Fact]
        public void ALeviathanRidesTheSurfaceByItsFloatOffset()
        {
            Assert.Equal(0f, SeaHold.AtSurface(floatOffset: 0f, placed: 30f).SurfaceOverRoot, 3);
            // A root held three metres under the surface has the surface three metres over it.
            Assert.Equal(3f, SeaHold.AtSurface(floatOffset: -3f, placed: null).SurfaceOverRoot, 3);
            Assert.Equal(SeaHeld.AtSurface, SeaHold.AtSurface(0f, null).Held);
        }

        [Fact]
        public void TheSeaUnderALeviathanIsAsDeepAsTheWorldPlacesItAndClearOfItsBody()
        {
            // The world places a Leviathan where its ground lies thirty metres and more under the water.
            var placed = SeaHold.Shallowest(new[] { (MinOceanDepth: 0f, MaxAltitude: -30f) });
            Assert.Equal(30f, placed.Value, 3);
            // Its body reaches 32 m under the surface, so the floor lies under that, not through it.
            var leviathan = SeaHold.AtSurface(0f, placed);
            Assert.Equal(32.17f + SeaHold.Clear, leviathan.Depth(lowestUnderSurface: 32.17f, height: 42f), 3);
            // A smaller body keeps the world's depth.
            Assert.Equal(30f, leviathan.Depth(lowestUnderSurface: 5f, height: 10f), 3);
        }

        [Fact]
        public void ASwimmerWithNoDepthOfTheWorldsSwimsInSeaDeepEnoughToLookDeep()
        {
            var serpent = SeaHold.Swims(1.16f, placed: null);
            Assert.Equal(SeaHold.DeepLook, serpent.Depth(lowestUnderSurface: 3.4f, height: 5.5f), 3);
            // A rule that lets it come anywhere in the sea leaves it so too.
            var anywhere = SeaHold.Swims(1.16f, SeaHold.Shallowest(new[] { (0f, 1000f) }));
            Assert.Equal(SeaHold.DeepLook, anywhere.Depth(3.4f, 5.5f), 3);
        }

        [Fact]
        public void TheShallowestSeaAnyRuleOfTheWorldsAllowsIsTheOneShown()
        {
            Assert.Equal(30f, SeaHold.Shallowest(new[] { (0f, -30f), (0f, -50f) }).Value, 3);
            Assert.Equal(20f, SeaHold.Shallowest(new[] { (20f, 1000f) }).Value, 3);
            Assert.Equal(25f, SeaHold.Shallowest(new[] { (25f, -10f) }).Value, 3);
            Assert.Equal(0f, SeaHold.Shallowest(new[] { (0f, -30f), (0f, 1000f) }).Value, 3);
            Assert.Null(SeaHold.Shallowest(Array.Empty<(float, float)>()));
        }

        [Fact]
        public void WhatTheSeaDoesNotHoldStandsOnItsFloorInTheShallows()
        {
            var bench = SeaHold.Not;
            Assert.Equal(SeaHeld.Not, bench.Held);
            // Water to its middle, so the camera looking down at its middle stays over the surface.
            Assert.Equal(0.6f, bench.Depth(lowestUnderSurface: 0f, height: 1.2f), 3);
            // Never deeper than the shallows.
            Assert.Equal(SeaHold.Shallows, bench.Depth(0f, 20f), 3);
            Assert.Equal(0f, bench.Depth(0f, 0f), 3);
        }

        [Fact]
        public void ASwimmerSwimsInTheSeaAndOneLivingOnlyInWaterSwimsWherever()
        {
            Assert.True(SeaHold.InWater(SeaHeld.Swimming, seaShown: true, livesInWater: false));
            // A greydwarf swims in the sea, and stands on the plain floor.
            Assert.False(SeaHold.InWater(SeaHeld.Swimming, seaShown: false, livesInWater: false));
            // A serpent is never seen out of the water but stranded, so it swims on any ground.
            Assert.True(SeaHold.InWater(SeaHeld.Swimming, seaShown: false, livesInWater: true));
            Assert.False(SeaHold.InWater(SeaHeld.Not, seaShown: true, livesInWater: false));
        }
    }
}
