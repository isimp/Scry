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
    internal sealed class ToolBook
    {
        /// <summary>A tool a piece is built with, and the tab it is on there.</summary>
        public sealed class Use
        {
            public string Tool = "";
            public string ToolName = "";
            public string Tab = "";
            public int TabOrder;
        }

        // Kept by piece and by tool, each in the order noted, as a mod such as PlanBuild puts
        // thousands of pieces in a tool and the mod report asks about every one.
        private readonly Dictionary<string, List<Use>> _byPiece = new Dictionary<string, List<Use>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<(Use Use, string Piece)>> _byTool = new Dictionary<string, List<(Use, string)>>(StringComparer.Ordinal);
        private readonly HashSet<(string Piece, string Tool, string Tab)> _noted = new HashSet<(string, string, string)>();

        /// <summary>Each tool's pieces by tab, once asked for, until something is noted again.</summary>
        private readonly Dictionary<string, IReadOnlyList<(string Tab, List<string> Pieces)>> _tabs = new Dictionary<string, IReadOnlyList<(string, List<string>)>>(StringComparer.Ordinal);

        public void Clear()
        {
            _byPiece.Clear();
            _byTool.Clear();
            _noted.Clear();
            _tabs.Clear();
        }

        /// <summary>Notes that a tool builds a piece on a tab, the tab's place in the tool's own order given.</summary>
        public void Add(string tool, string toolName, string piece, string tab, int tabOrder)
        {
            if (string.IsNullOrEmpty(tool) || string.IsNullOrEmpty(piece)) return;
            if (!_noted.Add((piece, tool, tab ?? ""))) return;
            var use = new Use { Tool = tool, ToolName = toolName ?? tool, Tab = tab ?? "", TabOrder = tabOrder };
            if (!_byPiece.TryGetValue(piece, out var uses)) _byPiece[piece] = uses = new List<Use>();
            uses.Add(use);
            if (!_byTool.TryGetValue(tool, out var built)) _byTool[tool] = built = new List<(Use, string)>();
            built.Add((use, piece));
            _tabs.Remove(tool);
        }

        /// <summary>Whether an item builds anything.</summary>
        public bool IsTool(string tool) => tool != null && _byTool.ContainsKey(tool);

        /// <summary>The tools a piece is built with, each once, in the order they were noted.</summary>
        public IReadOnlyList<Use> ToolsOf(string piece)
        {
            var uses = new List<Use>();
            if (piece == null || !_byPiece.TryGetValue(piece, out var noted)) return uses;
            foreach (var use in noted)
            {
                if (!uses.Exists(u => u.Tool == use.Tool)) uses.Add(use);
            }
            return uses;
        }

        /// <summary>What a tool builds, tab by tab in its own order, each piece once on a tab.</summary>
        public IReadOnlyList<(string Tab, List<string> Pieces)> PiecesOf(string tool)
        {
            if (tool == null || !_byTool.TryGetValue(tool, out var built)) return new List<(string, List<string>)>();
            if (_tabs.TryGetValue(tool, out var known)) return known;
            var tabs = built
                .GroupBy(e => e.Use.Tab, StringComparer.Ordinal)
                .OrderBy(g => g.Min(e => e.Use.TabOrder))
                .Select(g => (g.Key, g.Select(e => e.Piece).ToList()))
                .ToList();
            _tabs[tool] = tabs;
            return tabs;
        }
    }
}
