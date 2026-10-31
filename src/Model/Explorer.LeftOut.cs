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
        /// line for every section. Without "more" where the section lists nothing else (<paramref name="alone"/>).
        /// </summary>
        public List<(string Line, string Tip)> LeftOutLines(string topic, int unmet, int apart, bool alone = false)
        {
            var lines = new List<(string, string)>();
            if (!LeavingOut)
            {
                var line = CompendiumWords.NotYetMet(topic, unmet + apart, alone);
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
        /// What a line of where something spawns or grows waits on, the name it is counted by on
        /// the page: the boss it names where that was not met; else, while none of its biomes
        /// (<paramref name="metIn"/>) was met, the first of them, so the line counts as never met
        /// while hidden for it; else the boss, or a biome met where it names none, so the line
        /// still counts once shown and the page's total holds.
        /// </summary>
        public string WaitsOn(string prefab, IReadOnlyList<string> metIn)
        {
            if (prefab != null && NotMet(prefab)) return prefab;
            if (metIn == null || metIn.Count == 0) return prefab;
            foreach (var key in metIn) if (!NotMet(key)) return prefab ?? key;
            return metIn[0];
        }

        /// <summary>
        /// A chain's steps seen together (<see cref="Chain.Linked"/>), worked out once for what the
        /// character knows as handed over last, as the chain is asked about step by step each time
        /// the page is drawn.
        /// </summary>
        private HashSet<string> LinkedOf(Chain chain)
        {
            if (_linkedStamp != KnowStamp)
            {
                _linkedStamp = KnowStamp;
                _linked.Clear();
            }
            if (_linked.TryGetValue(chain, out var linked)) return linked;
            linked = chain.Linked((a, b) => _view.Related(Find(a), Find(b)));
            _linked[chain] = linked;
            return linked;
        }

        private readonly Dictionary<Chain, HashSet<string>> _linked = new Dictionary<Chain, HashSet<string>>();
        private int _linkedStamp = -1;
    }
}
