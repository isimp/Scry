using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Scry
{
    /// <summary>
    /// Scry's own code compiled ahead, a little each frame, before a player first runs it. The
    /// runtime compiles a method the first time it is called, about a tenth of a millisecond
    /// each, so the first page drawn in a session, which calls hundreds of methods for the first
    /// time, takes far longer than any page after it. Compiled in the frames after a world's
    /// catalog is read, the panel's code first, that cost falls where nobody sees it. Every method
    /// with a body of its own and no types left to give is compiled, as its first call would; a
    /// type's static setup is never run by it, so it still runs where the type is first used.
    /// </summary>
    internal sealed class CodeWarmup
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly IEnumerator<MethodBase> _methods;

        /// <summary>The methods to compile, looked up only as the warming reaches them, so finding them costs no frame of its own.</summary>
        public CodeWarmup(IEnumerable<MethodBase> methods) => _methods = methods.GetEnumerator();

        /// <summary>How many methods were compiled, and how many could not be.</summary>
        public int Compiled { get; private set; }

        public int Failed { get; private set; }

        /// <summary>Whether every method has been reached.</summary>
        public bool Done { get; private set; }

        /// <summary>
        /// The methods of these types that compiling ahead takes: the code of the types named
        /// first, their nested types' with them (where a lambda's body is), before all the rest,
        /// each in the order given. Each type's methods are looked up only as they are reached.
        /// </summary>
        public static IEnumerable<MethodBase> MethodsOf(IEnumerable<Type> types, params Type[] first)
        {
            var all = types.ToList();
            var ordered = all.Where(t => first.Any(f => Within(t, f))).Concat(all.Where(t => !first.Any(f => Within(t, f))));
            return ordered.SelectMany(t => t.GetMethods(Declared).Cast<MethodBase>().Concat(t.GetConstructors(Declared)).Where(HasOwnBody));
        }

        /// <summary>Compiles on while the frame's share lasts, the time taken from this step's start, so one method longer than the share is still compiled and the next frame goes on.</summary>
        public void Step(Func<double> elapsedMs, double budgetMs, Func<MethodBase, bool> compile)
        {
            while (!Done && elapsedMs() < budgetMs)
            {
                if (!_methods.MoveNext())
                {
                    Done = true;
                    _methods.Dispose();
                    return;
                }
                if (compile(_methods.Current)) Compiled++;
                else Failed++;
            }
        }

        /// <summary>Whether a method has a body to compile now: not abstract, not waiting for types, not made by the runtime or outside it, and no type's static setup.</summary>
        private static bool HasOwnBody(MethodBase method)
        {
            if (method.IsAbstract || method.ContainsGenericParameters) return false;
            if (method is ConstructorInfo && method.IsStatic) return false;
            if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0) return false;
            var made = method.MethodImplementationFlags;
            return (made & (MethodImplAttributes.InternalCall | MethodImplAttributes.Runtime)) == 0;
        }

        private static bool Within(Type type, Type outer)
        {
            for (var t = type; t != null; t = t.DeclaringType)
            {
                if (t == outer) return true;
            }
            return false;
        }
    }
}
