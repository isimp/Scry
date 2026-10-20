using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What another game sends packed is unpacked only so far: a message of a few bytes that
    /// would unpack to more than its kind ever holds is passed over before it takes the memory,
    /// so no player's game, and no server, can be made to run out by one.
    /// </summary>
    public class PackedTests
    {
        /// <summary>Packed as the game packs its messages (<c>Utils.Compress</c>).</summary>
        private static byte[] Pack(byte[] bytes)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Fastest)) gzip.Write(bytes, 0, bytes.Length);
                return output.ToArray();
            }
        }

        [Fact]
        public void AMessageWithinItsBoundUnpacksWhole()
        {
            var text = Encoding.UTF8.GetBytes("Crypt2 walked by you: 7 of 12 rooms");
            Assert.Equal(text, Packed.Unpack(Pack(text), 1024));
            Assert.Equal(new byte[0], Packed.Unpack(Pack(new byte[0]), 1024));
        }

        [Fact]
        public void AMessageThatWouldUnpackPastItsBoundIsPassedOver()
        {
            // A few kilobytes of zeros packed stand for megabytes unpacked.
            var packed = Pack(new byte[4 << 20]);
            Assert.True(packed.Length < 64 << 10);
            Assert.Null(Packed.Unpack(packed, 1 << 20));
            Assert.NotNull(Packed.Unpack(packed, 4 << 20));
        }

        [Fact]
        public void AMessageOneByteOverItsBoundIsPassedOver()
        {
            var packed = Pack(new byte[1 << 20]);
            Assert.NotNull(Packed.Unpack(packed, 1 << 20));
            Assert.Null(Packed.Unpack(packed, (1 << 20) - 1));
        }

        [Fact]
        public void BytesThatAreNoPackedMessageArePassedOver()
        {
            Assert.Null(Packed.Unpack(Encoding.UTF8.GetBytes("not packed at all"), 1024));
            Assert.Null(Packed.Unpack(null, 1024));
        }
    }
}
