using System;
using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class WorldCacheTests
    {
        [Fact]
        public void LeavingAWorldForgetsEveryCacheRegistered()
        {
            var forgotten = new List<string>();
            WorldCaches.Register("test first", () => forgotten.Add("first"));
            WorldCaches.Register("test second", () => forgotten.Add("second"));

            WorldCaches.ForgetAll(null);

            Assert.Contains("first", forgotten);
            Assert.Contains("second", forgotten);
        }

        [Fact]
        public void ACacheThatFailsToForgetDoesNotKeepTheOthersAndIsTold()
        {
            var forgotten = false;
            var told = new List<string>();
            WorldCaches.Register("test failing", () => throw new InvalidOperationException("broken"));
            WorldCaches.Register("test after", () => forgotten = true);

            WorldCaches.ForgetAll((name, ex) => told.Add(name));

            Assert.True(forgotten);
            Assert.Contains("test failing", told);
            WorldCaches.Register("test failing", () => { });
        }

        [Fact]
        public void RegisteringTheSameNameAgainReplacesItRatherThanForgettingTwice()
        {
            var times = 0;
            WorldCaches.Register("test twice", () => times++);
            WorldCaches.Register("test twice", () => times++);

            WorldCaches.ForgetAll(null);

            Assert.Equal(1, times);
        }
    }
}
