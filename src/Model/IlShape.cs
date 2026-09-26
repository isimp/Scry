using System.Reflection;
using System.Reflection.Emit;

namespace Scry
{
    /// <summary>
    /// A fingerprint of a method's code: the sequence of its steps (its IL opcodes), leaving out
    /// what they name and where they jump. The fields and methods a step names are numbered anew
    /// in every build of the game, so the raw code changes with every update; its steps only
    /// change when the method itself was rewritten. Zero means the code could not be read.
    /// </summary>
    public static class IlShape
    {
        private static readonly OpCode?[] OneByte = new OpCode?[256];
        private static readonly OpCode?[] TwoByte = new OpCode?[256];
        private static bool _built;

        public static uint Of(byte[] il)
        {
            if (il == null || il.Length == 0) return 0;
            Build();

            var hash = 2166136261u;
            var i = 0;
            while (i < il.Length)
            {
                OpCode? op;
                if (il[i] == 0xFE)
                {
                    if (i + 1 >= il.Length) return 0;
                    op = TwoByte[il[i + 1]];
                    i += 2;
                }
                else
                {
                    op = OneByte[il[i]];
                    i += 1;
                }
                if (op == null) return 0;

                var value = (ushort)op.Value.Value;
                hash = (hash ^ (byte)(value >> 8)) * 16777619u;
                hash = (hash ^ (byte)value) * 16777619u;

                var skip = OperandSize(op.Value, il, i);
                if (skip < 0 || i + skip > il.Length) return 0;
                i += skip;
            }
            return hash == 0 ? 1u : hash;
        }

        private static int OperandSize(OpCode op, byte[] il, int at)
        {
            switch (op.OperandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    if (at + 4 > il.Length) return -1;
                    var targets = il[at] | (il[at + 1] << 8) | (il[at + 2] << 16) | (il[at + 3] << 24);
                    return targets < 0 ? -1 : 4 + 4 * targets;
                default:
                    return 4;
            }
        }

        private static void Build()
        {
            if (_built) return;
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(field.GetValue(null) is OpCode op)) continue;
                var value = (ushort)op.Value;
                if (op.Size == 1) OneByte[value & 0xFF] = op;
                else TwoByte[value & 0xFF] = op;
            }
            _built = true;
        }
    }
}
