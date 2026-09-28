using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// Keeps the game's hands off the keyboard and mouse while the panel is open.
    ///
    /// The game already stops moving, looking, attacking and opening its menu while a text box is
    /// up: <c>Player.TakeInput</c>, <c>PlayerController.TakeInput</c>, the mouse capture in
    /// <c>GameCamera.UpdateMouseCapture</c> and the Escape check in <c>Menu.Update</c> all ask
    /// <c>TextInput.IsVisible</c>. Answering yes while the panel is open gets all of that at once,
    /// and frees the cursor.
    ///
    /// Each patch runs inside one of the game's own methods, every frame, so none may ever fail
    /// there: its work is in a method of its own, called inside a try, so that even a member an
    /// update renamed (which fails the method naming it before it runs) is caught. One that fails
    /// is told once and stands aside for the rest of the session, leaving the game as it would be
    /// without Scry.
    /// </summary>
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class TextInputBlock
    {
        private static bool _off;

        private static void Postfix(ref bool __result)
        {
            if (_off) return;
            try { if (Blocks()) __result = true; }
            catch (Exception ex) { _off = true; Faults.Tell("keeping the game's keys away while the panel is open", ex); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Blocks() => Session.BlocksInput;
    }

    /// <summary>
    /// The inventory opens on its key (Tab unless rebound) without asking <c>TextInput.IsVisible</c>:
    /// <c>InventoryGui.Update</c> checks only the chat, console, menu, text viewer, cutscenes, free
    /// fly and the map. So Tab to complete a search would open it. While one of the panel's text
    /// boxes has the keyboard the press is let go before the inventory looks at it, as the game
    /// itself lets go of one it has handled (<c>ZInput.ResetButtonStatus</c>).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "Update")]
    internal static class InventoryKeyBlock
    {
        private static bool _off;

        private static void Prefix()
        {
            if (_off) return;
            try { LetGo(); }
            catch (Exception ex) { _off = true; Faults.Tell("keeping Tab from opening the inventory while typing in the panel", ex); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LetGo()
        {
            if (Session.IsOpen && ScryPanel.Typing && ZInput.instance != null) ZInput.ResetButtonStatus("Inventory");
        }
    }

    /// <summary>
    /// Holding the right mouse button outside the panel turns the camera. The game asks for mouse
    /// look separately (<c>TakeInput(look: true)</c>) from movement and actions, so only that one
    /// question is answered yes; walking, blocking and attacking stay off.
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class LookThrough
    {
        private static bool _off;

        private static void Postfix(bool look, ref bool __result)
        {
            if (_off) return;
            try { if (Lets(look)) __result = true; }
            catch (Exception ex) { _off = true; Faults.Tell("looking around and walking while the panel is open", ex); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Lets(bool look) => look ? Session.Looking : Session.Walking;
    }

    /// <summary>
    /// While the panel is open and walking is allowed, the character gets its movement but never
    /// its combat: a click on the panel must not swing a weapon, and the right mouse button used
    /// for looking around must not raise a shield.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class NoCombatWhileOpen
    {
        private static bool _off;

        private static void Prefix(ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold,
            ref bool block, ref bool blockHold, ref bool dodge)
        {
            if (_off) return;
            try
            {
                if (!Open()) return;
                attack = attackHold = secondaryAttack = secondaryAttackHold = false;
                block = blockHold = dodge = false;
            }
            catch (Exception ex)
            {
                _off = true;
                Faults.Tell("keeping attacks, blocks and dodges from going off while the panel is open", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Open() => Session.IsOpen;
    }

    /// <summary>While looking around, the cursor is captured as in normal play.</summary>
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
    internal static class LookCapture
    {
        private static bool _off;

        private static void Postfix()
        {
            if (_off) return;
            try { Capture(); }
            catch (Exception ex) { _off = true; Faults.Tell("holding the cursor while looking around", ex); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Capture()
        {
            if (!Session.Looking) return;
            ZCursor.LockState = UnityEngine.CursorLockMode.Locked;
            ZCursor.Hide();
        }
    }

    /// <summary>
    /// The mouse wheel is not covered by the text box check: the camera zoom reads it regardless.
    /// Every wheel read in the game goes through this one method, so while the panel is open it
    /// answers nothing, and the wheel only scrolls the panel.
    /// </summary>
    [HarmonyPatch(typeof(ZInput), "Internal_GetMouseScrollWheel")]
    internal static class WheelBlock
    {
        private static bool _off;

        private static void Postfix(ref float __result)
        {
            if (_off) return;
            try { if (Blocks()) __result = 0f; }
            catch (Exception ex) { _off = true; Faults.Tell("keeping the mouse wheel from zooming the game camera while over the panel", ex); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Blocks() => Session.BlocksInput;
    }
}
