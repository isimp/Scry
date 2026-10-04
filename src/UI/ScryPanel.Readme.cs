using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>A mod's readme under its details, as plain text, read from its package folder the first time its page shows.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>How much of a readme is shown: enough for what a mod does and how to set it up; the rest stays in its file.</summary>
        private const int ReadmeLength = 6000;

        /// <summary>Each readme as shown, by its file, read once a session.</summary>
        private static readonly Dictionary<string, string> Readmes = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether readmes are folded away; setting it is remembered, as clicking its heading is.</summary>
        public static bool ReadmeFolded
        {
            get => IsFolded("readme");
            set => SetFolded("readme", value);
        }

        /// <summary>A mod's readme as its page shows it, or "" for none.</summary>
        public static string ReadmeOf(ModSource mod)
        {
            if (mod == null || string.IsNullOrEmpty(mod.ReadmePath)) return "";
            if (!Readmes.TryGetValue(mod.ReadmePath, out var text)) Readmes[mod.ReadmePath] = text = ModFolders.Readme(mod.ReadmePath, ReadmeLength);
            return text;
        }

        private static float ReadmeSection(Entry entry, float width, float y)
        {
            if (!(entry.Source is ModSource mod)) return y;
            var text = ReadmeOf(mod);
            if (text.Length == 0) return y;

            y = SectionHeading("README", width, y, null, "readme");
            if (IsFolded("readme")) return y;
            CountDrawn(PanelPart.Readme);
            var height = Skin.Height(Skin.Wrap, text, width);
            GUI.Label(new Rect(0f, y, width, height), text, Skin.Wrap);
            return y + height + U(10f);
        }
    }
}
