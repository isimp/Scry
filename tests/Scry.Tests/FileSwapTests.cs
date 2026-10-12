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
