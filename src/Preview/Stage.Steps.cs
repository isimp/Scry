using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The steps between the rooms of a dungeon's example: going into a room, from room to room
    /// and back out are steps of a history (<see cref="ViewSteps{T}"/>), which the mouse's back and
    /// forward buttons, Alt and the arrows and the panel's ‹ and › walk before the entries shown.
    /// Back returns to the view a step was taken from as it was left; changing the floor opened is
    /// no step. The steps go with the example they are of.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>A view of a dungeon's example stepped back or forward to: the room gone into, else the floor opened and how the view stood on it.</summary>
        private sealed class ExampleView
        {
            public PlacedRoom Room;
            public int Level;
            public float Zoom;
            public Vector3 Pan;
            public Vector3? Kept;
        }

        private static readonly ViewSteps<ExampleView> Steps = new ViewSteps<ExampleView>();

        /// <summary>Whether Back steps out of or between the rooms of the dungeon's inside shown, rather than to the entry shown before.</summary>
        public static bool CanStepBack => StepsShown && Steps.CanGoBack;

        /// <summary>Whether Forward steps into a room come out of, or one gone back from, rather than to the entry gone back from.</summary>
        public static bool CanStepForward => StepsShown && Steps.CanGoForward;

        /// <summary>Whether the inside the steps are of is on the stage.</summary>
        private static bool StepsShown => HasInside && ShowsInside;

        /// <summary>Keeps the view now as the one a step into or out of a room is taken from.</summary>
        private static void TookStep() => Steps.Took(ViewNow());

        /// <summary>Lets go of the steps, as the example they are of is taken down.</summary>
        private static void ForgetSteps() => Steps.Clear();

        /// <summary>Steps back to the view the last step was taken from; false with none.</summary>
        public static bool StepBack()
        {
            if (!CanStepBack || !Steps.Back(ViewNow(), out var to)) return false;
            ShowView(to);
            return true;
        }

        /// <summary>Takes the step gone back from again; false with none.</summary>
        public static bool StepForward()
        {
            if (!CanStepForward || !Steps.Forward(ViewNow(), out var to)) return false;
            ShowView(to);
            return true;
        }
    }
}
