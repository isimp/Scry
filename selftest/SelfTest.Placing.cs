using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for items a build tool places as a piece of themselves, told on the item's page.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Royal Jelly and Queen's Jam, foods the game's Serving Tray places:
        /// each page tells the tray and what placing one uses up, what a placed one does and what
        /// it stands as, under a topic of its own, and none of a piece's build cost or support;
        /// Royal Jelly's topic pictured.
        /// </summary>
        private static IEnumerator PlacedItemsTold(Probe p)
        {
            var jelly = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == "RoyalJelly");
            var jam = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == "QueensJam");
            var tools = Knowledge.Tools.ToolsOf("RoyalJelly");
            var tray = tools.Count > 0 ? tools[0] : null;
            if (jelly == null || jam == null || tray == null)
            {
                p.Skip("there is no Royal Jelly, Queen's Jam or tool placing them in this game's catalog");
                yield break;
            }
            string Said(Facts f, string label) => f.Pairs.FirstOrDefault(x => x.Key == label).Value;
            foreach (var item in new[] { jelly, jam })
            {
                var facts = Facts.For(item);
                var shared = GamePrefabs.Item(item.Name).OrNull()?.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
                // A food is eaten where it stands, as the game holds it (ItemData.ItemType.Consumable); Royal Jelly is one.
                var consumable = shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable;
                var wear = GamePrefabs.Item(item.Name).OrNull()?.GetComponent<WearNTear>();
                p.Check(Said(facts, "Placed with") == PlacingWords.With(tray.ToolName, 1) && facts.Links.TryGetValue("Placed with", out var link) && link == tray.Tool,
                    $"{item.Name}'s page says the {tray.ToolName} places it, using up one", $"{Said(facts, "Placed with")}, going to {(facts.Links.TryGetValue("Placed with", out var to) ? to : "nothing")}");
                p.Check(Said(facts, "Once placed") == PlacingWords.Once(consumable, shared?.m_isDrink ?? false),
                    consumable ? $"a placed {item.Name} is eaten from where it stands" : $"a placed {item.Name} is picked up again", Said(facts, "Once placed"));
                p.Check(wear != null && Said(facts, "Stands as") == PlacingWords.StandsAs(wear.m_materialType.ToString(), wear.m_health), $"and stands as a piece of its material", Said(facts, "Stands as"));
                p.Check(Said(facts, "Support") == null && facts.Rows.All(r => r.Title != BuildWords.Cost("")),
                    $"{item.Name}'s page tells no build cost and no support", string.Join(", ", facts.Pairs.Select(x => x.Key)));
            }

            Select(jelly);
            yield return Until(() => X.Selected == jelly, 3);
            // Its topics are drawn before the page can go to one.
            yield return Settled();
            yield return PictureOf(p, "placing", "Royal Jelly's page, placing it with the Serving Tray");
        }
    }
}
