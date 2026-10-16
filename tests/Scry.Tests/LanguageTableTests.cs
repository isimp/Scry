using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class LanguageTableTests
    {
        // The game ships every language in its tables at once (Localization.LoadCSV reads one
        // column of them, the language played in), so a word's text in any language can be read
        // from them as the game would read it in that language.

        [Fact]
        public void EveryLanguagesTextOfAWordIsReadAsTheGameReadsItsTables()
        {
            var table = "\"\",\"English\",\"Swedish\",\"German\"\r\n"
                + "\"biome_meadows\",\"Meadows\",\"Äng\",\"Grasland\"\r\n"
                + "\"// a note\",\"x\",\"y\",\"z\"\r\n"
                // A cell in quotes holds line breaks, commas and doubled quotes; a line inside it is no row.
                + "\"lore_long\",\"First line\nbiome_plains\",\"Fake\",\"Fake\",\"Fake\"\r\n"
                // An empty cell is the English.
                + "\"biome_swamp\",\"Swamp\",\"\",\"Sumpfland\"\r\n"
                // A row short of a language has no text for it.
                + "\"biome_plains\",\"Plains\",\"Slätt\"\r\n"
                + "\"biome_ashlands\",\"Ashlands, \"\"burnt\"\"\",\" Askland \",\"Aschenlande\"\r\n";

            var words = LanguageTable.Read(new[] { table }, new[] { "biome_meadows", "biome_swamp", "biome_plains", "biome_ashlands", "biome_ocean" });

            Assert.Equal(new[] { "biome_ashlands", "biome_meadows", "biome_plains", "biome_swamp" }, Sorted(words.Keys));
            Assert.Equal(Texts(("English", "Meadows"), ("Swedish", "Äng"), ("German", "Grasland")), words["biome_meadows"]);
            Assert.Equal(Texts(("English", "Swamp"), ("Swedish", "Swamp"), ("German", "Sumpfland")), words["biome_swamp"]);
            Assert.Equal(Texts(("English", "Plains"), ("Swedish", "Slätt")), words["biome_plains"]);
            Assert.Equal(Texts(("English", "Ashlands, \"burnt\""), ("Swedish", "Askland"), ("German", "Aschenlande")), words["biome_ashlands"]);
        }

        [Fact]
        public void AWordsTextInALaterTableReplacesAnEarlierOnesLanguageByLanguage()
        {
            // The game loads its tables in turn, a later one's text of a word replacing an earlier
            // one's in the languages the later one has; a table may go without quotes, and of a
            // language named twice the first column is its.
            var first = "\"\",\"English\",\"German\",\"French\"\n\"biome_meadows\",\"Meadows\",\"Wiese\",\"Prairies\"\n";
            var second = "﻿,English,German,German\nbiome_meadows,Meadows,Grasland,Wiesen\n";
            var third = ",English,Swedish\r\nbiome_meadows,Meadows,Äng";

            var words = LanguageTable.Read(new[] { first, second, third }, new[] { "biome_meadows" });

            Assert.Equal(Texts(("English", "Meadows"), ("German", "Grasland"), ("French", "Prairies"), ("Swedish", "Äng")), words["biome_meadows"]);
        }

        private static Dictionary<string, string> Texts(params (string Language, string Text)[] texts)
        {
            var all = new Dictionary<string, string>();
            foreach (var (language, text) in texts) all[language] = text;
            return all;
        }

        private static List<string> Sorted(IEnumerable<string> keys)
        {
            var list = new List<string>(keys);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }
    }
}
