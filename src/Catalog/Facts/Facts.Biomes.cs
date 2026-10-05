using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A biome's page: its weathers with how likely each is and what each does, its music by the
    /// time of day, each playing when clicked, and what lives, grows, stands and comes there,
    /// each a chip going to it: creatures toughest first, what grows hardest to gather first,
    /// places rarest first, raids in the order they come, the rest by name.
    /// </summary>
    internal sealed partial class Facts
    {
        private void Biome(BiomeSource biome)
        {
            var weathers = new Row { Title = BiomeWords.WeathersTitle, Columns = BiomeWords.WeatherColumns };
            foreach (var (weather, share) in BiomeWords.Weathers(biome.Weathers))
            {
                var does = WeatherFactsOf(weather) is WeatherFacts known ? WeatherWords.Effects(known) : "";
                weathers.Lines.Add((new[] { Naming.FieldLabel(weather), share, does }, null));
            }
            if (weathers.Lines.Count > 0) Rows.Add(weathers);
            if (biome.Weathers.Count > 0) Hooked(HookedRule.Weather);

            // Each music plays where it is named, as a fact naming music does.
            var tunes = new Row { Title = BiomeWords.MusicTitle, Columns = BiomeWords.MusicColumns };
            foreach (var (when, music) in BiomeWords.Music(biome.Morning, biome.Evening, biome.Day, biome.Night)) tunes.Lines.Add((new[] { MusicWords.Name(music), when }, EntryKeys.PlayMusicNamed(music)));
            if (tunes.Lines.Count > 0) Rows.Add(tunes);

            var catalog = WorldCatalog.Current?.All;
            if (catalog == null) return;
            // Each row by name, then in its own order where it has one.
            void Here(string title, Func<Entry, bool> which, bool home = true, Func<List<Entry>, List<Entry>> order = null)
            {
                var found = catalog.Where(e => which(e) && (home ? e.Biomes.Contains(biome.Name) : Knowledge.EventBiomes(e.Name).Contains(biome.Name) && !e.Biomes.Contains(biome.Name)))
                    .OrderBy(e => e.ShownName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var row = new Row();
                foreach (var entry in order != null ? order(found) : found) row.Items.Add(EntryChip(entry));
                row.Title = Naming.Counted(title, row.Items.Count);
                if (row.Items.Count > 0) Rows.Add(row);
            }
            bool Fish(Entry e) => e.Kind == Kind.Item && e.Source is UnityEngine.GameObject prefab && prefab.GetComponent<global::Fish>() != null;
            List<Entry> Toughest(List<Entry> found) => ContentOrder.ToughestFirst(found, e => FoeOf(e.Source as UnityEngine.GameObject));
            Here("Lives here", e => e.Kind == Kind.Creature, order: Toughest);
            Here("Fish here", Fish);
            // What grows wild counts whatever kind a mod made it, as a piece one can plant too.
            Here("Grows here", e => e.Kind == Kind.Resource || (e.Kind == Kind.Piece && Knowledge.IsPlacedByWorld(e.Name)),
                order: found => ContentOrder.HardestFirst(found, e => ToGather(e.Source as UnityEngine.GameObject)));
            // Places the fewest the world places first, by every set of rules it is placed by.
            Here("Places here", e => e.Kind == Kind.Location && !(e.Source is PlaceSource place && place.IsRoom),
                order: found => ContentOrder.FewestFirst(found, e => e.Source is PlaceSource place ? place.Rules.Sum(r => r.m_quantity) : 0));
            // Raids in the order they come, as the Raids tab lists them.
            Here("Raids here", e => e.Kind == Kind.Raid, order: found => found.OrderBy(e => e.GroupOrder).ThenBy(e => e.GroupRank).ToList());
            Here("Also placed here", e => (e.Kind == Kind.Other || e.Kind == Kind.Effect || e.Kind == Kind.Projectile || (e.Kind == Kind.Item && !Fish(e))) && Knowledge.IsPlacedByWorld(e.Name));
            Here("Here only in weather a world event brings", e => e.Kind != Kind.Biome, home: false, order: Toughest);
        }
    }
}
