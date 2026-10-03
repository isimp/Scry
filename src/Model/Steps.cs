using System;

namespace Scry
{
    /// <summary>
    /// A part of Scry's work run on its own: a failure is handed back to be told and stops only
    /// that part, so the parts after it still run; what must pass through (the GUI's way of
    /// leaving a pass) does. This is the one place in Scry a failure is caught; the game's side
    /// of it, timing and telling, is <c>Guard</c>. None of the forms makes anything for the
    /// call, so a part run many times a frame costs nothing more for being run this way.
    /// </summary>
    internal static class Steps
    {
        /// <summary>Runs a part; null when it finished, else its failure.</summary>
        public static Exception Run(Action step, Func<Exception, bool> passes)
        {
            try
            {
                step();
                return null;
            }
            catch (Exception ex) when (passes == null || !passes(ex))
            {
                return ex;
            }
        }

        /// <summary>Works out a value; null with the value when it finished, else its failure and no value.</summary>
        public static Exception Run<T>(Func<T> read, out T value, Func<Exception, bool> passes)
        {
            try
            {
                value = read();
                return null;
            }
            catch (Exception ex) when (passes == null || !passes(ex))
            {
                value = default;
                return ex;
            }
        }

        /// <summary>Runs a part on what it works on, with nothing made for the call; null when it finished, else its failure.</summary>
        public static Exception Run<T>(Action<T> step, T state, Func<Exception, bool> passes)
        {
            try
            {
                step(state);
                return null;
            }
            catch (Exception ex) when (passes == null || !passes(ex))
            {
                return ex;
            }
        }
    }
}
