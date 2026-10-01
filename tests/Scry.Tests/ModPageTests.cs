using Xunit;

namespace Scry.Tests
{
    public class ModPageTests
    {
        // Every mod loaded is an entry of its own, a page telling what it adds and changes. It is
        // no prefab, so it has a key of its own ("mod:" and its name), a tab of its own, and is
        // listed by whether it adds to the game, only hooks into what Scry tells of it, or neither.

        [Fact]
        public void AModIsKeptUnderAKeyOfItsOwn()
        {
            Assert.Equal("mod:Monstrum", EntryKeys.For(Kind.Mod, "Monstrum"));
            Assert.Equal("Monstrum", EntryKeys.Split("mod:Monstrum", out var kind));
            Assert.Equal(Kind.Mod, kind);
            Assert.True(EntryKeys.HasOwnNamespace(Kind.Mod));
        }

        [Fact]
        public void ModsHaveATabAndASearchTermOfTheirOwn()
        {
            Assert.Equal("Mods", Kinds.Label(Kind.Mod));
            Assert.True(Search.KindMatches(Kind.Mod, "mod"));
            Assert.False(Search.KindMatches(Kind.Item, "mod"));
        }

        [Fact]
        public void ModsAreListedByWhetherTheyAddOrOnlyHookIn()
        {
            var adds = Groups.Mod(adds: true, hooks: true);
            var hooks = Groups.Mod(adds: false, hooks: true);
            var neither = Groups.Mod(adds: false, hooks: false);

            Assert.Equal("Adding to the game", adds.Name);
            Assert.Equal(adds, Groups.Mod(adds: true, hooks: false));
            Assert.Equal("Hooking into what Scry tells", hooks.Name);
            Assert.Equal("Neither, as far as Scry sees", neither.Name);
            Assert.True(adds.Order < hooks.Order && hooks.Order < neither.Order);
        }

        [Fact]
        public void AModsCardTellsItsVersionAndId()
        {
            Assert.Equal("Version 1.6.0, id Therzie.Monstrum", ModWords.Card(new ModSource { Name = "Monstrum", Version = "1.6.0", Guid = "Therzie.Monstrum" }));
            Assert.Equal("Id Therzie.Monstrum", ModWords.Card(new ModSource { Name = "Monstrum", Guid = "Therzie.Monstrum" }));
        }
    }
}
