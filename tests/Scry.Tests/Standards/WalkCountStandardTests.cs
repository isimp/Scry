using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A dungeon's walked rooms are counted out of the rooms one can walk, its end caps and
    /// dividers left out (<see cref="DungeonMaps.Walked"/>), wherever the count is told: on a
    /// location's page, in its plan box and under a mark on Valheim's map alike. A count handed
    /// no kinds of room counts every wall cap as a room, so a dungeon walked through still reads
    /// as half walked.
    /// </summary>
    public class WalkCountStandardTests
    {
        [Fact]
        public void EveryWalkIsCountedByTheKindsOfRoomRead()
        {
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method) || method.Name != "Walked" || method.ContainingType?.Name != "DungeonMaps") continue;
                var args = call.ArgumentList.Arguments;
                if (args.Count < 2) continue;
                if (args[1].Expression is LambdaExpressionSyntax lambda && lambda.Body is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NullLiteralExpression))
                    found.Add($"{ScrySource.Where(call)} in {ScrySource.TopType(call)}.{ScrySource.Member(call)}");
            }
            Violations.None("count a dungeon's walked rooms by the kinds of room read (RoomKinds), not with none", found);
        }
    }
}
