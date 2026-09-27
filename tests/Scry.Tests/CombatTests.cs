using Xunit;

namespace Scry.Tests
{
    public class CombatTests
    {
        // Health at a level is the base times the level (Character.SetupMaxHealth); a star is a
        // level above the first. A hit at a level is 1 + half a star each (Attack.GetLevelDamageFactor).

        [Fact]
        public void EachStarAddsTheBaseHealthOnceMore()
        {
            Assert.Equal("one star 400, two stars 600", CombatWords.StarHealth(200f, 2));
        }

        [Fact]
        public void EachStarAddsHalfOfEveryHit()
        {
            Assert.Equal("one star ×1.5, two stars ×2", CombatWords.StarDamage(2));
            Assert.Equal("one star ×1.5, two stars ×2, three stars ×2.5", CombatWords.StarDamage(3));
        }

        [Fact]
        public void WithoutStarsThereIsNothingToSay()
        {
            Assert.Null(CombatWords.StarHealth(200f, 0));
            Assert.Null(CombatWords.StarDamage(0));
        }

        [Fact]
        public void DamageIsToldBiggestFirstLeavingOutWhatIsNone()
        {
            Assert.Equal("40 blunt, 20 fire", CombatWords.Damage(new[] { ("fire", 20f), ("slash", 0f), ("blunt", 40f) }));
            Assert.Equal("12.5 pierce", CombatWords.Damage(new[] { ("pierce", 12.5f) }));
            Assert.Null(CombatWords.Damage(new[] { ("blunt", 0f) }));
        }

        [Fact]
        public void AnAttackSaysWhatItDoesHowFarAndHowOften()
        {
            Assert.Equal("40 blunt · a swing, reaching 2.5 m · every 3 s", CombatWords.Attack("40 blunt", "Horizontal", 0f, 2.5f, 3f));
            Assert.Equal("a shot, from 5 to 30 m · every 10 s", CombatWords.Attack(null, "Projectile", 5f, 30f, 10f));
            Assert.Equal("20 fire · around it, reaching 4 m", CombatWords.Attack("20 fire", "Area", 0f, 4f, 0f));
        }
    }
}
