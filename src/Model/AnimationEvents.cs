using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The animation events Scry answers on a preview copy: those the game's
    /// <c>CharacterAnimEvent</c> and <c>AnimationEffect</c> answer. A copy's animations sound only
    /// when every event its clips send is one of these, since an event nothing hears makes Unity
    /// log an error each time it fires; the startup check fails a feature for an event the game
    /// answers that is not here.
    /// </summary>
    internal static class AnimationEvents
    {
        private static readonly HashSet<string> Answered = new HashSet<string>(StringComparer.Ordinal)
        {
            "FootStep", "Hit", "OnAttackTrigger", "Jump", "Land", "TakeOff", "Stop", "DodgeMortal",
            "TrailOn", "TrailOff", "GPower", "Die", "Speed", "Chain", "ResetChain", "FreezeFrame",
            "Effect", "Attach", "RemoveAttachments", "HideObject", "ShowObject",
        };

        /// <summary>Whether Scry answers an animation event of this name, as it is written.</summary>
        public static bool Answers(string name) => name != null && Answered.Contains(name);
    }
}
