using Xunit;

namespace Scry.Tests
{
    public class TameMessageTests
    {
        // The game tells the player closest to a creature it tamed "<its name> $hud_tamedone" in the
        // middle of the screen; that message names the creature tamed, by its name token.

        [Fact]
        public void TheTamingMessageNamesTheCreatureTamed()
        {
            Assert.Equal("$enemy_boar", TameMessage.Tamed("$enemy_boar $hud_tamedone"));
        }

        [Fact]
        public void AnyOtherMessageNamesNone()
        {
            Assert.Null(TameMessage.Tamed("$msg_added Wood"));
            Assert.Null(TameMessage.Tamed(" $hud_tamedone"));
            Assert.Null(TameMessage.Tamed(null));
        }
    }
}
