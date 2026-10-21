using System;

namespace Scry
{
    /// <summary>
    /// Marks a member Scry's own code never uses, there for the self-test to reach in by: to set up
    /// a part, to read what a part drew or worked out, or to stand in for the world. Scry's own code
    /// calls no such method, so what a player runs never takes a path only the self-test should; a
    /// member whose name ends in ForTest is one too, unmarked. A test holds both.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
    internal sealed class ForTestAttribute : Attribute
    {
    }
}
