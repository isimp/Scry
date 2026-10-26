using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>Where the polish sweep writes what it gathered for the next look over the panel's words.</summary>
        internal static string PolishReportFile => Path.Combine(PictureFolder, "polish-sweep.txt");

        /// <summary>
        /// What the panel's last round of polish changed, checked where the game shows it: room
        /// names without the Frost Caves' "new" and with their words apart, a room card's loot
        /// with copies numbered apart counted as one, a creature's attack known only by a code
        /// name going by what it fires, a staff's slower rise on its chip under what trains its
        /// skill, and under the strict rules each giver of an item a chip with its count. Then a
        /// real jump measured against the height the skill page would tell, the skill and
        /// stamina it costs put back. Last, a report for the next look over the words: every
        /// label by kind, words that read as code, names told apart only as "N of M", long room
        /// names and the biomes as the tiers name them.
        /// </summary>
        private static IEnumerator PolishSweep(Probe p)
        {
            var report = new StringBuilder();
            report.AppendLine("Scry polish sweep, " + About.Version);

            // Room names: no leading "new" where no room of that name without it is there, no words run together.
            var rooms = X.Catalog.Where(e => e.Kind == Kind.Location && e.Source is PlaceSource place && place.IsRoom).ToList();
            var names = new HashSet<string>(rooms.Select(r => r.Name), StringComparer.Ordinal);
            var newKept = rooms.Where(r => r.ShownName.StartsWith("New ", StringComparison.Ordinal)).ToList();
            var newWrong = newKept.Where(r => { var tag = r.Name.IndexOf('_'); return tag < 0 || !names.Contains(r.Name.Substring(0, tag + 1) + r.Name.Substring(tag + 5)); }).ToList();
            p.Check(newWrong.Count == 0, "a room reads without a leading \"new\" where no room of that name without it is there", string.Join(", ", newWrong.Take(8).Select(r => r.Name)));
            var runTogether = rooms.Where(r => r.ShownName.Split(' ').Any(w => w.Length > 11)).Select(r => r.ShownName).Distinct().ToList();
            p.Note($"{Numbers.Count(runTogether.Count)} room names hold a word over 11 letters: " + string.Join(", ", runTogether.Take(12)));
            report.AppendLine().AppendLine("Room names with a word over 11 letters:");
            foreach (var name in runTogether) report.AppendLine("  " + name);

            // A troll's rock throw goes by what it fires.
            var troll = Pick(Kind.Creature, "Troll");
            if (troll != null)
            {
                var attacks = Facts.For(troll).Rows.FirstOrDefault(r => r.Title == CombatWords.AttacksTitle);
                var named = attacks?.Lines.Select(l => l.Cells[0]).ToList() ?? new List<string>();
                p.Note("Troll's attacks: " + string.Join(", ", named));
                p.Check(!named.Contains("Fireball attack"), "a troll's rock throw is not called Fireball attack", string.Join(", ", named));
            }

            // A staff training Elemental Magic slower than the usual hit says so on its chip.
            var elemental = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Skill && e.Name == "ElementalMagic");
            if (elemental != null)
            {
                var trained = Facts.For(elemental).Rows.FirstOrDefault(r => r.Title == SkillPageWords.TrainedWithTitle);
                var slower = trained?.Items.Where(i => (i.Amount ?? "").EndsWith(" a hit", StringComparison.Ordinal)).ToList() ?? new List<Facts.Ingredient>();
                p.Note("Elemental Magic trained with: " + string.Join(", ", trained?.Items.Select(i => i.Name + (string.IsNullOrEmpty(i.Amount) ? "" : " " + i.Amount)) ?? Enumerable.Empty<string>()));
                p.Check(slower.Count > 0, "a staff whose hits train Elemental Magic slower says so on its chip", Numbers.Count(slower.Count));
            }

            // Every skill's weapons whose rise differs from the usual hit, for the report.
            report.AppendLine().AppendLine("Weapons rising a skill another share than its usual hit:");
            foreach (var skill in X.Catalog.Where(e => e.Kind == Kind.Skill))
            {
                var row = Facts.For(skill).Rows.FirstOrDefault(r => r.Title == SkillPageWords.TrainedWithTitle);
                var told = row?.Items.Where(i => !string.IsNullOrEmpty(i.Amount)).Select(i => i.Name + " " + i.Amount).ToList();
                if (told != null && told.Count > 0) report.AppendLine($"  {skill.ShownName}: {string.Join(", ", told)}");
            }
            yield return null;

            // A real jump against the height the page would tell: the jump's speed up is the
            // character's jump force times its skill's share (Character.Jump), and it rises until
            // gravity has taken that speed.
            yield return MeasureJump(p, report);

            // Labels by kind, words that read as code, and names told apart only by a count, over a spread of each kind.
            var labels = new Dictionary<Kind, Dictionary<string, int>>();
            var coded = new List<string>();
            var codeWord = new Regex(@"[a-z][A-Z]|_|\$[a-z]|\(\d+\)$");
            var sample = new List<Entry>();
            foreach (var kind in X.Catalog.Select(e => e.Kind).Distinct().ToList())
                sample.AddRange(Spread(X.Catalog.Where(e => e.Kind == kind).ToList(), 60));
            yield return Budgeted(sample, entry =>
            {
                var facts = Facts.For(entry);
                if (!labels.TryGetValue(entry.Kind, out var counted)) labels[entry.Kind] = counted = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var label in facts.Pairs.Select(pair => pair.Key).Concat(facts.Rows.Select(r => r.Title)).Distinct())
                {
                    counted.TryGetValue(label ?? "", out var n);
                    counted[label ?? ""] = n + 1;
                }
                foreach (var pair in facts.Pairs)
                    if (codeWord.IsMatch(pair.Value ?? "") && coded.Count < 200) coded.Add($"{entry.Kind} {entry.Name}: {pair.Key}: {pair.Value}");
                if (codeWord.IsMatch(entry.ShownName) && coded.Count < 200) coded.Add($"{entry.Kind} {entry.Name}: its name {entry.ShownName}");
            });
            report.AppendLine().AppendLine("Labels by kind, with how many of the entries looked at tell each:");
            foreach (var pair in labels.OrderBy(l => l.Key.ToString(), StringComparer.Ordinal))
                report.AppendLine($"  {pair.Key}: " + string.Join("; ", pair.Value.OrderByDescending(l => l.Value).Select(l => $"{l.Key} ({Numbers.Count(l.Value)})")));
            report.AppendLine().AppendLine("Values and names that read as code:");
            foreach (var line in coded) report.AppendLine("  " + line);
            var counts = X.Catalog.Where(e => e.Alike != null && Regex.IsMatch(e.Alike, @"^\d+ of \d+$")).ToList();
            report.AppendLine().AppendLine($"Names told apart only as \"N of M\" ({Numbers.Count(counts.Count)}):");
            foreach (var entry in counts.Take(80)) report.AppendLine($"  {entry.Kind} {entry.ShownName} {entry.Alike}: {entry.Name}");
            p.Note($"{Numbers.Count(counts.Count)} entries are told apart only as \"N of M\"; {Numbers.Count(coded.Count)} values or names read as code");

            report.AppendLine().AppendLine("Biomes as the tiers name them: " + string.Join(", ", Enumerable.Range(1, Tiers.Last).Select(CompendiumWords.FloorName)));

            var failure = Steps.Run(() =>
            {
                Directory.CreateDirectory(PictureFolder);
                File.WriteAllText(PolishReportFile, report.ToString());
            }, null);
            p.Note(failure == null ? $"the polish report is in {Path.GetFileName(PolishReportFile)}" : $"no polish report: {(failure.InnerException ?? failure).Message}");
        }

        /// <summary>
        /// Has the character jump where it stands and measures how high it rises, against the
        /// height its jump force and the world's gravity tell; the Jump skill's level and the way
        /// to its next, and the stamina the jump cost, are put back. Skipped where the character
        /// is not on the ground.
        /// </summary>
        private static IEnumerator MeasureJump(Probe p, StringBuilder report)
        {
            var player = Player.m_localPlayer;
            if (player == null || !player.IsOnGround() || player.InAttack() || player.IsEncumbered())
            {
                p.Note("no jump measured: the character is not standing free on the ground");
                yield break;
            }
            // The game's jump grows or shrinks by what the character has on, is weaker tired, and stops at a roof (Character.Jump).
            var lifted = Vector3.up;
            player.GetSEMan().ApplyStatusEffectJumpMods(ref lifted);
            var room = Physics.Raycast(player.transform.position + Vector3.up * 1.8f, Vector3.up, out var roof, 4f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")) ? roof.distance : 4f;
            var unchanged = Mathf.Abs(lifted.y - 1f) < 0.001f && player.HaveStamina(player.m_jumpStaminaUsage) && room >= 3f;
            var skills = player.GetSkills();
            var skill = skills != null ? skills.GetSkillList().FirstOrDefault(s => s.m_info.m_skill == Skills.SkillType.Jump) : null;
            var level = skill != null ? skill.m_level : 0f;
            var progress = skill != null ? skill.m_accumulator : 0f;
            var stamina = player.GetStamina();
            var share = SkillMath.JumpShare(YourSkills.LevelOf(Skills.SkillType.Jump));
            var gravity = Mathf.Abs(Physics.gravity.y);
            // The height the skill page tells, by the same rule.
            var told = SkillMath.JumpHeight(player.m_jumpForce, gravity, YourSkills.LevelOf(Skills.SkillType.Jump));

            var start = player.transform.position.y;
            var highest = start;
            player.Jump();
            var until = Time.unscaledTime + 2f;
            while (Time.unscaledTime < until)
            {
                highest = Mathf.Max(highest, player.transform.position.y);
                yield return null;
            }
            if (skill != null)
            {
                skill.m_level = level;
                skill.m_accumulator = progress;
            }
            if (player.GetStamina() < stamina) player.AddStamina(stamina - player.GetStamina());

            var rose = highest - start;
            var line = $"jump at Jump {Numbers.Count((long)Math.Floor(YourSkills.LevelOf(Skills.SkillType.Jump)))}: rose {Numbers.Amount(rose, 2)} m, the force and gravity ({Numbers.Amount(player.m_jumpForce, 1)} × {Numbers.Amount(share, 2)}, {Numbers.Amount(gravity, 2)} m/s²) tell {Numbers.Amount(told, 2)} m";
            p.Note(line);
            report.AppendLine().AppendLine(line);
            if (!unchanged) p.Note($"the jump is not checked: {(Mathf.Abs(lifted.y - 1f) >= 0.001f ? "something the character has on changes it" : room < 3f ? $"a roof {Numbers.Amount(room + 1.8f, 1)} m above stops it" : "the character was too tired to jump full")}");
            else if (rose > 0.05f && told > 0f) p.Check(Mathf.Abs(rose - told) <= told * 0.1f, "a real jump rises within a tenth of the height its force and gravity tell", line);
            else p.Note("the jump did not leave the ground, so nothing was compared");
        }
    }
}
