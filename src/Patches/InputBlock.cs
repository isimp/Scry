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
    /// </summary>
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class TextInputBlock
    {
        private static void Postfix(ref bool __result)
        {
            if (Session.BlocksInput) __result = true;
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
        private static void Postfix(ref float __result)
        {
            if (Session.BlocksInput) __result = 0f;
        }
    }
}
