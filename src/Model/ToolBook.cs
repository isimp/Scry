using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Which build tools build which pieces, and on which tab. A tool is any item with a build
    /// table (<c>ItemDrop.ItemData.SharedData.m_buildPieces</c>): the hammer, the hoe, the
    /// cultivator and any mod's own. A piece may be in several tools.
    /// </summary>
    public sealed class ToolBook
    {
        /// <summary>A tool a piece is built with, and the tab it is on there.</summary>
        public sealed class Use
        {
            public string Tool = "";
            public string ToolName = "";
            public string Tab = "";
            public int TabOrder;
        }

        private readonly List<(Use Use, string Piece)> _all = new List<(Use, string)>();

        public void Clear() => _all.Clear();

        /// <summary>Notes that a tool builds a piece on a tab, the tab's place in the tool's own order given.</summary>
        public void Add(string tool, string toolName, string piece, string tab, int tabOrder)
        {
            if (string.IsNullOrEmpty(tool) || string.IsNullOrEmpty(piece)) return;
            if (_all.Exists(e => e.Piece == piece && e.Use.Tool == tool && e.Use.Tab == tab)) return;
            _all.Add((new Use { Tool = tool, ToolName = toolName ?? tool, Tab = tab ?? "", TabOrder = tabOrder }, piece));
        }

        /// <summary>Whether an item builds anything.</summary>
        public bool IsTool(string tool) => _all.Exists(e => e.Use.Tool == tool);

        /// <summary>The tools a piece is built with, each once, in the order they were noted.</summary>
        public IReadOnlyList<Use> ToolsOf(string piece)
        {
            var uses = new List<Use>();
            foreach (var (use, built) in _all)
            {
                if (built == piece && !uses.Exists(u => u.Tool == use.Tool)) uses.Add(use);
            }
            return uses;
        }

        /// <summary>What a tool builds, tab by tab in its own order, each piece once on a tab.</summary>
        public IReadOnlyList<(string Tab, List<string> Pieces)> PiecesOf(string tool)
        {
            return _all.Where(e => e.Use.Tool == tool)
                .GroupBy(e => e.Use.Tab, StringComparer.Ordinal)
                .OrderBy(g => g.Min(e => e.Use.TabOrder))
                .Select(g => (g.Key, g.Select(e => e.Piece).ToList()))
                .ToList();
        }
    }
}
