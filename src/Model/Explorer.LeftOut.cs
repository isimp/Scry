using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The faint lines a page says at a section's end of what the rules left out of it.</summary>
    internal sealed partial class Explorer
    {
        /// <summary>
        /// The faint lines at a section's end telling how many things its page left out, each said
        /// as what to do and what it counts by the section (<see cref="CompendiumWords.MeetLine"/>),
        /// its hover saying the way to them: under the strict rules what was never met
        /// (<paramref name="unmet"/>) apart from what was met but not seen together
        /// (<paramref name="apart"/>, <see cref="CompendiumWords.SeeLine"/>), for every section;
        /// under the loose ones, which leave out only what was never met, one line for every
        /// section. Without "more" where the section lists nothing else (<paramref name="alone"/>);
        /// the page's <paramref name="name"/> and a room's one kind (<paramref name="holds"/>) as the
        /// lines say them. Where the kinds of all a line counts are given, its hover counts them by
        /// kind too (<see cref="CompendiumWords.ByKind"/>).
        /// </summary>
        public List<(string Line, string Tip)> LeftOutLines(string topic, int unmet, int apart, bool alone = false, string name = null, Kind? holds = null,
            IReadOnlyCollection<Kind> unmetKinds = null, IReadOnlyCollection<Kind> apartKinds = null)
        {
            var lines = new List<(string, string)>();
            if (!LeavingOut)
            {
                var all = unmetKinds != null && apartKinds != null ? unmetKinds.Concat(apartKinds).ToList() : null;
                var line = CompendiumWords.MeetLine(topic, unmet + apart, alone, name, holds);
                if (line != null) lines.Add((line, Tip(CompendiumWords.MeetFirstTip(strict: false), unmet + apart, all)));
                return lines;
            }
            // Strict: what was never met waits to be met first, what was met waits to be seen together; each its own line.
            if (unmet > 0) lines.Add((CompendiumWords.MeetLine(topic, unmet, alone, name, holds), Tip(CompendiumWords.MeetFirstTip(strict: true), unmet, unmetKinds)));
            if (apart > 0) lines.Add((CompendiumWords.SeeLine(topic, apart, name, holds), Tip(CompendiumWords.TogetherTip(topic), apart, apartKinds)));
            return lines;
        }

        /// <summary>A line's hover, its count by kind under it where the kinds of all it counts are known.</summary>
        private static string Tip(string tip, int count, IReadOnlyCollection<Kind> kinds) =>
            kinds != null && kinds.Count == count ? Naming.Lines(tip, CompendiumWords.ByKind(kinds)) : tip;

        /// <summary>
        /// What a card left out of a list it names, by the keys left out, each counted as never met
        /// (<see cref="NotMet"/>) or as met but not seen together: read after the names in a
        /// sentence, as a raid's or a room's peek (<see cref="CompendiumWords.NeverMet"/>), or as
        /// lines of their own under a room card's list (<paramref name="lines"/>,
        /// <see cref="LeftOutLines"/>), naming the one kind they are of where they are all of one.
        /// </summary>
        public List<string> LeftOutCounts(string topic, IEnumerable<string> leftOut, bool alone = false, bool lines = false)
        {
            var unmet = 0;
            var apart = 0;
            var kinds = new HashSet<Kind?>();
            foreach (var key in leftOut)
            {
                if (NotMet(key)) unmet++;
                else apart++;
                kinds.Add(Find(key)?.Kind);
            }
            var holds = kinds.Count == 1 ? kinds.First() : null;
            if (lines) return LeftOutLines(topic, unmet, apart, alone, holds: holds).ConvertAll(l => l.Line);
            var said = new List<string>();
            if (!LeavingOut) unmet += apart;
            if (unmet > 0) said.Add(CompendiumWords.NeverMet(unmet, alone, holds));
            if (LeavingOut && apart > 0) said.Add(CompendiumWords.LeftOutOf(topic, apart, alone, holds));
            return said;
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
