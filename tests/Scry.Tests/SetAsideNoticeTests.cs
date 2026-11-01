using Xunit;

namespace Scry.Tests
{
    /// <summary>A lasting file that could not be read and was set aside is told by a notice naming it, until put away.</summary>
    public class SetAsideNoticeTests
    {
        [Fact]
        public void EachFileSetAsideIsToldInTurnUntilPutAway()
        {
            var files = new SetAsideFiles();
            Assert.Null(files.First);
            files.Tell(SetAsideKind.Compendium, "C:/cache/compendium/Hilde-Cloud-1.dat.unreadable-20261024-141205");
            files.Tell(SetAsideKind.Walks, "C:/cache/maps/Hilde-Cloud-1.dat.unreadable-20261024-141206");
            files.Tell(SetAsideKind.Walks, null);
            Assert.Equal(2, files.Count);
            Assert.Equal(SetAsideKind.Compendium, files.First?.Kind);

            files.PutAway();
            Assert.Equal(SetAsideKind.Walks, files.First?.Kind);
            files.PutAway();
            Assert.Null(files.First);
            files.PutAway();
            Assert.Equal(0, files.Count);
        }

        [Fact]
        public void TheNoticeSaysWhatCouldNotBeReadAndWhereItWent()
        {
            Assert.Equal("Scry could not read this character's compendium, so it set the old file aside as Hilde-Cloud-1.dat.unreadable-20261024-141205 and started a new one.",
                CompendiumWords.SetAsideNotice(SetAsideKind.Compendium, "Hilde-Cloud-1.dat.unreadable-20261024-141205"));
            Assert.Equal("Scry could not read the maps of the dungeons your character walked, so it set the old file aside as x.unreadable-1 and started new ones.",
                CompendiumWords.SetAsideNotice(SetAsideKind.Walks, "x.unreadable-1"));
            Assert.Equal("Scry could not read this world's dungeon records, so it set the old file aside as y.unreadable-2 and started new ones.",
                CompendiumWords.SetAsideNotice(SetAsideKind.Records, "y.unreadable-2"));
            Assert.Equal("Copy path", CompendiumWords.CopyPath);
        }
    }
}
