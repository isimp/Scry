using System.IO;
using System.IO.Compression;

namespace Scry
{
    /// <summary>
    /// Bytes another game sent packed as the game packs its messages (<c>Utils.Compress</c>, gzip),
    /// unpacked only up to the most a message of their kind holds. The game's own unpacking takes
    /// whatever the bytes stand for, so a message of a few kilobytes could stand for gigabytes;
    /// Scry's messages from other games are unpacked here.
    /// </summary>
    internal static class Packed
    {
        /// <summary>The bytes unpacked, or none where they are no packed message or would unpack to more than <paramref name="most"/> bytes.</summary>
        public static byte[] Unpack(byte[] packed, int most)
        {
            return Steps.Run(() => Whole(packed, most), out var bytes, null) == null ? bytes : null;
        }

        private static byte[] Whole(byte[] packed, int most)
        {
            using (var input = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[16384];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > most) return null;
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
        }
    }
}
