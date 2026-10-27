using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Scry.Tests
{
    public class CodeWarmupTests
    {
        // The runtime compiles a method the first time it runs, so the first page drawn in a
        // session, running hundreds of methods for the first time, takes longer than any after
        // it. Scry compiles its own code ahead, a little each frame, the panel's first, so a
        // player's first page finds it compiled.

        private abstract class Sample
        {
            static Sample() => Seen = 0;

            protected Sample()
            {
            }

            public static int Seen;

            public void Plain()
            {
            }

            public static int Shared() => 1;

            public int Value => 2;

            public Func<int> Later() => () => Seen + 3;

            public abstract void Left();

            public T Any<T>() => default;

            public T Valued<T>() where T : struct => default;

            public T Drawn<T>() where T : Sample => default;

            [DllImport("nowhere")]
            private static extern void Outside();
        }

        private sealed class Open<T>
        {
            public void Held()
            {
            }

            public T Kept<TOther>(TOther other) => default;
        }

        private sealed class Counted<T> where T : struct
        {
            public void Count()
            {
            }
        }

        private delegate void Hook();

        private sealed class Panel
        {
            public void Draw()
            {
            }
        }

        [Fact]
        public void EveryMethodWithABodyIsCompiledAsItsFirstCallWould()
        {
            var names = CodeWarmup.MethodsOf(new[] { typeof(Sample), typeof(Open<>), typeof(Hook) }).Select(m => m.Name).ToList();

            Assert.Contains(".ctor", names);
            Assert.Contains("Plain", names);
            Assert.Contains("Shared", names);
            Assert.Contains("get_Value", names);
            Assert.Contains("Later", names);
            // What has no body of its own waits for its first call.
            Assert.DoesNotContain("Left", names);
            Assert.DoesNotContain("Outside", names);
            Assert.DoesNotContain("Invoke", names);
            // A type's static setup runs as the type is first used, never by the warming.
            Assert.DoesNotContain(".cctor", names);
        }

        // Generic code is compiled once for every class and anew for each value type it is
        // given. Compiled over object, it is the code any class it is first called with shares,
        // so a page's first sort or count of its classes finds it compiled.

        [Fact]
        public void GenericCodeEveryClassSharesIsCompiledAheadOnce()
        {
            var methods = CodeWarmup.MethodsOf(new[] { typeof(Sample), typeof(Open<>) }).ToList();

            var any = Assert.Single(methods, m => m.Name == "Any");
            Assert.Equal(new[] { typeof(object) }, ((MethodInfo)any).GetGenericArguments());
            var held = Assert.Single(methods, m => m.Name == "Held");
            Assert.Equal(typeof(Open<object>), held.DeclaringType);
            var kept = Assert.Single(methods, m => m.Name == "Kept");
            Assert.Equal(typeof(Open<object>), kept.DeclaringType);
            Assert.Equal(new[] { typeof(object) }, ((MethodInfo)kept).GetGenericArguments());
            Assert.All(methods, m => Assert.False(m.ContainsGenericParameters, m.Name + " still waits for types"));
        }

        [Fact]
        public void GenericCodeForAValueTypeOrAParticularClassWaitsForItsFirstCall()
        {
            // Object cannot stand for a value type, nor for a class it is not.
            var names = CodeWarmup.MethodsOf(new[] { typeof(Sample), typeof(Counted<>) }).Select(m => m.Name).ToList();

            Assert.DoesNotContain("Valued", names);
            Assert.DoesNotContain("Drawn", names);
            Assert.DoesNotContain("Count", names);
            Assert.Contains("Any", names);
        }

        [Fact]
        public void TheCodeOfTheTypesNamedFirstIsCompiledFirst()
        {
            var methods = CodeWarmup.MethodsOf(new[] { typeof(Sample), typeof(Panel) }, typeof(Panel)).ToList();

            Assert.Equal(typeof(Panel), methods[0].DeclaringType);
            Assert.Contains(methods.Skip(1), m => m.DeclaringType == typeof(Sample));
        }

        [Fact]
        public void ATypesOwnNestedCodeComesWithIt()
        {
            // A lambda's body is a method of a type the compiler nests in the one it is written in.
            var methods = CodeWarmup.MethodsOf(new[] { typeof(Panel), typeof(Sample) }.Concat(typeof(Sample).GetNestedTypes(BindingFlags.NonPublic)), typeof(Sample)).ToList();
            var lastOfSample = methods.FindLastIndex(m => Within(m.DeclaringType, typeof(Sample)));
            var firstOfPanel = methods.FindIndex(m => m.DeclaringType == typeof(Panel));

            Assert.True(lastOfSample < firstOfPanel, "the nested code of the type named first comes before the rest");
            Assert.Contains(methods, m => m.DeclaringType != typeof(Sample) && Within(m.DeclaringType, typeof(Sample)));
        }

        [Fact]
        public void ItCompilesWithinTheFramesShareAndGoesOnTheNextFrame()
        {
            var clock = 0.0;
            var warmup = new CodeWarmup(Methods(10));

            warmup.Step(() => clock, 2.0, m => { clock += 0.5; return true; });

            Assert.Equal(4, warmup.Compiled);
            Assert.False(warmup.Done);
            for (var frame = 0; frame < 10 && !warmup.Done; frame++)
            {
                clock = 0;
                warmup.Step(() => clock, 2.0, m => { clock += 0.5; return true; });
            }
            Assert.True(warmup.Done);
            Assert.Equal(10, warmup.Compiled);
        }

        [Fact]
        public void AMethodLongerThanTheShareStillLetsTheWarmingGoOn()
        {
            var clock = 0.0;
            var warmup = new CodeWarmup(Methods(3));

            warmup.Step(() => clock, 2.0, m => { clock += 10; return true; });

            Assert.Equal(1, warmup.Compiled);
            Assert.False(warmup.Done);
        }

        [Fact]
        public void AMethodThatCannotBeCompiledIsCountedAndPassedOver()
        {
            var warmup = new CodeWarmup(Methods(3));
            var calls = 0;

            warmup.Step(() => 0, 2.0, m => ++calls != 2);

            Assert.True(warmup.Done);
            Assert.Equal(2, warmup.Compiled);
            Assert.Equal(1, warmup.Failed);
        }

        [Fact]
        public void NothingToCompileIsDoneAtItsFirstStep()
        {
            var warmup = new CodeWarmup(new List<MethodBase>());
            warmup.Step(() => 0, 2.0, m => throw new InvalidOperationException("nothing is compiled"));
            Assert.True(warmup.Done);
            Assert.Equal(0, warmup.Compiled);
        }

        [Fact]
        public void TheCodeIsLookedUpOnlyAsTheWarmingReachesIt()
        {
            // Looking up every type's methods at once would cost a frame of its own.
            var looked = 0;
            var methods = new[] { typeof(Panel), typeof(Sample) }.SelectMany(t => { looked++; return CodeWarmup.MethodsOf(new[] { t }); });
            var warmup = new CodeWarmup(methods);

            warmup.Step(() => 0, 2.0, m => true);

            Assert.True(warmup.Done);
            Assert.Equal(2, looked);
            var fresh = new CodeWarmup(new[] { typeof(Panel), typeof(Sample) }.SelectMany(t => { looked++; return CodeWarmup.MethodsOf(new[] { t }); }));
            var clock = 0.0;
            fresh.Step(() => clock, 2.0, m => { clock += 10; return true; });
            Assert.Equal(3, looked);
        }

        // Compiling Scry's own methods leaves what they call: generic code over a value type (a
        // list of pairs, a count over them), which is compiled anew for each value type, and the
        // game's and Unity's code. A first page calls hundreds of those for the first time, so
        // they are compiled ahead with the code that calls them.

        private struct Pair
        {
            public string Name;
            public int Count;
        }

        private static class Inner<T>
        {
            public static int Outer(T value) => Deeper(value) + 1;

            public static int Deeper(T value) => Deepest(value);

            public static int Deepest(T value) => 0;
        }

        private interface IShown
        {
            void Show();
        }

        private sealed class Page
        {
            public int Tell()
            {
                var pairs = new List<Pair> { new Pair { Name = "a", Count = 1 } };
                return pairs.Count(p => p.Count > 0) + Inner<int>.Outer(1) + Inner<string>.Outer("a");
            }

            public void Told(IShown shown) => shown.Show();

            public void Twice() => Tell();
        }

        private sealed class Hooked
        {
            public string Name = "";

            public bool Calls() => string.IsNullOrEmpty(Name);
        }

        private static List<MethodBase> Warmed(Type type, Func<MethodBase, IEnumerable<MethodBase>> hooks = null) =>
            CodeWarmup.WithWhatTheyCall(CodeWarmup.MethodsOf(new[] { type }.Concat(type.GetNestedTypes(BindingFlags.NonPublic))), hooks).ToList();

        [Fact]
        public void TheGenericCodeAPageRunsOverAValueTypeIsCompiledAheadWithIt()
        {
            var methods = Warmed(typeof(Page));

            Assert.Contains(methods, m => m.DeclaringType == typeof(List<Pair>) && m.Name == "Add");
            Assert.Contains(methods, m => m is ConstructorInfo && m.DeclaringType == typeof(List<Pair>));
            Assert.Contains(methods, m => m.Name == "Count" && m.DeclaringType == typeof(Enumerable) && m.GetGenericArguments().SequenceEqual(new[] { typeof(Pair) }));
            Assert.All(methods, m => Assert.False(m.ContainsGenericParameters, m.Name + " still waits for types"));
        }

        [Fact]
        public void CodeMadeForAValueTypeIsFollowedInToWhatItCalls()
        {
            // Inner<int>.Outer runs code of its own made for int, which calls more of it: each is
            // compiled anew at its first call, a few steps in, further than others' code is followed.
            var methods = CodeWarmup.WithWhatTheyCall(CodeWarmup.MethodsOf(new[] { typeof(Page) }), othersDeep: 1).ToList();

            Assert.Contains(methods, m => m.DeclaringType == typeof(Inner<int>) && m.Name == "Outer");
            Assert.Contains(methods, m => m.DeclaringType == typeof(Inner<int>) && m.Name == "Deeper");
            Assert.Contains(methods, m => m.DeclaringType == typeof(Inner<int>) && m.Name == "Deepest");
        }

        [Fact]
        public void CodeEveryClassSharesIsLeftToItsOwnTurn()
        {
            // Inner<string> runs the code every class shares, which Scry's own generic code
            // compiled over object already is, so neither it nor what it calls comes from here.
            var methods = Warmed(typeof(Page));

            Assert.DoesNotContain(methods, m => m.DeclaringType == typeof(Inner<string>));
        }

        [Fact]
        public void WhatACallLandsOnOnlyAtRunTimeIsLeftToItsCall()
        {
            // A call through an interface lands on whatever the object is, unknown until then.
            var methods = Warmed(typeof(Page));

            Assert.DoesNotContain(methods, m => m.Name == nameof(IShown.Show));
        }

        [Fact]
        public void EachMethodIsCompiledOnceHoweverOftenItIsCalled()
        {
            var methods = Warmed(typeof(Page));

            Assert.Equal(methods.Count, methods.Distinct().Count());
            Assert.Single(methods, m => m.DeclaringType == typeof(List<Pair>) && m.Name == "Add");
            Assert.Single(methods, m => m.DeclaringType == typeof(Page) && m.Name == nameof(Page.Tell));
        }

        [Fact]
        public void WhatAMethodCallsIsCompiledRightAfterIt()
        {
            // The panel's code comes first, and so does what it calls, before the rest of Scry.
            var methods = CodeWarmup.WithWhatTheyCall(CodeWarmup.MethodsOf(new[] { typeof(Page), typeof(Panel) }, typeof(Page))).ToList();
            var add = methods.FindIndex(m => m.DeclaringType == typeof(List<Pair>) && m.Name == "Add");
            var panel = methods.FindIndex(m => m.DeclaringType == typeof(Panel));

            Assert.True(add >= 0 && add < panel, "the page's calls come before the next type's code");
        }

        private static readonly MethodBase Original = typeof(string).GetMethod(nameof(string.IsNullOrEmpty));
        private static readonly MethodBase ModsHook = typeof(Math).GetMethod(nameof(Math.Abs), new[] { typeof(int) });

        [Fact]
        public void TheHooksModsPutOnACalledMethodAreCompiledWithIt()
        {
            // A mod's hook on the game's code runs when Scry calls that code (a tooltip asked for).
            var methods = CodeWarmup.WithWhatTheyCall(new[] { typeof(Hooked).GetMethod(nameof(Hooked.Calls)) }, m => m == Original ? new[] { ModsHook } : Array.Empty<MethodBase>()).ToList();

            Assert.Contains(Original, methods);
            Assert.Contains(ModsHook, methods);
            Assert.True(methods.IndexOf(Original) < methods.IndexOf(ModsHook), "the hook comes with the method it is on");
        }

        [Fact]
        public void HooksThatCannotBeToldLeaveTheCallsCompiled()
        {
            // Asking for a method's hooks can fail (a mod's patch half made); what is called is compiled all the same.
            var methods = CodeWarmup.WithWhatTheyCall(new[] { typeof(Hooked).GetMethod(nameof(Hooked.Calls)) }, m => throw new InvalidOperationException("no hooks")).ToList();

            Assert.Contains(methods, m => m.Name == nameof(Hooked.Calls));
            Assert.Contains(Original, methods);
        }

        [Fact]
        public void OthersCodeIsFollowedOnlyALittleWayIn()
        {
            // The game's code calls on into more of its own; a step past what Scry calls is
            // compiled, and no further, which would reach most of the game.
            var calls = typeof(Hooked).GetMethod(nameof(Hooked.Calls));
            var near = CodeWarmup.WithWhatTheyCall(new[] { calls }, othersDeep: 1).ToList();
            var further = CodeWarmup.WithWhatTheyCall(new[] { calls }, othersDeep: 2).ToList();
            var length = typeof(string).GetProperty(nameof(string.Length)).GetMethod;

            Assert.Equal(new[] { calls, Original }, near);
            Assert.Contains(length, further);
            Assert.True(further.Count < 50, $"{further.Count} methods: it stays a step in");
        }

        [Fact]
        public void ACallIntoAnArraysOwnMethodsIsLeftToTheRuntime()
        {
            // A many-dimensional array's Get and Set are made by the runtime, never compiled from code.
            var methods = Warmed(typeof(Grid));

            Assert.DoesNotContain(methods, m => m.DeclaringType != null && m.DeclaringType.IsArray);
        }

        private sealed class Grid
        {
            public int At(int[,] cells) => cells[1, 1];
        }

        private static List<MethodBase> Methods(int count) =>
            Enumerable.Repeat((MethodBase)typeof(Panel).GetMethod(nameof(Panel.Draw)), count).ToList();

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
