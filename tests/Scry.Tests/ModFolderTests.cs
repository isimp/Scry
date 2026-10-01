using Xunit;

namespace Scry.Tests
{
    public class ModFolderTests
    {
        // A mod installed by a mod manager sits in a folder named for its package ("ASharpPen-
        // Drop_That") with the package's manifest.json, icon.png and README.md beside it. Its
        // page tells what the manifest says of it, who made it, and its readme as plain text.

        private const string DropThat = "\uFEFF{\n  \"name\": \"Drop_That\",\n  \"version_number\": \"3.1.6\",\n  \"website_url\": \"https://github.com/ASharpPen/Valheim.DropThat\",\n  \"description\": \"Tool for configuring loot tables.\",\n  \"dependencies\": [\n    \"denikson-BepInExPack_Valheim-5.4.2333\"\n  ]\n}";

        [Fact]
        public void AManifestTellsTheModsNameDescriptionWebsiteAndDependencies()
        {
            var manifest = ModManifest.Parse(DropThat);

            Assert.Equal("Drop_That", manifest.Name);
            Assert.Equal("3.1.6", manifest.Version);
            Assert.Equal("Tool for configuring loot tables.", manifest.Description);
            Assert.Equal("https://github.com/ASharpPen/Valheim.DropThat", manifest.Website);
            Assert.Equal(new[] { "denikson-BepInExPack_Valheim-5.4.2333" }, manifest.Dependencies);
        }

        [Fact]
        public void AManifestIsReadWhateverElseItHolds()
        {
            var manifest = ModManifest.Parse("{\"installers\": [{\"identifier\": \"bepinex\"}], \"name\": \"A\\\"B\\u00e9\\\\C\", \"extra\": 12.5, \"flag\": true, \"none\": null, \"dependencies\": [], \"description\": \"Line one\\nline two\"}");

            Assert.Equal("A\"B\u00e9\\C", manifest.Name);
            Assert.Equal("Line one\nline two", manifest.Description);
            Assert.Empty(manifest.Dependencies);
            Assert.Equal("", manifest.Website);
        }

        [Fact]
        public void WhatIsNoManifestIsNone()
        {
            Assert.Null(ModManifest.Parse(null));
            Assert.Null(ModManifest.Parse(""));
            Assert.Null(ModManifest.Parse("not json"));
            Assert.Null(ModManifest.Parse("[\"a\"]"));
            Assert.Null(ModManifest.Parse("{\"name\": \"cut off"));
            Assert.Null(ModManifest.Parse("{\"name\": \"x\", \"version_number\": }"));
            Assert.Null(ModManifest.Parse("{\"name\": \"x\" \"description\": \"y\"}"));
        }

        [Fact]
        public void TheAuthorIsTheFolderNameBeforeThePackagesName()
        {
            Assert.Equal("ASharpPen", ModManifest.Author("ASharpPen-Drop_That", "Drop_That"));
            Assert.Equal("Azumatt", ModManifest.Author("Azumatt-AzuAutoStore", "azuautostore"));
            Assert.Null(ModManifest.Author("Drop_That", "Drop_That"));
            Assert.Null(ModManifest.Author("plugins", "Drop_That"));
            Assert.Null(ModManifest.Author("-Drop_That", "Drop_That"));
            Assert.Null(ModManifest.Author("ASharpPen-Drop_That", ""));
            Assert.Null(ModManifest.Author("ASharpPen-", ""));
        }

        [Fact]
        public void AWebsiteIsLinkedOnlyWhenOnTheWeb()
        {
            // A click opens it in the browser, so it must be a web address and nothing else.
            Assert.True(ModWords.IsWebsite("https://github.com/ASharpPen/Valheim.DropThat"));
            Assert.True(ModWords.IsWebsite("HTTP://example.com"));
            Assert.False(ModWords.IsWebsite("file:///C:/Windows/system32/calc.exe"));
            Assert.False(ModWords.IsWebsite("C:\\Games\\mod.exe"));
            Assert.False(ModWords.IsWebsite("https://example.com\" && calc"));
            Assert.False(ModWords.IsWebsite("https://example.com/a b"));
            Assert.False(ModWords.IsWebsite("https://example.com/\"&calc"));
            Assert.False(ModWords.IsWebsite(""));
            Assert.False(ModWords.IsWebsite(null));
        }

