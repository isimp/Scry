using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// What of the character's own ran in another player's game, for the compendium's strict
    /// rules (<see cref="Elsewhere"/>): a kill the game that ran it tells the character of, as it
    /// tells each player who hit the creature (<c>Game.RPC_RegisterKill</c>, sent from that
    /// game, where one this game ran comes with no sender or its own); a pick of the character's
    /// of a thing another game runs, which <c>Pickable.Interact</c> sends there; and a thing the
    /// character hit that another game runs going soon after (<c>ZNetScene.OnZDODestroyed</c>,
    /// which every game runs as a thing goes).
    /// </summary>
    internal static class ElsewhereWatch
    {
        /// <summary>A kill the game tells the character of, by the creature's name: counted where another game sent it.</summary>
        public static void KillTold(long sender, string name)
        {
            if (sender == 0L || sender == ZNet.GetUID() || string.IsNullOrEmpty(name)) return;
            Guard.Run(Feature.CompendiumTogether, "noting a kill run in another player's game", KilledElsewhere, name);
        }

        private static readonly System.Action<string> KilledElsewhere = name => CompendiumKeeper.KilledElsewhere(name);

        /// <summary>
        /// Whether the character's use of a pickable sends a pick to another player's game: one
        /// not picked, not turned off and not stuck in tar, that this game does not run.
        /// </summary>
        public static bool Picking(Pickable pickable, Humanoid character)
        {
            if (pickable == null || character == null || character != Player.m_localPlayer) return false;
            var view = pickable.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || view.IsOwner() || pickable.GetPicked() || pickable.GetEnabled == 0) return false;
            var floating = pickable.m_tarPreventsPicking ? pickable.GetComponent<Floating>() : null;
            return floating == null || !floating.IsInTar();
        }

        /// <summary>A pick sent to another player's game (<see cref="Picking"/>), counted as such.</summary>
        public static void Picked(Pickable pickable, bool elsewhere)
        {
            if (!elsewhere) return;
            Guard.Run(Feature.CompendiumTogether, "noting a pick run in another player's game", PickedElsewhere, pickable);
        }

        private static readonly System.Action<Pickable> PickedElsewhere = pickable => CompendiumKeeper.PickedElsewhere(pickable);

        /// <summary>A thing in the world gone, and whether this game ran it as it went.</summary>
        public static void Gone(ZDO zdo)
        {
            if (zdo == null) return;
            Guard.Run(Feature.CompendiumTogether, "noting a thing gone", Went, zdo);
        }

        private static readonly System.Action<ZDO> Went = zdo => CompendiumKeeper.Gone(zdo.m_uid, zdo.IsOwner());
    }

    [HarmonyPatch(typeof(Game), nameof(Game.RPC_RegisterKill))]
    internal static class KillToldWatched
    {
        private static void Postfix(long sender, string enemyName) => ElsewhereWatch.KillTold(sender, enemyName);
    }

    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
    internal static class PickSentWatched
    {
        private static void Prefix(Pickable __instance, Humanoid character, out bool __state) => __state = ElsewhereWatch.Picking(__instance, character);

        private static void Postfix(Pickable __instance, bool __state) => ElsewhereWatch.Picked(__instance, __state);
    }

    [HarmonyPatch(typeof(ZNetScene), "OnZDODestroyed")]
    internal static class GoneWatched
    {
        private static void Prefix(ZDO zdo) => ElsewhereWatch.Gone(zdo);
    }
}
