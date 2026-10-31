using System.Collections.Generic;

namespace Scry
{
    /// <summary>The faint lines a page says at a section's end of what the rules left out of it.</summary>
    internal sealed partial class Explorer
    {
        /// <summary>
        /// The faint lines at a section's end telling how many things its page left out, each with
        /// its hover saying the way to them: under the strict rules what was never met
        /// (<paramref name="unmet"/>, <see cref="CompendiumWords.NeverMet"/>) apart from what was met
        /// but not seen together (<paramref name="apart"/>, <see cref="CompendiumWords.LeftOutOf"/>),
        /// for every section; under the loose ones, which leave out only what was never met, one
        /// line, only for a creature's loot, where it lives, a room's card and a raid, counts the
        /// player can act on. Without "more" where the section lists nothing else (<paramref name="alone"/>).
        /// </summary>
        public List<(string Line, string Tip)> LeftOutLines(string topic, int unmet, int apart, bool alone = false)
        {
            var lines = new List<(string, string)>();
            if (!LeavingOut)
            {
                var line = LeftOutLine(topic, unmet + apart, alone);
                if (line != null) lines.Add((line, CompendiumWords.MeetFirstTip(strict: false)));
                return lines;
            }
            // Strict: what was never met waits to be met first, what was met waits to be seen together; each its own line.
            if (unmet > 0) lines.Add((CompendiumWords.NeverMet(topic, unmet, alone), CompendiumWords.MeetFirstTip(strict: true)));
            if (apart > 0) lines.Add((CompendiumWords.LeftOutOf(topic, apart, alone), CompendiumWords.TogetherTip(topic)));
            return lines;
        }

        /// <summary>
        /// What a card left out of a list it names, by the keys left out, in its lines' words
        /// (<see cref="LeftOutLines"/>): each counted as never met (<see cref="NotMet"/>) or as met
        /// but not seen together; a room's card or a raid's.
        /// </summary>
        public List<string> LeftOutCounts(string topic, IEnumerable<string> leftOut, bool alone = false)
        {
            var unmet = 0;
            var apart = 0;
            foreach (var key in leftOut)
            {
                if (NotMet(key)) unmet++;
                else apart++;
            }
            return LeftOutLines(topic, unmet, apart, alone).ConvertAll(l => l.Line);
        }

        /// <summary>
        /// The faint line telling how many things a page left out, as <see cref="LeftOutLines"/>
        /// tells them, every one in one count.
        /// </summary>
        public string LeftOutLine(string topic, int count, bool alone = false)
        {
            // Strict leaves out what was not seen together with the page: every section says how
            // many, so the page never goes quiet on what it keeps back.
            if (LeavingOut) return CompendiumWords.LeftOutOf(topic, count, alone);
            // Otherwise only a count the player can act on is said: drops not yet seen fall, places it
            // lives not met in, what a dungeon room's card holds not yet met, and what a raid brings not yet met.
            if (topic != "loot" && topic != "lives" && topic != "room" && topic != "raid") return null;
            return CompendiumWords.NotYetMet(topic, count, alone);
        }
    }
}
