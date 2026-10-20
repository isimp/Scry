using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class IlNamesTests
    {
        // A mod's hook into a creature's death counts as a hook into its drops only when its own
        // code names the drop list; the fields, methods and types a method's code names are read
        // from its IL, each as the token its module resolves.

        [Fact]
        public void TheFieldsMethodsAndTypesACodeNamesAreRead()
        {
            // ldarg.0; ldfld 0x04000012; call 0x0A000034; isinst 0x02000005; ret
            var il = new byte[] { 0x02, 0x7B, 0x12, 0x00, 0x00, 0x04, 0x28, 0x34, 0x00, 0x00, 0x0A, 0x75, 0x05, 0x00, 0x00, 0x02, 0x2A };

            Assert.Equal(new[] { 0x04000012, 0x0A000034, 0x02000005 }, IlShape.Names(il));
        }

        [Fact]
        public void AMethodThatCanOnlyAnswerFalseIsTold()
        {
            // A hook acting only on the game's true can never act where the game answers false
            // whatever happens, as RuneStone.Interact does: each of its returns gives false.
            Assert.True(IlShape.OnlyFalse(new byte[] { 0x16, 0x2A }));
            // ldarg.1; brtrue.s +2; ldc.i4.0; ret; ldc.i4.0; ret
            Assert.True(IlShape.OnlyFalse(new byte[] { 0x03, 0x2D, 0x02, 0x16, 0x2A, 0x16, 0x2A }));
            Assert.False(IlShape.OnlyFalse(new byte[] { 0x17, 0x2A }));
            Assert.False(IlShape.OnlyFalse(new byte[] { 0x03, 0x2D, 0x02, 0x16, 0x2A, 0x17, 0x2A }));
            // What it answers through a local is not plain: ldc.i4.0; stloc.0; ldloc.0; ret.
            Assert.False(IlShape.OnlyFalse(new byte[] { 0x16, 0x0A, 0x06, 0x2A }));
            // A return jumped to may give what was pushed before the jump: ldarg.1; brtrue.s +1; ldc.i4.0; ret.
            Assert.False(IlShape.OnlyFalse(new byte[] { 0x03, 0x2D, 0x01, 0x16, 0x2A }));
            // A long jump and a switch name returns too: ldarg.1; brtrue +1; ldc.i4.0; ret / ldarg.1; switch (1) +1; ldc.i4.0; ret.
            Assert.False(IlShape.OnlyFalse(new byte[] { 0x03, 0x3A, 0x01, 0x00, 0x00, 0x00, 0x16, 0x2A }));
            Assert.False(IlShape.OnlyFalse(new byte[] { 0x03, 0x45, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x16, 0x2A }));
            // No return, or code that cannot be read, tells nothing.
            Assert.False(IlShape.OnlyFalse(new byte[] { 0x16 }));
            Assert.False(IlShape.OnlyFalse(null));
            Assert.False(IlShape.OnlyFalse(new byte[] { 0xFE }));
        }

        [Fact]
        public void NumbersJumpsAndStringsAreNotNames()
        {
            // ldc.i4 7; ldstr 0x70000001; br.s +0; ldc.r8 1.0; switch (1) +0; ldtoken 0x02000009; ret
            var il = new byte[]
            {
                0x20, 0x07, 0x00, 0x00, 0x00,
                0x72, 0x01, 0x00, 0x00, 0x70,
                0x2B, 0x00,
                0x23, 0, 0, 0, 0, 0, 0, 0xF0, 0x3F,
                0x45, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xD0, 0x09, 0x00, 0x00, 0x02,
                0x2A,
            };

            Assert.Equal(new[] { 0x02000009 }, IlShape.Names(il));
        }

        [Fact]
        public void TwoByteStepsAreReadThrough()
        {
            // ldarg 1 (FE 09, two-byte operand); ldftn 0x06000001 (FE 06); ret
            var il = new byte[] { 0xFE, 0x09, 0x01, 0x00, 0xFE, 0x06, 0x01, 0x00, 0x00, 0x06, 0x2A };

            Assert.Equal(new[] { 0x06000001 }, IlShape.Names(il));
        }

        [Fact]
        public void CodeThatCannotBeReadNamesNothing()
        {
            Assert.Empty(IlShape.Names(null));
            Assert.Empty(IlShape.Names(new byte[0]));
            Assert.Empty(IlShape.Names(new byte[] { 0x7B, 0x12 }));

            // A step no opcode has, after a name: none of it is taken, not the part before.
            Assert.Empty(IlShape.Names(new byte[] { 0x7B, 0x12, 0x00, 0x00, 0x04, 0xA6, 0x2A }));
        }
    }
}
