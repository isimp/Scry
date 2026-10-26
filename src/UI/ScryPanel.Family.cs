using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>A family's page (<see cref="Family"/>): the switch among the kinds the character met, and where those kinds differ.</summary>
    internal static partial class ScryPanel
    {
        private const float KindRowH = 26f;
        private const float KindRowGap = 4f;

        /// <summary>Where the switch's segments were laid last, by member, for the self-test.</summary>
        [Diagnostic]
        public static List<(Entry Member, Rect At)> KindSegmentsForTest { get; } = new List<(Entry, Rect)>();

        private static readonly List<(Entry Member, string Text, Rect At)> KindSegments = new List<(Entry, string, Rect)>();

        /// <summary>Lays the switch from a point: its caption, then a segment for each kind met, onto further rows where a row is full. How tall it is; none where there is nothing to switch among.</summary>
        private static float LayKindSwitch(Explorer explorer, Entry entry, float x, float y, float width)
        {
            KindSegments.Clear();
            var members = explorer.MembersMet(entry);
            if (members.Count == 0) return 0f;
            var captionW = Skin.Width(Skin.FaintLabel, CompendiumWords.KindsCaption) + U(8f);
            var at = x + captionW;
            var top = y;
            foreach (var member in members)
            {
                var text = CompendiumWords.KindSwitchLabel(explorer.KindLabel(member), explorer.LevelOf(member) == KnownLevel.Seen);
                var w = Skin.Width(Skin.Segment, text) + U(6f);
                if (at + w > x + width && at > x + captionW)
                {
                    at = x + captionW;
                    top += U(KindRowH + KindRowGap);
                }
                KindSegments.Add((member, text, new Rect(at, top, w, U(KindRowH))));
                at += w + U(4f);
            }
            return top - y + U(KindRowH + KindRowGap);
        }

        /// <summary>How tall the kind switch is at a width (<see cref="KindSwitch"/>).</summary>
        private static float KindSwitchHeight(Explorer explorer, Entry entry, float width) => LayKindSwitch(explorer, entry, 0f, 0f, width);

        /// <summary>
        /// The switch among the kinds of the page's family the character met, the one shown lit, one
        /// only seen saying so; a click shows that kind on the same page. Where it ends.
        /// </summary>
        private static float KindSwitch(Explorer explorer, Entry entry, float x, float y, float width)
        {
            var height = LayKindSwitch(explorer, entry, x, y, width);
            if (height <= 0f) return y;
            CountDrawn(PanelPart.KindSwitch);
            GUI.Label(new Rect(x, y, Skin.Width(Skin.FaintLabel, CompendiumWords.KindsCaption) + U(8f), U(KindRowH)), CompendiumWords.KindsCaption, Skin.FaintLabel);
            if (Event.current.type == EventType.Repaint) KindSegmentsForTest.Clear();
            Entry picked = null;
            foreach (var (member, text, at) in KindSegments)
            {
                var on = member == entry;
                if (GUI.Button(at, text, on ? Skin.SegmentOn : Skin.Segment) && !on) picked = member;
                if (Event.current.type == EventType.Repaint) KindSegmentsForTest.Add((member, at));
            }
            if (picked != null) explorer.SwitchTo(picked);
            return y + height;
        }

        private static Entry _familyFor;
        private static int _familyStamp = -1;
        private static Facts.Row _familyRow;

        /// <summary>Lets go of the family whose kinds were last set side by side.</summary>
        private static void ForgetKinds()
        {
            _familyFor = null;
            _familyRow = null;
            _familyStamp = -1;
        }

        /// <summary>
        /// A family's kinds the character met side by side where they differ, as a table under its
        /// own heading (<see cref="FamilyDiffers"/>): each kind's lines as its own page would show
        /// them, kept back as its page keeps them back, one only seen an empty column.
        /// </summary>
        private static float KindsSection(Explorer explorer, Entry entry, float width, float y)
        {
            if (!ReferenceEquals(entry, _familyFor) || _familyStamp != explorer.KnowStamp)
            {
                _familyFor = entry;
                _familyStamp = explorer.KnowStamp;
                _familyRow = KindsDiffer(explorer, explorer.MembersMet(entry));
            }
            if (_familyRow == null) return y;
            y = SectionHeading(CompendiumWords.KindsHeading, width, y, null, "kinds");
            if (IsFolded("kinds")) return y;
            CountDrawn(PanelPart.KindsDiffer);
            return TableRow(explorer, _familyRow, width, y) + U(6f);
        }

        /// <summary>The table of where the kinds met differ; none where they show alike or fewer than two were met.</summary>
        private static Facts.Row KindsDiffer(Explorer explorer, IReadOnlyList<Entry> members)
        {
            if (members.Count < 2) return null;
            var columns = new List<IReadOnlyList<KeyValuePair<string, string>>>();
            foreach (var member in members)
            {
                if (explorer.LevelOf(member) != KnownLevel.Known)
                {
                    columns.Add(null);
                    continue;
                }
                var facts = Facts.For(member);
                var lines = new List<KeyValuePair<string, string>>();
                foreach (var pair in facts.Pairs)
                {
                    string value;
                    if (facts.Links.TryGetValue(pair.Key, out var linked) && explorer.LeavesOutOn(member, linked, GameShows(explorer, facts, pair.Key))) value = CompendiumWords.Hidden;
                    else if (facts.Proofs.TryGetValue(pair.Key, out var proof) && explorer.KeepsBackOn(member, proof)) value = CompendiumWords.Hidden;
                    else value = Masked(explorer, pair.Value);
                    lines.Add(new KeyValuePair<string, string>(pair.Key, value));
                }
                columns.Add(lines);
            }
            var rows = FamilyDiffers.Rows(columns, CompendiumWords.Hidden);
            if (rows.Count == 0) return null;
            var headings = new string[members.Count + 1];
            headings[0] = "";
            for (var i = 0; i < members.Count; i++) headings[i + 1] = explorer.KindLabel(members[i]);
            var row = new Facts.Row { Title = CompendiumWords.KindsDifferTitle, Columns = headings };
            foreach (var (label, values) in rows)
            {
                var cells = new string[values.Length + 1];
                cells[0] = label;
                for (var i = 0; i < values.Length; i++) cells[i + 1] = values[i] ?? CompendiumWords.SeenRowTag;
                row.Lines.Add((cells, null));
            }
            return row;
        }
    }
}
