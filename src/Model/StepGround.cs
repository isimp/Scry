using System;

namespace Scry
{
    /// <summary>
    /// The grounds a footstep can be heard on, with the values the game gives them
    /// (<c>FootStep.GroundMaterial</c>), so one is the other cast; a test holds them equal.
    /// </summary>
    [Flags]
    internal enum StepGround
    {
        None = 0,
        Default = 1,
        Water = 2,
        Stone = 4,
        Wood = 8,
        Snow = 0x10,
        Mud = 0x20,
        Grass = 0x40,
        GenericGround = 0x80,
        Metal = 0x100,
        Tar = 0x200,
        Ashlands = 0x400,
        Lava = 0x800,
        SnowDeep = 0x1000,
        SnowVeryDeep = 0x2000,
        Ice = 0x4000,
    }
}
