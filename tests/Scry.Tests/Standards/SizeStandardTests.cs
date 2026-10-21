using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// No method of Scry's or the self-test's runs longer than <see cref="MostMethodLines"/> lines
    /// and no file longer than <see cref="MostFileLines"/>, so a page or a part stays one a reader
    /// takes in. Those already longer are listed with the lines they hold: each may shrink, never
    /// grow, and leaves the list once it is short enough, so the list only ever gets shorter.
    /// </summary>
    public class SizeStandardTests
    {
        private const int MostMethodLines = 120;
        private const int MostFileLines = 1000;

        /// <summary>The methods longer than allowed, by file and name, with the lines each holds at the most.</summary>
        private static readonly Dictionary<string, int> LongMethods = new Dictionary<string, int>
        {
            ["selftest/SelfTest.Card.cs SelfTest.RoomCardShows"] = 174,
            ["selftest/SelfTest.Compendium.cs SelfTest.CompendiumKept"] = 124,
            ["selftest/SelfTest.Compendium.cs SelfTest.CompendiumShown"] = 267,
            ["selftest/SelfTest.Focus.cs SelfTest.GoesIntoRooms"] = 123,
            ["selftest/SelfTest.Layouts.cs SelfTest.Layout"] = 155,
            ["selftest/SelfTest.LevelsTried.cs SelfTest.LevelsTried"] = 222,
            ["selftest/SelfTest.Pins.cs SelfTest.DungeonPins"] = 159,
            ["selftest/SelfTest.PlanBox.cs SelfTest.PlanBoxShows"] = 495,
            ["selftest/SelfTest.Play.cs SelfTest.CompendiumHooksFed"] = 168,
            ["selftest/SelfTest.Stage.cs SelfTest.GroundBackdrop"] = 135,
            ["src/Catalog/Facts/Facts.Resources.cs Facts.Resource"] = 144,
            ["src/Preview/Stage.Framing.cs Stage.Frame"] = 129,
            ["src/UI/ScryPanel.Animations.cs ScryPanel.Clips"] = 146,
            ["src/UI/ScryPanel.Header.cs ScryPanel.BookLayout"] = 125,
            ["src/UI/ScryPanel.List.cs ScryPanel.List"] = 128,
            ["src/UI/ScryPanel.cs ScryPanel.Draw"] = 187,
            ["src/UI/Skin.cs Skin.Ensure"] = 203,
        };

        /// <summary>The files longer than allowed, with the lines each holds at the most.</summary>
        private static readonly Dictionary<string, int> LongFiles = new Dictionary<string, int>
        {
            ["selftest/SelfTest.Details.cs"] = 1017,
            ["src/UI/Skin.cs"] = 1230,
        };

        /// <summary>Every method, constructor and property accessor with a body, by file and name, its longest where a name is overloaded.</summary>
        private static Dictionary<string, int> MethodLines()
        {
            var lines = new Dictionary<string, int>();
            foreach (var compilation in ScrySource.Compilations)
            {
                foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
                {
                    var file = ScrySource.Relative(tree);
                    foreach (var node in tree.GetRoot().DescendantNodes())
                    {
                        string name;
                        switch (node)
                        {
                            case MethodDeclarationSyntax method:
                                name = method.Identifier.Text;
                                break;
                            case ConstructorDeclarationSyntax constructor:
                                name = constructor.Identifier.Text;
                                break;
                            case AccessorDeclarationSyntax accessor when accessor.Parent?.Parent is BasePropertyDeclarationSyntax property:
                                name = (property is PropertyDeclarationSyntax named ? named.Identifier.Text : "this") + "." + accessor.Keyword.Text;
                                break;
                            default:
                                continue;
                        }
                        var span = node.GetLocation().GetLineSpan();
                        var count = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
                        var type = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "";
                        var key = $"{file} {type}.{name}";
                        if (!lines.TryGetValue(key, out var held) || count > held) lines[key] = count;
                    }
                }
            }
            return lines;
        }

        private static Dictionary<string, int> FileLines() =>
            ScrySource.Compilations.SelectMany(c => c.SyntaxTrees).Where(t => !string.IsNullOrEmpty(t.FilePath))
                .ToDictionary(ScrySource.Relative, t => t.GetText().Lines.Count);

        /// <summary>The places over the limit not listed, or listed and grown, and the entries listed but short enough or gone, to take off the list.</summary>
        private static IEnumerable<string> Over(Dictionary<string, int> measured, Dictionary<string, int> listed, int most)
        {
            foreach (var (key, count) in measured.Where(m => m.Value > most))
            {
                if (!listed.TryGetValue(key, out var allowed)) yield return $"{key} is {count} lines, over {most}: split it";
                else if (count > allowed) yield return $"{key} grew to {count} lines from {allowed}: split it";
                else if (count < allowed) yield return $"{key} shrank to {count} lines: list it at {count}";
            }
            foreach (var (key, _) in listed)
            {
                if (!measured.TryGetValue(key, out var count) || count <= most) yield return $"{key} is listed but no longer over {most}: take it off the list";
            }
        }

        [Fact]
        public void NoMethodRunsLongerThanAllowedAndNoneListedGrows()
        {
            Violations.None("keep methods short", Over(MethodLines(), LongMethods, MostMethodLines));
        }

        [Fact]
        public void NoFileRunsLongerThanAllowedAndNoneListedGrows()
        {
            Violations.None("keep files short", Over(FileLines(), LongFiles, MostFileLines));
        }
    }
}
