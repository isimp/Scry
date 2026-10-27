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
    /// with a body of its own is compiled, as its first call would. A type's static constructor
    /// still runs where the type is first used; a type with field initializers and no static
    /// constructor is set up as code reading its fields is compiled, in Valheim's Mono as well
    /// (seen in it running alone), so earlier than its first use.
    /// </summary>
    /// <remarks>
    /// What Scry's code calls is compiled with it (<see cref="WithWhatTheyCall"/>): generic code
    /// over a value type it names, which no compiling of Scry's own covers, and the game's and
    /// Unity's code with the hooks mods put on it, which a first page runs for the first time too.
    /// Generic code is compiled once for every class and anew for each value type it is given,
    /// so a generic method or type is compiled over object, the code any class it is first called
    /// with shares. One that needs a value type or a particular class waits for its first call.
    /// </remarks>
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
            return ordered.SelectMany(CodeOf).Where(HasOwnBody);
        }

        /// <summary>How many steps into others' code (the game's, Unity's, the runtime's) is compiled from Scry's: what Scry calls, what that calls, and what that calls in turn.</summary>
        public const int OthersDeep = 3;

        /// <summary>How many steps into code made for a value type is compiled, each step of it compiled anew at its first call.</summary>
        public const int ValueTypeDeep = 3;

        /// <summary>
        /// Each method followed by what its first run would compile with it: the methods its code
        /// calls, read from that code, and the hooks mods put on them (<paramref name="hooks"/>).
        /// Code made for a value type is followed in to what it calls (<see cref="ValueTypeDeep"/>),
        /// as is others' code a little way (<see cref="OthersDeep"/>). A call into the methods'
        /// own module that is not made for a value type is left to that method's own turn, and a
        /// call through an interface or to an abstract method waits for the object it lands on.
        /// Each method comes once, its calls looked up only as the warming reaches it.
        /// </summary>
        public static IEnumerable<MethodBase> WithWhatTheyCall(IEnumerable<MethodBase> methods, Func<MethodBase, IEnumerable<MethodBase>> hooks = null, int othersDeep = OthersDeep, int valueTypeDeep = ValueTypeDeep)
        {
            var seen = new HashSet<MethodBase>();
            var waiting = new Stack<(MethodBase Method, int Deep)>();
            foreach (var root in methods)
            {
                if (!seen.Add(root)) continue;
                yield return root;
                var home = root.Module;
                waiting.Push((root, 0));
                while (waiting.Count > 0)
                {
                    var (method, deep) = waiting.Pop();
                    var found = new List<MethodBase>();
                    foreach (var called in CalledBy(method))
                    {
                        // A method whose types cannot be read (one of them missing) is left to its call.
                        if (Steps.Run(() => HowMade(called, home), out var made, null) != null || made == Made.Elsewhere || !seen.Add(called)) continue;
                        found.Add(called);
                        if (deep + 1 < (made == Made.ForValueType ? valueTypeDeep : othersDeep)) waiting.Push((called, deep + 1));
                        foreach (var hook in HooksOn(called, hooks))
                        {
                            if (seen.Add(hook)) found.Add(hook);
                        }
                    }
                    foreach (var each in found) yield return each;
                }
            }
        }

        /// <summary>The hooks mods put on a method; none where they cannot be told.</summary>
        private static IEnumerable<MethodBase> HooksOn(MethodBase method, Func<MethodBase, IEnumerable<MethodBase>> hooks)
        {
            if (hooks == null) return Array.Empty<MethodBase>();
            var failed = Steps.Run(() => hooks(method)?.Where(h => h != null && !h.ContainsGenericParameters && HasOwnBody(h)).ToList(), out var found, null);
            return failed == null && found != null ? found : (IEnumerable<MethodBase>)Array.Empty<MethodBase>();
        }

        private enum Made
        {
            /// <summary>Compiled at another time: in its own turn, by the runtime, or at the call that tells what it lands on.</summary>
            Elsewhere,
            ForValueType,
            Others,
        }

        /// <summary>How a method called from code of this module is compiled.</summary>
        private static Made HowMade(MethodBase method, Module home)
        {
            if (!HasOwnBody(method) || method.DeclaringType?.IsArray == true) return Made.Elsewhere;
            if (MadeForValueType(method)) return Made.ForValueType;
            return method.Module == home ? Made.Elsewhere : Made.Others;
        }

        /// <summary>Whether a method's code is made for a value type it is given, so compiled anew for it, not shared with every class.</summary>
        private static bool MadeForValueType(MethodBase method)
        {
            if (method.IsGenericMethod && method.GetGenericArguments().Any(t => t.IsValueType)) return true;
            var type = method.DeclaringType;
            return type != null && type.IsGenericType && type.GetGenericArguments().Any(t => t.IsValueType);
        }

        /// <summary>
        /// The methods a method's code names in a call, a new object or a delegate made of it, each
        /// read in the method's own types; none of a method without code to read, and a name that
        /// cannot be found (what a game update removed) passed over.
        /// </summary>
        private static List<MethodBase> CalledBy(MethodBase method)
        {
            var called = new List<MethodBase>();
            if (Steps.Run(() => method.GetMethodBody()?.GetILAsByteArray(), out var il, null) != null || il == null) return called;
            var typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
            foreach (var token in IlShape.Names(il, methodsOnly: true))
            {
                // A method or type the code names that is not there is passed over.
                if (Steps.Run(() => method.Module.ResolveMethod(token, typeArgs, methodArgs), out var found, null) == null && found != null && !found.ContainsGenericParameters) called.Add(found);
            }
            return called;
        }

        /// <summary>A type's methods and constructors, a generic type's and a generic method's over object (<see cref="Shared(Type)"/>); none of a generic type object cannot stand in.</summary>
        private static IEnumerable<MethodBase> CodeOf(Type type)
        {
            var made = type.IsGenericTypeDefinition ? Shared(type) : type;
            if (made == null) yield break;
            foreach (var method in made.GetMethods(Declared))
            {
                var shared = method.IsGenericMethodDefinition ? Shared(method) : method;
                if (shared != null) yield return shared;
            }
            foreach (var constructor in made.GetConstructors(Declared)) yield return constructor;
        }

        /// <summary>A generic type over object for each of its types, the code every class shares; null where object cannot stand in.</summary>
        private static Type Shared(Type type)
        {
            var parameters = type.GetGenericArguments();
            return Takes(parameters) ? type.MakeGenericType(Objects(parameters.Length)) : null;
        }

        /// <summary>A generic method over object for each of its types; null where object cannot stand in.</summary>
        private static MethodInfo Shared(MethodInfo method)
        {
            var parameters = method.GetGenericArguments();
            return Takes(parameters) ? method.MakeGenericMethod(Objects(parameters.Length)) : null;
        }

        /// <summary>
        /// Whether object meets every one of these type parameters' constraints: none but object's
        /// own. A value type asked for is a constraint too, of <see cref="ValueType"/>.
        /// </summary>
        private static bool Takes(Type[] parameters)
        {
            foreach (var parameter in parameters)
            {
                foreach (var constraint in parameter.GetGenericParameterConstraints())
                {
                    if (constraint != typeof(object)) return false;
                }
            }
            return true;
        }

        private static Type[] Objects(int count)
        {
            var objects = new Type[count];
            for (var i = 0; i < count; i++) objects[i] = typeof(object);
            return objects;
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
