using System;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A biome's page: its weathers with how likely each is and what each does, its music by the
    /// time of day, each playing when clicked, and what lives, grows, stands and comes there,
    /// each a chip going to it.
    /// </summary>
    internal sealed partial class Facts
    {
        private void Biome(BiomeSource biome)
        {
            foreach (var (weather, share) in BiomeWords.Weathers(biome.Weathers)) Add($"{share} of the time", Weather(weather));
            if (biome.Weathers.Count > 0) Hooked(HookedRule.Weather);
            foreach (var (label, music) in BiomeWords.Music(biome.Morning, biome.Evening, biome.Day, biome.Night)) Add(label, Naming.FieldLabel(music), PlayMusic + ":" + music);

            var catalog = Session.Explorer?.Catalog;
            if (catalog == null) return;
            void Here(string title, Func<Entry, bool> which)
            {
                var row = new Row();
                foreach (var entry in catalog.Where(e => which(e) && e.Biomes.Contains(biome.Name)).OrderBy(e => e.DisplayName.Length > 0 ? e.DisplayName : e.Name, StringComparer.OrdinalIgnoreCase))
                {
                    row.Items.Add(EntryChip(entry));
                }
                row.Title = $"{title} ({row.Items.Count})";
                if (row.Items.Count > 0) Rows.Add(row);
            }
            Here("Lives here", e => e.Kind == Kind.Creature);
            Here("Grows here", e => e.Kind == Kind.Resource);
            Here("Places here", e => e.Kind == Kind.Location && !(e.Source is PlaceSource place && place.IsRoom));
            Here("Raids here", e => e.Kind == Kind.Raid);
        }
    }
}
