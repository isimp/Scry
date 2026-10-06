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

            [DllImport("nowhere")]
            private static extern void Outside();
        }

        private sealed class Open<T>
        {
            public void Held()
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
            // What has no body of its own, or none until it is given types, waits for its first call.
            Assert.DoesNotContain("Left", names);
            Assert.DoesNotContain("Any", names);
            Assert.DoesNotContain("Outside", names);
            Assert.DoesNotContain("Held", names);
            Assert.DoesNotContain("Invoke", names);
            // A type's static setup runs as the type is first used, never by the warming.
            Assert.DoesNotContain(".cctor", names);
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
