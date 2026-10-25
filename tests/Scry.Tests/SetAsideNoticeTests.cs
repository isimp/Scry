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
            Assert.Equal("This character's compendium could not be read. It was set aside as Hilde-Cloud-1.dat.unreadable-20261024-141205 and a new one started.",
                CompendiumWords.SetAsideNotice(SetAsideKind.Compendium, "Hilde-Cloud-1.dat.unreadable-20261024-141205"));
            Assert.Equal("The maps of the dungeons your character walked could not be read. They were set aside as x.unreadable-1 and new ones started.",
                CompendiumWords.SetAsideNotice(SetAsideKind.Walks, "x.unreadable-1"));
            Assert.Equal("This world's dungeon records could not be read. They were set aside as y.unreadable-2 and new ones started.",
                CompendiumWords.SetAsideNotice(SetAsideKind.Records, "y.unreadable-2"));
            Assert.Equal("Copy path", CompendiumWords.CopyPath);
        }
    }
}
