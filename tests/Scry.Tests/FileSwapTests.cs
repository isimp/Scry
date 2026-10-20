using System;
using System.IO;
using Xunit;

namespace Scry.Tests
{
    // Scry writes its own files aside and swaps them into place, so a crash leaves the old file or
    // the new one. A file removed, as what was kept is forgotten, goes with any copy written aside,
    // which would otherwise be read in its place.
    public sealed class FileSwapTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "scry-swap-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Fact]
        public void AFileRemovedLeavesNeitherItNorOneWrittenAside()
        {
            var path = Path.Combine(_folder, "places.dat");
            FileSwap.Write(path, aside => File.WriteAllText(aside, "kept"));
            File.WriteAllText(path + ".tmp", "a save that stopped");

            FileSwap.Remove(path);

            Assert.Null(FileSwap.ToRead(path));
            Assert.Empty(Directory.GetFiles(_folder));
        }

        [Fact]
        public void RemovingAFileThatIsNotThereDoesNothing()
        {
            FileSwap.Remove(Path.Combine(_folder, "places.dat"));

            Assert.False(Directory.Exists(_folder));
        }

        [Fact]
        public void ALastingFileKeepsTheOneBeforeItBesideIt()
        {
            var path = Path.Combine(_folder, "book.txt");
            FileSwap.Write(path, aside => File.WriteAllText(aside, "day 3"), lasting: true);
            FileSwap.Write(path, aside => File.WriteAllText(aside, "day 4"), lasting: true);

            Assert.Equal("day 4", File.ReadAllText(FileSwap.ToRead(path)));
            Assert.Equal("day 3", File.ReadAllText(path + ".bak"));
        }

        [Fact]
        public void ALastingFileLeftAsZerosByAPowerCutIsReadFromTheOneBefore()
        {
            // A power cut after the swap can leave the file's length on the disk with its bytes never written.
            var path = Path.Combine(_folder, "book.txt");
            FileSwap.Write(path, aside => File.WriteAllText(aside, "day 3"), lasting: true);
            FileSwap.Write(path, aside => File.WriteAllText(aside, "day 4"), lasting: true);
            File.WriteAllBytes(path, new byte[64]);

            Assert.Equal("day 3", File.ReadAllText(FileSwap.ToRead(path)));
        }

        [Fact]
        public void AnEmptyFileIsReadAsItIs()
        {
            // Empty is a file with nothing in it, such as no favourites, never a lost one.
            var path = Path.Combine(_folder, "favourites.txt");
            FileSwap.Write(path, aside => File.WriteAllText(aside, "Troll"), lasting: true);
            FileSwap.Write(path, aside => File.WriteAllText(aside, ""), lasting: true);

            Assert.Equal("", File.ReadAllText(FileSwap.ToRead(path)));
        }

        [Fact]
        public void AFileRemovedTakesTheOneBeforeItAlong()
        {
            var path = Path.Combine(_folder, "places.dat");
            FileSwap.Write(path, aside => File.WriteAllText(aside, "first"), lasting: true);
            FileSwap.Write(path, aside => File.WriteAllText(aside, "second"), lasting: true);

            FileSwap.Remove(path);

            Assert.Null(FileSwap.ToRead(path));
            Assert.Empty(Directory.GetFiles(_folder));
        }

        [Fact]
        public void OnlyACopyWrittenAsideIsRemovedToo()
        {
            var path = Path.Combine(_folder, "places.dat");
            Directory.CreateDirectory(_folder);
            File.WriteAllText(path + ".tmp", "a save that stopped");

            FileSwap.Remove(path);

            Assert.Null(FileSwap.ToRead(path));
        }
    }
}
