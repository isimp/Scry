using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class LoadsAheadTests
    {
        // Loading a dungeon's room prefabs at once held a hosting game's frame for a fifth of a
        // second and more. They are asked for ahead, loading on their own, and the dungeon is read
        // once none of them is still loading.

        private sealed class Loader
        {
            public readonly List<int> Asked = new List<int>();
            public readonly HashSet<int> Loading = new HashSet<int>();

            public void Ask(int room)
            {
                Asked.Add(room);
                Loading.Add(room);
            }

            public bool IsLoading(int room) => Loading.Contains(room);
        }

        [Fact]
        public void EachThingNeededIsAskedForOnceAndTheWorkWaitsWhileAnyIsLoading()
        {
            var loader = new Loader();
            var ahead = new LoadsAhead<int>();

            Assert.False(ahead.Ready(new[] { 1, 2, 2 }, loader.IsLoading, loader.Ask));
            Assert.Equal(new[] { 1, 2 }, loader.Asked);

            loader.Loading.Remove(1);
            Assert.False(ahead.Ready(new[] { 1, 2 }, loader.IsLoading, loader.Ask));
            Assert.Equal(new[] { 1, 2 }, loader.Asked);
        }

        [Fact]
        public void TheWorkGoesOnOnceNoneIsLoadingLoadedOrFailedAlike()
        {
            // One that failed is loading no more either: the work goes on and finds it missing, as before.
            var loader = new Loader();
            var ahead = new LoadsAhead<int>();
            ahead.Ready(new[] { 1, 2 }, loader.IsLoading, loader.Ask);

            loader.Loading.Clear();

            Assert.True(ahead.Ready(new[] { 1, 2 }, loader.IsLoading, loader.Ask));
        }

        [Fact]
        public void AThingAskedForBeforeIsNotAskedForAgainByOtherWork()
        {
            // Two dungeons of one kind share their rooms: the second waits for none it needs loaded already.
            var loader = new Loader();
            var ahead = new LoadsAhead<int>();
            ahead.Ready(new[] { 1, 2 }, loader.IsLoading, loader.Ask);
            loader.Loading.Clear();

            Assert.False(ahead.Ready(new[] { 2, 3 }, loader.IsLoading, loader.Ask));
            Assert.Equal(new[] { 1, 2, 3 }, loader.Asked);
            Assert.True(ahead.Asked(2));
            Assert.False(ahead.Asked(4));
        }

        [Fact]
        public void NothingNeededIsReadyAtOnce()
        {
            var loader = new Loader();
            Assert.True(new LoadsAhead<int>().Ready(new int[0], loader.IsLoading, loader.Ask));
            Assert.Empty(loader.Asked);
        }

        [Fact]
        public void ForgottenEverythingIsAskedForAgain()
        {
            // The reading done, what it held is let go of, and a reading after asks anew.
            var loader = new Loader();
            var ahead = new LoadsAhead<int>();
            ahead.Ready(new[] { 1 }, loader.IsLoading, loader.Ask);
            loader.Loading.Clear();

            ahead.Forget();
            ahead.Ready(new[] { 1 }, loader.IsLoading, loader.Ask);

            Assert.Equal(new[] { 1, 1 }, loader.Asked);
            Assert.False(ahead.Asked(2));
        }
    }
}
