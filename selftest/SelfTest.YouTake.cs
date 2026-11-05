using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for what a creature's hit does to the character, told on its page as the game takes it.</summary>
    internal static partial class SelfTest
    {
        /// <summary>Creature attacks whose hits are checked against the game's own: a Seeker's claw, its landing of pierce and frost together, a troll's log and a greydwarf's punch.</summary>
        private static readonly string[] HitsChecked = { "seeker_claw_left", "seeker_land", "troll_log_swing_v", "Greydwarf_attack" };

        /// <summary>
        /// What a creature's hit does to the character, as Scry reckons it (<see cref="HitTaken"/>),
        /// is what the game itself makes of the same hit: each attack's damage given the
        /// character's own modifiers (<c>HitData.ApplyResistance</c>) and armour
        /// (<c>HitData.ApplyArmor</c>), its whole hit, as one lands before the world scales it.
        /// What guards the character is read as the game has it. A creature's page then tells each
        /// attack's toll in its attacks table, and says under it what that is reckoned at.
        /// </summary>
        private static IEnumerator WhatYouTakeTold(Probe p)
        {
            var player = Player.m_localPlayer;
            if (player == null) p.Skip("there is no character");
            yield return null;
            p.Check(YourGuard.Known && Mathf.Approximately(YourGuard.Armour, player.GetBodyArmor()), "your armour is read as the game has it",
                $"{Numbers.Fixed(YourGuard.Armour, 1)} read, the game's {Numbers.Fixed(player.GetBodyArmor(), 1)}");
            var mods = player.GetDamageModifiers();
            foreach (var name in HitsChecked)
            {
                var item = GamePrefabs.Item(name);
                var damages = item.OrNull()?.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_damages;
                if (damages == null)
                {
                    p.Note($"{name} is not in this game: its hit untried");
                    continue;
                }
                var hit = new HitData { m_damage = damages.Value.Clone() };
                hit.ApplyResistance(mods, out _);
                hit.ApplyArmor(player.GetBodyArmor());
                var game = hit.GetTotalDamage();
                var ours = HitTaken.Taken(Facts.DamageFigures(damages.Value), 1f, YourGuard.Factor, YourGuard.Armour);
                p.Check(Mathf.Abs(game - ours) < 0.01f, $"{name}'s hit does to you what the game makes of it", $"the game {Numbers.Fixed(game, 2)}, Scry {Numbers.Fixed(ours, 2)}");
            }
            var seeker = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e.Name == "Seeker");
            if (seeker == null)
            {
                p.Note("there is no Seeker: its page's column untried");
                yield break;
            }
            var table = Facts.For(seeker).Rows.FirstOrDefault(r => r.Title == CombatWords.AttacksTitle);
            var column = table != null ? System.Array.IndexOf(table.Columns, CombatWords.YouTakeColumn) : -1;
            p.Check(column == table?.Columns.Length - 1 && table.Lines.All(l => l.Cells.Length == table.Columns.Length && l.Cells[column].Length > 0) && table.Foot == CombatWords.YouTakeAt(YourGuard.Armour, YourGuard.Scale),
                "a Seeker's attacks table tells what each attack does to you, last, and says what at under it",
                table == null ? "no attacks table" : $"{string.Join(", ", table.Columns)}; {string.Join("; ", table.Lines.Select(l => l.Cells.LastOrDefault()))}; {table.Foot}");
        }
    }
}
