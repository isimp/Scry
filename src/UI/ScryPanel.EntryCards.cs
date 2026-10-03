using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The selected entry's card where it has no stage, or beside it in the compact view: a biome's, a raid's, a mod's, a sound's or a status effect's, and its kind's badge.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>The kind, in its colour, as a small pill.</summary>
        private static float KindBadge(Entry entry, Vector2 at)
        {
            var color = Skin.KindColor(entry.Kind);
            var label = entry.Kind == Kind.StatusEffect ? "Status effect" : Kinds.Label(entry.Kind).TrimEnd('s');
            var width = Skin.Width(Skin.Glyph, label) + U(20f);
            var badge = new Rect(at.x, at.y, width, U(22f));
            _badgeWidth = width;
            Skin.PillBox(badge, new Color(color.r * 0.28f, color.g * 0.28f, color.b * 0.28f, 0.95f));
            var style = Skin.Glyph;
            var was = style.normal.textColor;
            style.normal.textColor = Color.Lerp(color, Color.white, 0.25f);
            GUI.Label(badge, label, style);
            style.normal.textColor = was;
            return badge.xMax;
        }

        /// <summary>In compact view, what the sound card would say: its clips and length.</summary>
        private static float CompactCard(Entry entry, float width, float y)
        {
            var text = SoundFacts(entry);
            var height = Skin.Height(Skin.DimWrap, text, width);
            GUI.Label(new Rect(0f, y, width, height), text, Skin.DimWrap);
            return y + height + U(12f);
        }

        private static readonly Dictionary<Entry, string> SoundFactCache = new Dictionary<Entry, string>();

        private static string SoundFacts(Entry entry)
        {
            if (!SoundFactCache.TryGetValue(entry, out var facts))
            {
                facts = DescribeSound(entry.Source as GameObject);
                SoundFactCache[entry] = facts;
            }
            return facts;
        }

        /// <summary>The card's height for an entry shown as a card on the stage, or 0 for one the stage shows.</summary>
        private static float CardHeight(Entry entry)
        {
            if (entry == null || Stage.IsStaged(entry)) return 0f;
            if (entry.Kind == Kind.Sound) return U(118f);
            if (entry.Kind == Kind.StatusEffect) return U(150f);
            if (entry.Kind == Kind.Mod && entry.Icon is Sprite) return U(150f);
            if (entry.Kind == Kind.Raid || entry.Kind == Kind.Mod || entry.Kind == Kind.Biome) return U(118f);
            return 0f;
        }

        private static readonly Dictionary<Entry, (string Lasts, string Brings)> RaidCardCache = new Dictionary<Entry, (string, string)>();

        /// <summary>A raid has nothing to show on the stage: how long it lasts and what it brings, the rest under In the game.</summary>
        /// <summary>A biome has nothing to show on the stage: its weathers, music and what is there are under In the game.</summary>
        private static void BiomeCard(Entry entry, Rect rect)
        {
            var weathers = entry.Source is BiomeSource biome ? BiomeWords.Weathers(biome.Weathers).Count : 0;
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(40f), rect.width - U(40f), U(24f)), weathers == 1 ? "One weather" : $"{weathers} weathers", Skin.Center);
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(68f), rect.width - U(40f), U(40f)), "Enter plays its music", Skin.CenterDim);
        }

        private static void RaidCard(Entry entry, Rect rect)
        {
            if (!RaidCardCache.TryGetValue(entry, out var card))
            {
                var raid = entry.Source as RandomEvent;
                var brings = raid?.m_spawn?.Where(s => s?.m_prefab != null).Select(s => ShownNameOf(s.m_prefab)).Distinct().ToList() ?? new List<string>();
                card = (raid != null ? "Lasts " + Naming.Duration(raid.m_duration) : "", brings.Count > 0 ? "Brings " + string.Join(", ", brings) : "Brings nothing");
                RaidCardCache[entry] = card;
            }
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(40f), rect.width - U(40f), U(24f)), card.Lasts, Skin.Center);
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(68f), rect.width - U(40f), U(40f)), card.Brings, Skin.CenterDim);
        }

        /// <summary>A mod's card in the stage's place: its version and id, and what it adds.</summary>
        private static void ModCard(Entry entry, Rect rect)
        {
            if (!(entry.Source is ModSource mod)) return;
            var summary = Session.Explorer != null ? Report(Session.Explorer).FirstOrDefault(m => m.Mod == mod.Name) : null;
            var adds = summary != null ? ModReportWords.Counts(summary) : "adds nothing of its own";
            // Its package's icon above, where it has one.
            var top = rect.y + U(40f);
            if (entry.Icon is Sprite icon && icon != null)
            {
                var size = U(56f);
                DrawSprite(icon, new Rect(rect.center.x - size / 2f, rect.y + U(34f), size, size));
                top = rect.y + U(96f);
            }
            GUI.Label(new Rect(rect.x + U(20f), top, rect.width - U(40f), U(24f)), ModWords.Card(mod), Skin.Center);
            GUI.Label(new Rect(rect.x + U(20f), top + U(28f), rect.width - U(40f), U(40f)), char.ToUpperInvariant(adds[0]) + adds.Substring(1), Skin.CenterDim);
        }

        /// <summary>A creature's name as the game shows it, else its prefab's.</summary>
        private static string ShownNameOf(GameObject prefab)
        {
            var shown = CatalogBuilder.Localize(prefab.GetComponent<Character>()?.m_name);
            return shown.Length > 0 ? shown : prefab.name;
        }

        /// <summary>What the sound is (its clips and length), and where it has got to, which can be moved.</summary>
        private static void SoundCard(Entry entry, Rect rect)
        {
            GUI.Label(new Rect(rect.x + U(20f), rect.y + U(40f), rect.width - U(40f), U(24f)), SoundFacts(entry), Skin.Center);
            var line = new Rect(rect.x + U(20f), rect.y + U(74f), rect.width - U(40f), U(30f));
            GUI.BeginGroup(line);
            Timeline(line.width, 0f);
            GUI.EndGroup();
        }

        private static string DescribeSound(GameObject prefab)
        {
            if (prefab == null) return "";

            var clips = new List<AudioClip>();
            foreach (var sfx in prefab.GetComponentsInChildren<ZSFX>(true))
            {
                if (sfx.m_audioClips != null) clips.AddRange(sfx.m_audioClips.Where(c => c != null));
            }
            foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip != null && !clips.Contains(source.clip)) clips.Add(source.clip);
            }

            var loops = prefab.GetComponentsInChildren<AudioSource>(true).Any(s => s.loop);
            if (clips.Count == 0) return loops ? "Loops" : "No clips found";

            var longest = clips.Max(c => c.length);
            var what = clips.Count == 1 ? "1 clip" : $"{clips.Count} clips, one picked at random";
            return $"{what}, {longest.ToString("0.0", CultureInfo.InvariantCulture)} s{(loops ? ", loops" : "")}";
        }

        private static readonly Dictionary<Entry, List<KeyValuePair<string, EffectList>>> StatusListCache =
            new Dictionary<Entry, List<KeyValuePair<string, EffectList>>>();

        private static List<KeyValuePair<string, EffectList>> StatusLists(Entry entry)
        {
            if (!StatusListCache.TryGetValue(entry, out var lists))
            {
                lists = Previews.StatusLists(entry.Source as StatusEffect);
                StatusListCache[entry] = lists;
            }
            return lists;
        }

        private static string StatusFacts(StatusEffect effect)
        {
            var parts = new List<string>();
            parts.Add(effect.m_ttl > 0f ? "Lasts " + Naming.Duration(effect.m_ttl) : "No time limit of its own");
            return string.Join("    ", parts);
        }

        private static void StatusCard(Entry entry, Rect rect)
        {
            // Its icon and how long it lasts; what it does is the first thing under In the game.
            var effect = entry.Source as StatusEffect;
            var iconSize = U(64f);
            var icon = new Rect(rect.x + (rect.width - iconSize) / 2f, rect.y + U(38f), iconSize, iconSize);
            if (effect != null && effect.m_icon != null) DrawIcon(entry, icon);
            else DrawIcon(new Entry { Kind = Kind.StatusEffect }, icon);

            if (effect == null) return;
            GUI.Label(new Rect(rect.x + U(20f), icon.yMax + U(10f), rect.width - U(40f), U(22f)), StatusFacts(effect), Skin.CenterDim);
        }
    }
}
