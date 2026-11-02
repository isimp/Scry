using Xunit;

namespace Scry.Tests
{
    public class OpenKeysTests
    {
        // The key opening Scry may come with Shift, Ctrl or Alt, so it stays clear of other mods'
        // F keys; keys held besides, as one walking holds them, never stop it.

        [Fact]
        public void AKeyAloneOpensWhateverIsHeldBeside()
        {
            Assert.True(OpenKeys.Fires(true, KeyMods.None, KeyMods.None));
            Assert.True(OpenKeys.Fires(true, KeyMods.None, KeyMods.Shift));
            Assert.False(OpenKeys.Fires(false, KeyMods.None, KeyMods.None));
        }

        [Fact]
        public void AKeyWithModifiersOpensOnlyWithThemHeld()
        {
            Assert.True(OpenKeys.Fires(true, KeyMods.Shift, KeyMods.Shift));
            Assert.False(OpenKeys.Fires(true, KeyMods.Shift, KeyMods.None));
            Assert.False(OpenKeys.Fires(true, KeyMods.Shift | KeyMods.Control, KeyMods.Shift));
            Assert.True(OpenKeys.Fires(true, KeyMods.Shift | KeyMods.Control, KeyMods.Control | KeyMods.Shift | KeyMods.Alt));
            Assert.False(OpenKeys.Fires(false, KeyMods.Shift, KeyMods.Shift));
        }

        [Fact]
        public void AKeyIsNamedAsAPlayerSaysIt()
        {
            Assert.Equal("F7", OpenKeys.Name("F7", KeyMods.None));
            Assert.Equal("Shift+F7", OpenKeys.Name("F7", KeyMods.Shift));
            Assert.Equal("Ctrl+Alt+Shift+F7", OpenKeys.Name("F7", KeyMods.Alt | KeyMods.Shift | KeyMods.Control));
            Assert.Equal("1", OpenKeys.Name("Alpha1", KeyMods.None));
            Assert.Equal("Keypad 5", OpenKeys.Name("Keypad5", KeyMods.None));
            Assert.Equal("Ctrl+K", OpenKeys.Name("K", KeyMods.Control));
        }

        [Fact]
        public void TypingInTheSearchKeepsAKeyThatTypesFromOpening()
        {
            // A letter or digit typed into the search is text, Ctrl+V pasting or AltGr typing a sign
            // included; only a key that itself wants Ctrl or Alt types nothing there and opens.
            Assert.True(OpenKeys.TypesText(true, KeyMods.None));
            Assert.True(OpenKeys.TypesText(true, KeyMods.Shift));
            Assert.False(OpenKeys.TypesText(true, KeyMods.Control));
            Assert.False(OpenKeys.TypesText(true, KeyMods.Alt | KeyMods.Shift));
            Assert.False(OpenKeys.TypesText(false, KeyMods.None));
        }
    }
}
