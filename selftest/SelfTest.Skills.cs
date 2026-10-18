using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for skills: every skill's page, what other pages tell at the
    /// character's own levels, and that reading the character's skills leaves its save alone.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Every skill of the character's list has a page in the Skills tab telling what it
        /// changes at no skill, the character's level and the top, what raises it and what a
        /// death takes, the You box opening with the level; and reading them all gives the
        /// character no skill it never trained (the game's own GetSkillLevel would).
        /// </summary>
        private static IEnumerator SkillPages(Probe p)
        {
            var player = Player.m_localPlayer;
            if (player == null) p.Skip("no character in the world");
            var skills = player.GetSkills();
            var trained = skills.GetSkillList().Count;
            var listed = skills.m_skills.Where(d => d != null && d.m_skill != Skills.SkillType.None && d.m_skill != Skills.SkillType.All).Select(d => d.m_skill.ToString()).Distinct().ToList();
            var pages = X.Catalog.Where(e => e.Kind == Kind.Skill).ToList();
            var missing = listed.Where(name => pages.All(e => e.Name != name)).ToList();
            p.Note(string.Join("; ", pages.GroupBy(e => e.Group).Select(g => $"{g.Key}: {string.Join(", ", g.Select(e => e.ShownName))}")));
            p.Check(missing.Count == 0, "every skill of the character's list has a page", string.Join(", ", missing));

            var swords = Pick(Kind.Skill, "Swords");
            if (!p.Check(swords != null, "Swords has a page")) yield break;
            Select(swords);
            yield return null;
            var told = Facts.For(swords);
            var table = told.Rows.FirstOrDefault(r => r.Title == SkillPageWords.ChangesTitle);
            var level = YourSkills.LevelOf(Skills.SkillType.Swords);
            p.Check(table != null && table.Columns.Length == 4 && table.Lines.Count > 0, "Swords' page tells what it changes at no skill, yours and the top",
                table == null ? Pairs(told) : string.Join(" | ", table.Lines.Select(l => string.Join(", ", l.Cells))));
            if (table != null) p.Check(table.Columns[2] == SkillPageWords.ChangesColumns(level)[2], "its middle column is the character's level", table.Columns[2]);
            p.Check(Tells(told, SkillPageWords.RisesLabel) && Tells(told, SkillPageWords.OnDeathLabel), "it tells what raises it and what a death takes", Pairs(told));
            var you = YourSkills.Lines((SkillSource)swords.Source);
            p.Check(you.Count > 0 && you[0].Label == SkillPageWords.LevelLabel, "the You box opens with the character's level", string.Join("; ", you.Select(l => l.Label + ": " + l.Value)));
            yield return PictureOf(p, "facts", "skill Swords");

            var run = Pick(Kind.Skill, "Run");
            if (run != null)
            {
                Select(run);
                yield return null;
                var runTable = Facts.For(run).Rows.FirstOrDefault(r => r.Title == SkillPageWords.ChangesTitle);
                p.Check(runTable != null && runTable.Lines.Any(l => l.Cells[0] == "Running speed") && runTable.Lines.Any(l => l.Cells[0] == "Stamina running"), "Run's page tells running speed and stamina from the character's own figures",
                    runTable == null ? "" : string.Join(" | ", runTable.Lines.Select(l => string.Join(", ", l.Cells))));
                yield return PictureOf(p, "facts", "skill Run");
            }
            var pickaxes = Pick(Kind.Skill, "Pickaxes");
            if (pickaxes != null) p.Check(Tells(Facts.For(pickaxes), "Digging the ground"), "Pickaxes' page says digging the ground is the same at every level", Pairs(Facts.For(pickaxes)));

            foreach (var page in pages)
            {
                Select(page);
                yield return null;
                var each = Facts.For(page);
                var changes = each.Rows.FirstOrDefault(r => r.Title == SkillPageWords.ChangesTitle);
                p.Note($"{page.ShownName}: {(changes != null ? string.Join(" | ", changes.Lines.Select(l => string.Join(", ", l.Cells))) : "no table")}");
            }
            var after = skills.GetSkillList().Count;
            p.Check(after == trained, "reading every skill gives the character no skill it never trained", $"{Numbers.Count(trained)} before, {Numbers.Count(after)} after");
        }

        /// <summary>
        /// Pages tell, faint under the game's figures, what they come to at the character's skill:
        /// an iron sword's damage as the game's own range for its tooltip (Skills.GetRandomSkillRange)
        /// and each line of its attacks, an axe's hits on trees, a creature's sight while sneaking,
        /// a workbench's making, a crop's bonus, the hammer's cost; drawn, and pictured.
        /// </summary>
        private static IEnumerator SkillLines(Probe p)
        {
            var player = Player.m_localPlayer;
            if (player == null) p.Skip("no character in the world");
            var sword = Pick(Kind.Item, "SwordIron");
            if (sword == null) p.Skip("there is no iron sword");
            Select(sword);
            yield return null;
            var told = Facts.For(sword);
            var shared = ((GameObject)sword.Source).GetComponent<ItemDrop>().m_itemData.m_shared;
            var level = YourSkills.LevelOf(Skills.SkillType.Swords);
            p.Note($"Swords {Numbers.Count((long)level)}");
            // The game's own range only for a skill trained: asking it of one never trained gives the character that skill.
            if (player.GetSkills().GetSkillList().Any(s => s.m_info != null && s.m_info.m_skill == Skills.SkillType.Swords))
            {
                player.GetSkills().GetRandomSkillRange(out var least, out var most, Skills.SkillType.Swords);
                var expected = SkillWords.Yours(SkillWords.At(Facts.SkillName(Skills.SkillType.Swords), level), SkillWords.DamageRange(Facts.DamageFigures(shared.m_damages), least, most));
                p.Check(told.Yours.TryGetValue("Damage", out var damage) && damage == expected, "its damage at the character's Swords is the game's own range", $"{damage} / {expected}");
            }
            else p.Note("Swords never trained: the game's own range is not asked");
            var attacks = told.Rows.FirstOrDefault(r => r.Title == CombatWords.WeaponAttacksTitle);
            p.Check(attacks != null && attacks.Lines.Count > 0 && Enumerable.Range(0, attacks.Lines.Count).All(attacks.Yours.ContainsKey), "each line of its attacks has a faint line at the character's skill",
                attacks == null ? "" : string.Join(" | ", attacks.Yours.Values.SelectMany(v => v).Select(c => string.Join(", ", c))));
            var drawn = ScryPanel.Drawn(PanelPart.YourSkillLine);
            yield return PictureOf(p, "fight", "skill lines SwordIron");
            p.Check(ScryPanel.Drawn(PanelPart.YourSkillLine) > drawn, "and they are drawn", Numbers.Count(ScryPanel.Drawn(PanelPart.YourSkillLine) - drawn));

            var axe = Pick(Kind.Item, "AxeBronze", "AxeIron", "AxeStone");
            if (axe != null)
            {
                var axeAttacks = Facts.For(axe).Rows.FirstOrDefault(r => r.Title == CombatWords.WeaponAttacksTitle);
                p.Check(axeAttacks != null && axeAttacks.Yours.Values.Any(lines => lines.Any(cells => cells[0].StartsWith(SkillWords.OnTrees(""), System.StringComparison.Ordinal))),
                    $"{axe.ShownName} tells its hits on trees by the skill trees take", axeAttacks == null ? "" : string.Join(" | ", axeAttacks.Yours.Values.SelectMany(v => v).Select(c => c[0])));
            }
            var creature = Pick(Kind.Creature, "Troll", "Greydwarf");
            if (creature != null)
            {
                Select(creature);
                yield return null;
                var seen = Facts.For(creature);
                p.Check(seen.Yours.ContainsKey("Sees"), $"{creature.ShownName} tells how near it sees you sneaking", Pairs(seen));
                yield return PictureOf(p, "senses", $"skill lines {creature.Name} senses");
            }
            var bench = Pick(Kind.Piece, "piece_workbench");
            if (bench != null) p.Check(Facts.For(bench).Yours.ContainsKey("Making takes"), "the workbench tells how long making takes at your Crafting", Pairs(Facts.For(bench)));
            var crop = Pick(Kind.Resource, "Pickable_Carrot", "RaspberryBush", "BlueberryBush");
            if (crop != null) p.Check(Facts.For(crop).Yours.ContainsKey("Bonus when picked"), $"{crop.ShownName} tells its bonus when picked at your Farming", Pairs(Facts.For(crop)));
            var hammer = Pick(Kind.Item, "Hammer");
            if (hammer != null) p.Check(Facts.For(hammer).Yours.ContainsKey("Each use costs"), "the hammer tells what each use costs at your Crafting", Pairs(Facts.For(hammer)));
        }
    }
}
