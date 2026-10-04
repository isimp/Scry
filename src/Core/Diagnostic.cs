using System;

namespace Scry
{
    /// <summary>
    /// Marks a member whose text is for whoever looks after Scry rather than a player: a line for
    /// the log, a report for the self-test or the console's dump. What a player reads is worded in
    /// the model; text such a member puts together stays where it is made.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, Inherited = false)]
    internal sealed class DiagnosticAttribute : Attribute
    {
    }
}
