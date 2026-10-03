using System.Collections.Generic;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class LearnedTests
    {
        // What reads the world (a place's bundle, a dungeon's rooms, every location) tells here
        // when an entry's details change; whatever keeps told details listens, so the readers
        // need not know who that is.

        [Fact]
        public void ListenersHearWhichEntryChanged()
        {
            var heard = new List<Entry>();
            void Listen(Entry entry) => heard.Add(entry);
            Learned.Changed += Listen;
            try
            {
                var crypt = E("Crypt2", Kind.Location);
                Learned.About(crypt);
                Assert.Equal(new[] { crypt }, heard);
            }
            finally
            {
                Learned.Changed -= Listen;
            }
        }

        [Fact]
        public void ChangesToEveryEntryAreHeardAsNone()
        {
            var heard = new List<Entry>();
            var calls = 0;
            void Listen(Entry entry)
            {
                calls++;
                heard.Add(entry);
            }
            Learned.Changed += Listen;
            try
            {
                Learned.AboutAll();
                Assert.Equal(1, calls);
                Assert.Null(heard[0]);
            }
            finally
            {
                Learned.Changed -= Listen;
            }
        }

        [Fact]
        public void NoEntryTellsNothing()
        {
            var calls = 0;
            void Listen(Entry entry) => calls++;
            Learned.Changed += Listen;
            try
            {
                Learned.About(null);
                Assert.Equal(0, calls);
            }
            finally
            {
                Learned.Changed -= Listen;
            }
        }

        [Fact]
        public void TellingWithNobodyListeningIsFine()
        {
            Learned.About(E("Crypt2", Kind.Location));
            Learned.AboutAll();
        }
    }
}
