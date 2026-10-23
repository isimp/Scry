using System;
using System.IO;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class KeptFileTests
    {
        // A file Scry keeps of what was learned or walked (a character's book, its walks, a
        // server's records) is read from the one in place, else the one kept from before it.
        // Where neither can be read, both are set aside beside it, dated, never lost, and a new
        // one starts, which saves, so a broken file stops nothing learned from then on.

        private static string Load(string text) => text == "broken" ? throw new FormatException("not a book") : text ?? "new";

        private static string Folder()
        {
            var folder = Path.Combine(TempDir(), "kept-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        [Fact]
        public void TheFileInPlaceIsRead()
        {
            var path = Path.Combine(Folder(), "book.dat");
            File.WriteAllText(path, "fine");
            Assert.Equal("fine", KeptFile.Read(path, Load, "20261024-101500", out var aside));
            Assert.Null(aside);
        }

        [Fact]
        public void NoFileStartsANewOne()
        {
            var path = Path.Combine(Folder(), "book.dat");
            Assert.Equal("new", KeptFile.Read(path, Load, "20261024-101500", out var aside));
            Assert.Null(aside);
        }

        [Fact]
        public void AFileThatCannotBeReadGivesWayToTheOneKeptBeforeIt()
        {
            var path = Path.Combine(Folder(), "book.dat");
            File.WriteAllText(path, "broken");
            File.WriteAllText(path + ".bak", "yesterday");
            Assert.Equal("yesterday", KeptFile.Read(path, Load, "20261024-101500", out var aside));
            Assert.Null(aside);
        }

        [Fact]
        public void WhereNeitherCanBeReadBothAreSetAsideAndANewOneStarts()
        {
            var path = Path.Combine(Folder(), "book.dat");
            File.WriteAllText(path, "broken");
            File.WriteAllText(path + ".bak", "broken");
            Assert.Equal("new", KeptFile.Read(path, Load, "20261024-101500", out var aside));
            Assert.Equal(path + ".unreadable-20261024-101500", aside);
            // Nothing is lost: both stand beside it as they were, none in place to be read again.
            Assert.Equal("broken", File.ReadAllText(path + ".unreadable-20261024-101500"));
            Assert.Equal("broken", File.ReadAllText(path + ".bak.unreadable-20261024-101500"));
            Assert.False(File.Exists(path));
            Assert.False(File.Exists(path + ".bak"));
            Assert.Null(FileSwap.ToRead(path));
        }
    }
}
