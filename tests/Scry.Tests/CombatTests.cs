using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CombatTests
    {
        // Health at a level is the base times the level (Character.SetupMaxHealth); a star is a
        // level above the first. A hit at a level is 1 + half a star each (Attack.GetLevelDamageFactor).

        [Fact]
        public void AnAttackKnownOnlyByItsCodeNameIsNamedByWhatItFires()
        {
            // A troll's rock throw is "fireballattack" in the game's data: named by its projectile, not "Fireball attack".
            Assert.Equal("Troll throw", CombatWords.FiredName("Troll throw projectile"));
            Assert.Equal("Goblin shaman fireball", CombatWords.FiredName("Goblin shaman projectile fireball"));
            Assert.Equal("Spit", CombatWords.FiredName("Spit"));
            // A projectile named for nothing but being one tells nothing better.
            Assert.Null(CombatWords.FiredName("Projectile"));
            Assert.Null(CombatWords.FiredName(""));
        }

        [Fact]
        public void WhatStarsAddIsATableOfHealthAndDamageByStar()
        {
            Assert.Equal("With stars", CombatWords.StarsTitle);
            Assert.Equal(new[] { "", "No stars", "1 star", "2 stars" }, CombatWords.StarColumns(2));
            var lines = CombatWords.StarLines(200f, 2);
            // Each star adds the base health once more, and half of every hit.
            Assert.Equal(new[] { "Health", "200", "400", "600" }, lines[0]);
            Assert.Equal(new[] { "Damage", "×1", "×1.5", "×2" }, lines[1]);
            Assert.Equal(new[] { "Damage", "×1", "×1.5", "×2", "×2.5" }, CombatWords.StarLines(200f, 3)[1]);
            Assert.Equal(2, lines.Count);
        }

        [Fact]
        public void ACreatureWhoseDropsGrowWithStarsHasThemInItsStarsTable()
        {
            // CharacterDrop.GenerateDropList doubles a drop's amount and chance for each star,
            // for each drop with m_levelMultiplier set whose amount stars do not leave (DropWords.WithStars).
            var lines = CombatWords.StarLines(200f, 2, dropsGrow: true);
            Assert.Equal(3, lines.Count);
            Assert.Equal(new[] { "Drops", "×1", "×2", "×4" }, lines[2]);
            Assert.Equal(new[] { "Drops", "×1", "×2", "×4", "×8" }, CombatWords.StarLines(200f, 3, dropsGrow: true)[2]);
        }

        [Fact]
        public void ADropThatOnlyGetsLikelierWithStarsHasItsChanceAtEachStar()
        {
            // CharacterDrop.GenerateDropList doubles the chance of a drop with m_levelMultiplier for
            // each star, but leaves its amount where m_levelMultiplierNotAmount is set, as on every
            // creature's trophy since 1.0.20; a chance past a whole one is a sure drop.
            Assert.Equal(new[] { "Frost troll trophy", "50%", "100%", "100%" }, CombatWords.StarChance("Frost troll trophy", 0.5f, 2));
            Assert.Equal(new[] { "Wolf trophy", "10%", "20%", "40%", "80%" }, CombatWords.StarChance("Wolf trophy", 0.1f, 3));
            Assert.Equal(new[] { "Neck trophy", "5%", "10%", "20%" }, CombatWords.StarChance("Neck trophy", 0.05f, 2));
        }

        [Fact]
        public void WithoutStarsNoDropGetsAChanceLine()
        {
            Assert.Null(CombatWords.StarChance("Frost troll trophy", 0.5f, 0));
        }

        [Fact]
        public void WithoutStarsThereIsNothingToSay()
        {
            Assert.Empty(CombatWords.StarLines(200f, 0));
        }

        [Fact]
        public void DamageIsToldBiggestFirstLeavingOutWhatIsNone()
        {
            Assert.Equal("40 blunt, 20 fire", CombatWords.Damage(new[] { ("fire", 20f), ("slash", 0f), ("blunt", 40f) }));
            Assert.Equal("12.5 pierce", CombatWords.Damage(new[] { ("pierce", 12.5f) }));
            Assert.Null(CombatWords.Damage(new[] { ("blunt", 0f) }));
        }

        [Fact]
        public void ACreaturesHitDealsFromAbout87To100PercentOfItsDamageRolledEachHit()
        {
            // Character.GetRandomSkillFactor for all but a player: the square root of an even roll from 0.75 to 1.
            var (least, most) = SkillMath.CreatureHitShare;
            Assert.Equal(0.866f, least, 3);
            Assert.Equal(1f, most);
            Assert.Equal("48–55 slash, 9–10 fire", CombatWords.CreatureDamage(new[] { ("fire", 10f), ("slash", 55f) }));
            // Where the roll rounds to one number, one number.
            Assert.Equal("1 blunt", CombatWords.CreatureDamage(new[] { ("blunt", 1f) }));
            Assert.Null(CombatWords.CreatureDamage(new[] { ("blunt", 0f) }));
        }

        [Fact]
        public void AnAttackIsALineOfItsTableSayingWhatItDoesHowFarAndHowOften()
        {
            // Its name, its damage, how it is made, its reach and how often its AI uses it; a dash where it tells none.
            Assert.Equal(new[] { "Attack", "Damage", "How", "Reach", "Every" }, CombatWords.AttackColumns);
            Assert.Equal(new[] { "Troll club", "40 blunt", "a swing", "2.5 m", "3 s" }, CombatWords.AttackCells("Troll club", "40 blunt", "Horizontal", 0f, 2.5f, 3f));
            Assert.Equal(new[] { "Spit", "–", "a shot", "5–30 m", "10 s" }, CombatWords.AttackCells("Spit", null, "Projectile", 5f, 30f, 10f));
            Assert.Equal(new[] { "Stomp", "20 fire", "around it", "4 m", "–" }, CombatWords.AttackCells("Stomp", "20 fire", "Area", 0f, 4f, 0f));
        }

        [Fact]
        public void AnAttackNamesWhatItFiresAndWhatItPutsOnYou()
        {
            // An attack that fires a projectile names it, whatever kind of attack it is.
            Assert.Equal(new[] { "Throw", "–", "fires Troll rock", "5–30 m", "10 s" }, CombatWords.AttackCells("Throw", null, "Projectile", 5f, 30f, 10f, fires: "Troll rock"));
            Assert.Equal("fires Spit", CombatWords.AttackCells("Spit", null, "Horizontal", 0f, 3f, 2f, fires: "Spit")[2]);

            // The table has a column of what each puts on you only where one does.
            Assert.Equal(CombatWords.AttackColumns, CombatWords.AttackColumnsFor(onHit: false));
            Assert.Equal(new[] { "Attack", "Damage", "How", "On hit", "Reach", "Every" }, CombatWords.AttackColumnsFor(onHit: true));
            var cells = CombatWords.AttackCells("Bite", "30 poison", "Horizontal", 0f, 2f, 3f);
            Assert.Equal(new[] { "Bite", "30 poison", "a swing", "Poison", "2 m", "3 s" }, CombatWords.OnHitCells(cells, "Poison"));
            Assert.Equal(new[] { "Bite", "30 poison", "a swing", "–", "2 m", "3 s" }, CombatWords.OnHitCells(cells, null));
            Assert.Equal(new[] { "Bite", "5 slash", "an attack", "–", "–" }, CombatWords.AttackCells("Bite", "5 slash", "Other", 0f, 0f, 0f));
            Assert.Equal("Attacks", CombatWords.AttacksTitle);
            // Every kind of swing and of shot the game has reads as one.
            Assert.Equal("a swing", CombatWords.AttackCells("Slam", null, "Vertical", 0f, 0f, 0f)[2]);
            Assert.Equal("a shot", CombatWords.AttackCells("Throw", null, "TriggerProjectile", 0f, 0f, 0f)[2]);
        }

        [Fact]
        public void SightTellsTheWholeFieldOfViewAndThatAnAlertedCreatureSeesAllRound()
        {
            // BaseAI.CanSeeTarget turns away a target more than m_viewAngle off the creature's
            // forward, either side, and only while it is not alerted.
            Assert.Equal("30 m, 180° ahead, all round once alerted", CombatWords.Sight(30f, 90f));
            Assert.Equal("40 m, 120° ahead, all round once alerted", CombatWords.Sight(40f, 60f));
            Assert.Equal("25 m, all round", CombatWords.Sight(25f, 180f));
            Assert.Equal("25 m, all round", CombatWords.Sight(25f, 200f));
        }

        [Fact]
        public void AlertRangeSaysHowNearItMustSeeYouToTurnOnYou()
        {
            // MonsterAI turns alerted on seeing its target within m_alertRange times the target's
            // stealth factor, which is below one only while the target sneaks.
            Assert.Equal("on seeing you within 90 m, nearer while you sneak", CombatWords.Alerted(90f));
            Assert.Equal("on seeing you within 12.5 m, nearer while you sneak", CombatWords.Alerted(12.5f));
        }

        [Fact]
        public void TheGamesUnlimitedAlertRangeIsNotTold()
        {
            // 9,999 is the field's default: whatever it sees, it turns on, which sight already tells.
            Assert.Null(CombatWords.Alerted(9999f));
        }

        [Fact]
        public void NoAlertRangeMeansSightAloneNeverAlertsIt()
        {
            // The check is "nearer than the range", which nothing is at a range of zero.
            Assert.Equal("never on sight alone", CombatWords.Alerted(0f));
        }

        [Fact]
        public void ChaseDistanceSaysHowFarFromItsSpawnItFollows()
        {
            // MonsterAI gives up once farther than m_maxChaseDistance from where it spawned and a
            // second has passed without sensing its target; none is set means no limit.
            Assert.Equal("beyond 500 m from where it spawned, once it has lost you", CombatWords.Chase(500f));
            Assert.Null(CombatWords.Chase(0f));
        }

        [Fact]
        public void ACreatureThePassiveEnemiesSettingDoesNotCalmIsToldSo()
        {
            // With the world setting Passive enemies on, a creature senses no one until provoked
            // (BaseAI.CanSenseTarget), unless it is marked m_passiveAggresive: then it hunts as
            // ever, and an animal, which only flees, flees as ever.
            Assert.Equal("attacks unprovoked all the same", CombatWords.PassiveEnemies(flees: false));
            Assert.Equal("flees from you all the same", CombatWords.PassiveEnemies(flees: true));
        }

        [Fact]
        public void AWeakSpotIsNamedAfterItsPartInPlainWords()
        {
            Assert.Equal("head", CombatWords.PartName("WEAKSPOT_HEAD"));
            Assert.Equal("head", CombatWords.PartName("WeakSpot_head"));
            Assert.Equal("eggs", CombatWords.PartName("WEAKSPOT_EGGS"));
            Assert.Equal("eye", CombatWords.PartName("EYE (1)"));
            Assert.Equal("left horn", CombatWords.PartName("weakspot_left_horn"));
        }

        [Fact]
        public void AWeakSpotWithNoNameLeftIsCalledJustThat()
        {
            Assert.Equal("weak spot", CombatWords.PartName("WeakSpot"));
            Assert.Equal("weak spot", CombatWords.PartName(""));
        }

        [Fact]
        public void ResistancesReadAsOneLineFromMostHarmToLeast()
        {
            var degrees = new[]
            {
                ("Very weak to", new[] { "pierce" }),
                ("Immune to", new[] { "spirit" }),
                ("Unaffected by", new[] { "chop", "pickaxe" }),
            };
            Assert.Equal("very weak to pierce; immune to spirit; unaffected by chop, pickaxe", CombatWords.Resistances(degrees));
        }

        [Fact]
        public void APartResistingNothingReadsAsLikeAnyOtherHit()
        {
            Assert.Equal("takes every hit in full", CombatWords.Resistances(new (string, string[])[0]));
        }

        // An attack's cost is what Attack.HaveStamina and the rest take for it: stamina, eitr,
        // health and a share of health, each only when the attack takes it.
        [Fact]
        public void AnAttackCostsWhatItTakes()
        {
            Assert.Equal("20 stamina, 5 eitr, 10 health, 25% health", CombatWords.Costs(20f, 5f, 10f, 25f));
            Assert.Equal("12.5 stamina", CombatWords.Costs(12.5f, 0f, 0f, 0f));
            Assert.Null(CombatWords.Costs(0f, 0f, 0f, 0f));
        }

        [Fact]
        public void DrawingABowCostsStaminaASecond() => Assert.Equal("1.5 stamina a second", CombatWords.DrawCost(1.5f));

        [Fact]
        public void EachKindOfDamageSaysWhatItCauses() => Assert.Equal("Fire damage causes", CombatWords.DamageCauses("fire"));

        [Fact]
        public void ACreatureSaysHowFastItWalksRunsFliesAndSwims()
        {
            Assert.Equal("walks 2 m/s, runs 6.5 m/s, swims 1,200 m/s", CombatWords.Moves(false, 0f, 0f, 2f, 6.5f, true, 1200f));
            Assert.Equal("walks 2 m/s, runs 6 m/s", CombatWords.Moves(false, 5f, 9f, 2f, 6f, false, 3f));
            Assert.Equal("flies 5–9 m/s", CombatWords.Moves(true, 5f, 9f, 2f, 6f, false, 3f));
        }

        [Fact]
        public void HearingIsToldOnlyWhereItHasALimit()
        {
            // BaseAI's default of 9,999 is no limit.
            Assert.Equal("30 m", CombatWords.Hears(30f));
            Assert.Null(CombatWords.Hears(9999f));
        }

        [Fact]
        public void ACreatureSaysWhenItFlees()
        {
            Assert.Equal("below 25% health, right after being hurt", CombatWords.Flees(0.25f));
            Assert.Null(CombatWords.Flees(0f));
        }

        [Fact]
        public void AWeakSpotIsLabelledByItsPartOfTheBody() => Assert.Equal("Hit on the left horn", CombatWords.WeakSpot("weakspot_left_horn"));

        [Fact]
        public void AWeaponsTwoAttacksAreATableOfWhatEachDoesAndCosts()
        {
            Assert.Equal("Its attacks", CombatWords.WeaponAttacksTitle);
            Assert.Equal(new[] { "", "Damage", "Knockback", "Stagger", "Costs" }, CombatWords.WeaponAttackColumns);
            Assert.Equal(new[] { "Primary", "35 slash", "40", "×1", "8 stamina" }, CombatWords.WeaponAttack(false, "35 slash", 40f, 1f, "8 stamina"));
            Assert.Equal(new[] { "Secondary", "105 slash", "80", "×3", "16 stamina, 4 eitr" }, CombatWords.WeaponAttack(true, "105 slash", 80f, 3f, "16 stamina, 4 eitr"));
            // A dash where an attack tells none.
            Assert.Equal(new[] { "Primary", "–", "–", "×1", "–" }, CombatWords.WeaponAttack(false, null, 0f, 1f, null));
        }

        [Fact]
        public void AWeaponsComboHasALineForEachSwingItsLastOneDoublingDamageAndStaggerAndPushingHarder()
        {
            // Attack.DoMeleeAttack: the last step of a chain deals double and pushes 1.2 times as
            // far; stagger is taken from the damage dealt, so it doubles too.
            var lines = CombatWords.WeaponCombo(3, false, new[] { ("slash", 60f) }, 40f, 1f, 2f, "8 stamina");

            Assert.Equal(3, lines.Count);
            Assert.Equal(new[] { "Primary 1", "60 slash", "40", "×1", "8 stamina" }, lines[0]);
            Assert.Equal(new[] { "Primary 2", "60 slash", "40", "×1", "8 stamina" }, lines[1]);
            Assert.Equal(new[] { "Primary 3", "120 slash", "48", "×2", "8 stamina" }, lines[2]);
            Assert.Equal("Secondary 2", CombatWords.WeaponCombo(2, true, new[] { ("blunt", 10f) }, 0f, 1f, 1f, null)[1][0]);
        }

        [Fact]
        public void AComboWithoutABonusEndsAsItBegan()
        {
            var lines = CombatWords.WeaponCombo(2, false, new[] { ("fire", 50f) }, 20f, 1f, 1f, "20 eitr");

            Assert.Equal(lines[0].Skip(1), lines[1].Skip(1));
        }

        [Fact]
        public void AMeleeSwingsLastHitDoublesWhateverTheItemSaysAndAnAreaAttackTakesTheItemsWord()
        {
            // DoMeleeAttack doubles the last step outright; DoAreaAttack takes m_lastChainDamageMultiplier
            // where it is more than 1; a projectile's chain is its animation alone.
            Assert.Equal(2f, CombatWords.LastSwingMultiplier(melee: true, area: false, itemSays: 1f));
            Assert.Equal(2f, CombatWords.LastSwingMultiplier(melee: true, area: false, itemSays: 3f));
            Assert.Equal(1.5f, CombatWords.LastSwingMultiplier(melee: false, area: true, itemSays: 1.5f));
            Assert.Equal(1f, CombatWords.LastSwingMultiplier(melee: false, area: true, itemSays: 1f));
            Assert.Equal(1f, CombatWords.LastSwingMultiplier(melee: false, area: true, itemSays: 0.5f));
            Assert.Equal(1f, CombatWords.LastSwingMultiplier(melee: false, area: false, itemSays: 2f));
        }

        [Fact]
        public void HowAComboIsKeptGoingIsSaid()
        {
            Assert.Equal("3 swings, the next pressed in time; a pause of 0.2 s starts over; follow-ups keep the first one's facing", CombatWords.Combo(3, swings: true));
            Assert.Equal("Combo", CombatWords.ComboTitle(second: false));
            Assert.Equal("Second combo", CombatWords.ComboTitle(second: true));
            Assert.Equal("2 attacks in turn, the next pressed in time; a pause of 0.2 s starts over; follow-ups keep the first one's facing", CombatWords.Combo(2, swings: false));
        }

        [Fact]
        public void WhatStartsAComboOverIsSaidForEachThingItCanHit()
        {
            // Attack.m_resetChainIfHit, by DestructibleType: Default 1, Tree 2, Character 4.
            Assert.Equal(new[] { ("On a tree", "starts the combo over") }, CombatWords.ComboResets(2));
            Assert.Empty(CombatWords.ComboResets(0));
            Assert.Equal(new[] { ("On a rock or piece", "starts the combo over"), ("On a creature", "starts the combo over") }, CombatWords.ComboResets(5));
        }

        [Fact]
        public void ACreaturesAttackIsAnItemItSwingsNotItsArmorOrShield()
        {
            // A creature may carry armor and a shield too (a draugr's shield, a goblin's armband,
            // a dverger's suit): only an item with an attack animation that is no armor is one of
            // its attacks (Attack.Start pulls the animation's trigger).
            Assert.True(CombatWords.IsCreatureAttack("OneHandedWeapon", "swing_longsword"));
            Assert.True(CombatWords.IsCreatureAttack("Misc", "attack"));
            Assert.True(CombatWords.IsCreatureAttack("Bow", "bow_fire"));
            Assert.True(CombatWords.IsCreatureAttack("Attach_Atgeir", "atgeir_attack"));
            Assert.False(CombatWords.IsCreatureAttack("Shield", ""));
            Assert.False(CombatWords.IsCreatureAttack("Chest", ""));
            Assert.False(CombatWords.IsCreatureAttack("Trophy", ""));
            Assert.False(CombatWords.IsCreatureAttack("Tool", ""));
            Assert.False(CombatWords.IsCreatureAttack("OneHandedWeapon", ""));
            // The game's own test dverger wears headdresses with an animation set: armor still.
            Assert.False(CombatWords.IsCreatureAttack("Helmet", "swing_longsword"));
            Assert.False(CombatWords.IsCreatureAttack("Shoulder", "swing_longsword"));
            Assert.False(CombatWords.IsCreatureAttack("Shield", "swing_longsword"));
        }
    }
}
