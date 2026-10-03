using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Scry keeps two clocks. Its own timers (how long an effect plays, waiting a moment, how long
    /// its work took, the panel's tips) keep unscaled time, which runs on while the game is
    /// paused or slowed. What moves as the world moves keeps the game's own time, as the game's
    /// code moving it does, and stops with the world. Only these do.
    /// </summary>
    public class ClockStandardTests
    {
        private static readonly Dictionary<string, string> WorldTime = new Dictionary<string, string>
        {
            ["FallWatch.cs"] = "watches a copy fall under the game's physics",
            ["Flight.cs"] = "flies a projectile as the game's own fly",
            ["TrunkShake.cs"] = "shakes a felled trunk as its log falls under the game's physics",
            ["Turn.cs"] = "turns a windmill as Windmill.Update turns it",
            ["BiomeGround.cs"] = "gives the water's shader the time the game gives it",
        };

        [Fact]
        public void OnlyWhatMovesWithTheWorldKeepsTheGamesTime()
        {
            var found = new List<string>();
            foreach (var (access, model) in ScrySource.All<MemberAccessExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(access).Symbol is IPropertySymbol property) || property.ContainingType?.ToDisplayString() != "UnityEngine.Time") continue;
                var name = property.Name;
                if (name != "time" && name != "deltaTime" && name != "timeAsDouble" && name != "fixedTime" && name != "smoothDeltaTime") continue;
                if (WorldTime.ContainsKey(System.IO.Path.GetFileName(access.SyntaxTree.FilePath))) continue;
                found.Add($"{ScrySource.Where(access)} Time.{name} in {ScrySource.TopType(access)}.{ScrySource.Member(access)}");
            }
            Violations.None("keep the game's time for what does not move with the world", found);
        }
    }
}
