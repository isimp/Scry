using System;
using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class StepsTests
    {
        // Every part of Scry's work runs on its own: a part that fails hands its failure back to
        // be told and stops only itself; what must pass through (the GUI's way of leaving a pass)
        // does.

        private sealed class PassThrough : Exception
        {
        }

        private static bool Passes(Exception ex) => ex is PassThrough;

        [Fact]
        public void APartThatFinishesHandsBackNothing()
        {
            var ran = false;

            Assert.Null(Steps.Run(() => ran = true, Passes));
            Assert.True(ran);
        }

        [Fact]
        public void APartThatFailsHandsBackItsFailure()
        {
            var thrown = new InvalidOperationException("odd prefab");

            Assert.Same(thrown, Steps.Run(() => throw thrown, Passes));
        }

        [Fact]
        public void AFailureStopsOnlyItsOwnPart()
        {
            var ran = new List<string>();

            Steps.Run(() => { ran.Add("first"); throw new InvalidOperationException(); }, Passes);
            Steps.Run(() => ran.Add("second"), Passes);

            Assert.Equal(new[] { "first", "second" }, ran);
        }

        [Fact]
        public void WhatMustPassThroughDoes()
        {
            Assert.Throws<PassThrough>(() => Steps.Run(() => throw new PassThrough(), Passes));
        }

        [Fact]
        public void APartThatWorksOutAValueHandsItBack()
        {
            Assert.Null(Steps.Run(() => 42, out var value, Passes));
            Assert.Equal(42, value);
        }

        [Fact]
        public void APartThatFailsWorkingOutAValueHandsBackItsFailureAndNoValue()
        {
            var thrown = new InvalidOperationException("odd prefab");

            Assert.Same(thrown, Steps.Run<string>(() => throw thrown, out var value, Passes));
            Assert.Null(value);
        }

        [Fact]
        public void WhatMustPassThroughWorkingOutAValueDoes()
        {
            Assert.Throws<PassThrough>(() => Steps.Run<int>(() => throw new PassThrough(), out _, Passes));
        }

        [Fact]
        public void WithNothingToPassThroughEveryFailureIsHandedBack()
        {
            Assert.IsType<PassThrough>(Steps.Run(() => throw new PassThrough(), null));
        }

        [Fact]
        public void APartTakesWhatItWorksOn()
        {
            var seen = 0;

            Assert.Null(Steps.Run(n => seen = n, 42, Passes));
            Assert.Equal(42, seen);
            Assert.IsType<InvalidOperationException>(Steps.Run<int>(n => throw new InvalidOperationException(), 1, Passes));
            Assert.Throws<PassThrough>(() => Steps.Run<int>(n => throw new PassThrough(), 1, Passes));
        }
    }
}