        [Fact]
        public void ADependencyNamesItsPackageWithoutItsVersion()
        {
            Assert.Equal("denikson-BepInExPack_Valheim", ModManifest.Package("denikson-BepInExPack_Valheim-5.4.2333"));
            Assert.Equal("ValheimModding-Jotunn", ModManifest.Package("ValheimModding-Jotunn-2.30.2"));
            Assert.Equal("Jotunn", ModManifest.Package("Jotunn"));
            Assert.Equal("", ModManifest.Package(null));
        }

        private const string Readme = "<p align=\"center\"><img src=\"banner.png\"></p>\n\n# Drop That\n\n[![Badge](https://img.shields.io/x.svg)](https://thunderstore.io)\n\nA **tool** for _configuring_ [loot tables](https://example.com/wiki).\n\n## Features\n\n- Change drops of `CharacterDrop`\n* Keep OP_Bamboo_Wood as it is\n1. Numbered too\n\n> Quoted note\n\n---\n\n```\n[Section]\nKey = 1\n```\n\n| Setting | Default |\n|---|---|\n| Enabled | true |\n\n<!-- hidden -->\n[ref]: https://example.com\nEnd \\_escaped\\_.";

        [Fact]
        public void AReadmeIsToldAsPlainText()
        {
            var text = ReadmeText.Plain(Readme, 10000);

            Assert.Equal("Drop That\n\nA tool for configuring loot tables.\n\nFeatures\n\n\u2022 Change drops of CharacterDrop\n\u2022 Keep OP_Bamboo_Wood as it is\n\u2022 Numbered too\n\nQuoted note\n\n[Section]\nKey = 1\n\nSetting \u00b7 Default\nEnabled \u00b7 true\n\nEnd _escaped_.", text);
        }

        [Fact]
        public void LinesGoTogetherAsTheMarkdownPutsThem()
        {
            // A paragraph's lines are one; a line straight after an item goes on with the item;
            // headings, items, rows and code each stand apart from what is unlike them.
            var text = ReadmeText.Plain("A paragraph wrapped\nat eighty.\n- an item\nwrapped too\n- next\n| a | b |\n# Title\n## Under it\nText.", 1000);

            Assert.Equal("A paragraph wrapped at eighty.\n\n\u2022 an item wrapped too\n\u2022 next\n\na \u00b7 b\n\nTitle\n\nUnder it\n\nText.", text);
        }

        [Fact]
        public void MarkupSpanningLinesAndTheWebsEscapesAreReadToo()
        {
            // Bold wrapped over two lines of an item, entities as a web page writes them, and a
            // file starting with a byte-order mark before its heading.
            var text = ReadmeText.Plain("\uFEFF# Title\n1. **Install [r2modman](https://x)\n   or the other.**\n\nUse &lt;id&gt; &amp; more&nbsp;here, &#65;&#x42;, *leaning* and &#42;starred&#42;.\n\n```\na **b** &lt;\n```", 1000);

            Assert.Equal("Title\n\n\u2022 Install r2modman or the other.\n\nUse <id> & more here, AB, leaning and *starred*.\n\na **b** &lt;", text);
        }

        [Fact]
        public void ALongReadmeStopsAtAParagraph()
        {
            var text = ReadmeText.Plain("First paragraph.\n\nSecond paragraph is here.\n\nThird.", 30);

            Assert.Equal("First paragraph.\n\n\u2026", text);
            Assert.Equal("Short.", ReadmeText.Plain("Short.", 30));
            Assert.Equal("Short.", ReadmeText.Plain("Short.", 6));
            Assert.Equal("", ReadmeText.Plain(null, 30));
        }

        [Fact]
        public void AReadmeWithNoParagraphBreakStopsAtAWord()
        {
            var text = ReadmeText.Plain("one two three four five six seven eight", 20);

            Assert.Equal("one two three four \u2026", text);
        }
    }
}
