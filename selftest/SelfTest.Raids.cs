using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for raids: bosses' fights grouped apart, a raid's roll on the
    /// stage wave by wave, and what links raids to what they bring.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>Bosses' own events are grouped apart from the raids, named for their bosses, in the order the bosses are fought, and linked both ways.</summary>
        private static IEnumerator BossFightsGrouped(Probe p)
        {
            var raids = X.Catalog.Where(e => e.Kind == Kind.Raid && e.Source is RandomEvent).ToList();
            var fights = raids.Where(e => BossFight(e) != null).ToList();
            p.Note(string.Join("; ", raids.GroupBy(e => e.Group).Select(g => $"{g.Key}: {Numbers.Count(g.Count())}")));
            if (fights.Count == 0) p.Skip("no boss names an event of its own");
            var astray = fights.Where(e => e.Group != "Boss fights").Select(e => $"{e.Name} under {e.Group}").ToList();
            p.Check(astray.Count == 0, "every boss's own event is under Boss fights", string.Join(", ", astray));
            var intruders = raids.Except(fights).Where(e => e.Group == "Boss fights").Select(e => e.Name).ToList();
            p.Check(intruders.Count == 0, "and nothing else is", string.Join(", ", intruders));
            p.Check(fights.All(e => !string.IsNullOrEmpty(e.DisplayName)), "each goes by a name", string.Join(", ", fights.Where(e => string.IsNullOrEmpty(e.DisplayName)).Select(e => e.Name)));

            X.SearchEverything("");
            X.KindFilter = Kind.Raid;
            yield return null;
            var listed = X.Results.ToList();
            X.KindFilter = null;
            var firstFight = listed.FindIndex(e => e.Group == "Boss fights");
            p.Check(firstFight >= 0 && listed.Take(firstFight).All(e => e.Group == "Raids"), "the tab lists the raids first, then the boss fights", string.Join(", ", listed.Select(e => e.Group).Distinct()));
            var inOrder = listed.Where(e => e.Group == "Boss fights").ToList();
            var health = inOrder.Select(e => BossOf(e).GetComponent<Character>()?.m_health ?? 0f).ToList();
            p.Check(health.Zip(health.Skip(1), (a, b) => a <= b).All(rising => rising), "the boss fights in the order the bosses are fought", string.Join(", ", inOrder.Select(e => e.DisplayName)));

            var fight = fights[0];
            var boss = BossOf(fight);
            p.Check(Facts.For(fight).Links.TryGetValue("On", out var toBoss) && toBoss == boss.name, $"{fight.DisplayName}'s page goes to its boss", toBoss);
            var bossEntry = Pick(Kind.Creature, boss.name);
            if (bossEntry != null) p.Check(Facts.For(bossEntry).Links.TryGetValue("Its fight", out var toFight) && toFight == fight.Key, "and the boss's page to its fight", toFight);
        }

        private static Entry BossFight(Entry entry) => entry?.Source is RandomEvent raid && Knowledge.BossOfEvent(raid.m_name) != null ? entry : null;

        private static GameObject BossOf(Entry fight) => Knowledge.BossOfEvent(((RandomEvent)fight.Source).m_name);

        /// <summary>A raid stands on the stage as the first roll of its creatures, every one rolled there, and Roll again rolls them anew.</summary>
        private static IEnumerator RaidWave(Probe p)
        {
            var raid = Pick(Kind.Raid, "army_eikthyr") ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Raid && Stage.IsStaged(e));
            if (raid == null || !Stage.IsStaged(raid)) p.Skip("no raid brings creatures");
            Select(raid);
            yield return Until(() => CopyOf(raid) != null, 15);
            var crowd = CopyOf(raid);
            if (!p.Check(crowd != null, $"{raid.DisplayName} stands on the stage")) yield break;
            p.Note(RaidWords.FirstRoll(RaidCrowd.LastRoll, name => name));
            p.Check(RaidCrowd.LastFor == raid && crowd.transform.childCount == RaidCrowd.LastRoll.Count, "every creature rolled stands there", $"{Numbers.Count(crowd.transform.childCount)} stand, {Numbers.Count(RaidCrowd.LastRoll.Count)} rolled");

            var spawns = ((RandomEvent)raid.Source).m_spawn.Where(s => s?.m_prefab != null).ToList();
            var counts = new List<int>();
            var wrong = new List<string>();
            for (var i = 0; i < 8; i++)
            {
                Previews.Rebuild();
                yield return Until(() => CopyOf(raid) != null && CopyOf(raid) != crowd, 5);
                crowd = CopyOf(raid);
                if (crowd == null)
                {
                    wrong.Add($"roll {Numbers.Count(i + 1)} stands nowhere");
                    continue;
                }
                counts.Add(RaidCrowd.LastRoll.Count);
                if (crowd.transform.childCount != RaidCrowd.LastRoll.Count) wrong.Add($"roll {Numbers.Count(i + 1)}: {Numbers.Count(crowd.transform.childCount)} of {Numbers.Count(RaidCrowd.LastRoll.Count)} stand");
                foreach (var creature in RaidCrowd.LastRoll)
                {
                    var spawn = spawns.FirstOrDefault(s => s.m_prefab.name == creature.Prefab);
                    if (spawn != null && (creature.Level < spawn.m_minLevel || creature.Level > Math.Max(spawn.m_minLevel, spawn.m_maxLevel))) wrong.Add($"{creature.Prefab} at level {Numbers.Count(creature.Level)}");
                }
            }
            p.Note($"8 more rolls brought {string.Join(", ", counts.Select(c => Numbers.Count(c)))} creatures");
            p.Check(wrong.Count == 0, "Roll again rolls them anew each time, whole and at the levels it allows", string.Join("; ", wrong.Take(6)));
        }

        /// <summary>Every raid that brings creatures rolls them onto the stage, every creature rolled standing.</summary>
        private static IEnumerator EveryRaidWave(Probe p)
        {
            var raids = X.Catalog.Where(e => e.Kind == Kind.Raid && Stage.IsStaged(e)).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            if (raids.Count == 0) p.Skip("no raid brings creatures");
            var wrong = new List<string>();
            foreach (var raid in raids)
            {
                Select(raid);
                yield return Until(() => CopyOf(raid) != null, 10);
                var crowd = CopyOf(raid);
                if (crowd == null) wrong.Add($"{raid.Name} stands nowhere");
                else if (crowd.transform.childCount != RaidCrowd.LastRoll.Count) wrong.Add($"{raid.Name}: {Numbers.Count(crowd.transform.childCount)} of {Numbers.Count(RaidCrowd.LastRoll.Count)} stand");
            }
            p.Note($"{Numbers.Count(raids.Count)} raids bring creatures");
            p.Check(wrong.Count == 0, "each stands as rolled", string.Join("; ", wrong.Take(8)));
        }

        private static IEnumerator RaidLinks(Probe p)
        {
            var raid = Pick(Kind.Raid, "army_eikthyr");
            if (raid == null) p.Skip("there is no raid");
            p.Note($"{raid.Name} ({raid.DisplayName})");
            Select(raid);
            yield return null;
            var told = Facts.For(raid);
            foreach (var row in new[] { "Comes for", "On the table", "Lasts" }) p.Check(Tells(told, row), $"it tells {row.ToLowerInvariant()}", Pairs(told));
            p.Check(told.Pairs.Any(pair => pair.Key.StartsWith("Brings ", StringComparison.Ordinal)), "it tells what it brings", Pairs(told));
            p.Check(Stage.IsStaged(raid), "it stands on the stage as the first roll of what it brings");

            var prefab = (raid.Source as RandomEvent)?.m_spawn?.Select(s => s?.m_prefab).FirstOrDefault(x => x != null);
            if (!p.Check(prefab != null, "it brings a creature")) yield break;
            p.Check(X.Jump(prefab.name), $"going to what it brings ({prefab.name})");
            var creature = X.Selected;
            p.Check(creature?.Kind == Kind.Creature, "lands on the creature");

            // The creature's own line about the raid goes back to it, by the raid's key.
            var line = Facts.For(creature).Where.FirstOrDefault(s => s.Prefab == raid.Key);
            if (p.Check(line.Text != null, "the creature tells the raid it comes in, linked to it"))
            {
                p.Check(X.Jump(line.Prefab) && X.Selected == raid, "that line goes to the raid", line.Text);
                p.Check(X.Back() && X.Selected == creature, "and Back returns to the creature");
            }
            p.Check(X.Back() && X.Selected == raid, "and Back again to the raid");
        }
    }
}
