using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Every file of the repository's own reads as it was written: a sign a tool could not read
    /// (U+FFFD, the replacement character) is a letter lost on the way, and stays lost unless a
    /// test finds it.
    /// </summary>
    public class FileStandardTests
    {
        private static readonly string[] Folders = { "src", "selftest", "tests", "docs", "tools", ".github" };
        private static readonly string[] Kinds = { ".cs", ".md", ".json", ".csproj", ".props", ".globalconfig", ".py", ".yml", ".txt" };

        /// <summary>Written by its number, so this file does not hold it.</summary>
        private const char LostCharacter = (char)0xFFFD;

        [Fact]
        public void NoFileHoldsACharacterLostOnTheWay()
        {
            var root = ScrySource.Root();
            var files = Folders
                .Select(folder => Path.Combine(root, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                .Where(file => Kinds.Contains(Path.GetExtension(file)))
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .ToList();
            Assert.NotEmpty(files);

            var found = new List<string>();
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf(LostCharacter) >= 0) found.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{i + 1}");
                }
            }
            Violations.None("put back the character lost on the way", found);
        }
    }
}
