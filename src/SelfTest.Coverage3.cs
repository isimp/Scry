using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for the Raids tab's groups, a raid's wave on the stage and the
    /// runestones' texts.
    /// </summary>
    internal static partial class SelfTest
    {
        private static Entry BossFight(Entry entry) => entry?.Source is RandomEvent raid && Knowledge.BossOfEvent(raid.m_name) != null ? entry : null;

        private static GameObject BossOf(Entry fight) => Knowledge.BossOfEvent(((RandomEvent)fight.Source).m_name);

        /// <summary>Bosses' own events are grouped apart from the raids, named for their bosses, in the order the bosses are fought, and linked both ways.</summary>
        private static IEnumerator BossFightsGrouped(Probe p)
        {
            var raids = X.Catalog.Where(e => e.Kind == Kind.Raid && e.Source is RandomEvent).ToList();
            var fights = raids.Where(e => BossFight(e) != null).ToList();
            p.Note(string.Join("; ", raids.GroupBy(e => e.Group).Select(g => $"{g.Key}: {g.Count()}")));
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

        /// <summary>A raid stands on the stage as one rolled wave, every creature of it there, and Roll again rolls a new one.</summary>
        private static IEnumerator RaidWave(Probe p)
        {
            var raid = Pick(Kind.Raid, "army_eikthyr") ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Raid && Stage.IsStaged(e));
            if (raid == null || !Stage.IsStaged(raid)) p.Skip("no raid brings creatures");
            Select(raid);
            yield return Until(() => CopyOf(raid) != null, 15);
            var crowd = CopyOf(raid);
            if (!p.Check(crowd != null, $"{raid.DisplayName} stands on the stage")) yield break;
            p.Note(RaidWords.Wave(RaidCrowd.LastWave, name => name));
            p.Check(RaidCrowd.LastFor == raid && crowd.transform.childCount == RaidCrowd.LastWave.Count, "every creature rolled stands there", $"{crowd.transform.childCount} stand, {RaidCrowd.LastWave.Count} rolled");

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
                    wrong.Add($"roll {i + 1} stands nowhere");
                    continue;
                }
                counts.Add(RaidCrowd.LastWave.Count);
                if (crowd.transform.childCount != RaidCrowd.LastWave.Count) wrong.Add($"roll {i + 1}: {crowd.transform.childCount} of {RaidCrowd.LastWave.Count} stand");
                foreach (var creature in RaidCrowd.LastWave)
                {
                    var spawn = spawns.FirstOrDefault(s => s.m_prefab.name == creature.Prefab);
                    if (spawn != null && (creature.Level < spawn.m_minLevel || creature.Level > Math.Max(spawn.m_minLevel, spawn.m_maxLevel))) wrong.Add($"{creature.Prefab} at level {creature.Level}");
                }
            }
            p.Note($"8 more rolls brought {string.Join(", ", counts)} creatures");
            p.Check(wrong.Count == 0, "Roll again rolls a new wave each time, whole and at the levels it allows", string.Join("; ", wrong.Take(6)));
        }

        /// <summary>Every raid that brings creatures rolls a wave onto the stage, every creature of it standing.</summary>
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
                else if (crowd.transform.childCount != RaidCrowd.LastWave.Count) wrong.Add($"{raid.Name}: {crowd.transform.childCount} of {RaidCrowd.LastWave.Count} stand");
            }
            p.Note($"{raids.Count} raids bring creatures");
            p.Check(wrong.Count == 0, "each stands as a wave", string.Join("; ", wrong.Take(8)));
        }

        /// <summary>A runestone location tells its stone's texts, in words, under Runestone texts.</summary>
        private static IEnumerator RunestoneTexts(Probe p)
        {
            var stone = X.Catalog.Where(e => e.Kind == Kind.Location && e.Name.StartsWith("Runestone", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.Name, StringComparer.Ordinal).FirstOrDefault();
            if (stone == null) p.Skip("there is no runestone location");
            var place = PlaceOf(stone);
            Select(stone);
            yield return Until(() => place.Contents != null && PlaceAssets.State(place) != PlaceLoad.Loading, 30);
            var stones = place.Contents?.Runestones;
            if (!p.Check(stones != null && stones.Count > 0, $"{stone.Name} holds a runestone with texts")) yield break;
            var texts = stones.SelectMany(s => s.Texts).ToList();
            p.Note($"{stones.Count} stones, {texts.Count} texts; the first: \"{RuneWords.Title(texts[0], stones[0].Name)}\"");
            p.Check(texts.All(t => t.Text.Length > 0 && t.Text.IndexOf('$') < 0), "every text is in words, not the game's keys");
            ScryPanel.RunesFolded = false;
            var drawn = ScryPanel.RunesDrawn;
            yield return Until(() => ScryPanel.RunesDrawn > drawn, 3);
            p.Check(ScryPanel.RunesDrawn > drawn, "its page shows them under Runestone texts");
        }

        /// <summary>Once every location is read: the runestones they hold, each with its texts in words.</summary>
        private static IEnumerator EveryRunestone(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var holding = X.Catalog.Where(e => PlaceOf(e)?.Contents?.Runestones.Count > 0).ToList();
            var texts = holding.SelectMany(e => PlaceOf(e).Contents.Runestones.SelectMany(s => s.Texts.Select(t => (e.Name, Text: t)))).ToList();
            p.Note($"{holding.Count} locations and rooms hold runestones, with {texts.Select(t => t.Text.Text).Distinct().Count()} texts between them: " + string.Join(", ", holding.Take(12).Select(e => e.Name)));
            p.Check(holding.Count > 0, "some locations hold runestones");
            var raw = texts.Where(t => t.Text.Text.IndexOf('$') >= 0 || t.Text.Topic.IndexOf('$') >= 0).Select(t => t.Name).Distinct().ToList();
            p.Check(raw.Count == 0, "every text and title is in words", string.Join(", ", raw.Take(8)));
            yield break;
        }
    }
}
