using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A runestone a location holds (<c>RuneStone</c>): its name and every text it can give, the
    /// one it has (<c>m_topic</c>, <c>m_text</c>) or those it picks one of (<c>m_randomTexts</c>).
    /// </summary>
    internal sealed class PlaceRunestone
    {
        public string Name = "";
        public List<RuneText> Texts = new List<RuneText>();
    }

    /// <summary>A text a runestone gives: its title and the text, as the game shows them.</summary>
    internal sealed class RuneText
    {
        public string Topic = "";
        public string Text = "";
    }

    /// <summary>A location's runestones, each told once however often it holds one alike.</summary>
    internal static class PlaceRunes
    {
        /// <summary>Adds a stone with something to read, unless one with the same texts is there already.</summary>
        public static void Add(List<PlaceRunestone> stones, PlaceRunestone stone)
        {
            stone.Texts.RemoveAll(t => string.IsNullOrEmpty(t.Text));
            if (stone.Texts.Count == 0) return;
            if (stones.Any(s => s.Texts.Select(t => t.Text).SequenceEqual(stone.Texts.Select(t => t.Text)))) return;
            stones.Add(stone);
        }
    }

    /// <summary>A runestone's texts in words.</summary>
    internal static class RuneWords
    {
        /// <summary>
        /// What a stone with several texts does: gives one, picked with its place as the seed
        /// (<c>RuneStone.GetRandomText</c>), so always the same for a stone; null for one text.
        /// </summary>
        public static string Caption(PlaceRunestone stone) =>
            stone.Texts.Count > 1 ? $"Each stone gives one of these {Numbers.Count(stone.Texts.Count)}, always the same one for where it stands." : null;

        /// <summary>A text's title, or its stone's name when it has none.</summary>
        public static string Title(RuneText text, string stone) => string.IsNullOrEmpty(text.Topic) ? stone : text.Topic;
    }
}
