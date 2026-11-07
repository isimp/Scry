using System.Collections;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for Scry's marks against the game's own pins: what stands over them, and the pins near a pointed mark fading.</summary>
    internal static partial class SelfTest
    {
        /// <summary>The settings and the pin the case set, put back after it.</summary>
        private static (MarksOverPins Marks, bool Pips, PinsNearPointer Near)? _overPinsBefore;
        private static Minimap.PinData _testPin;

        /// <summary>
        /// With the stack standing on the large map, a pin of the game's laid between its two marks,
        /// as a base's pins lie over Scry's: with Marks over pins on When pointed at, Pips over
        /// pins on and Pins near the pointer at a third, the pips stand over the pin at rest; the
        /// stack pointed at spreads over it and the pin fades, and comes back as the pointer
        /// leaves; the lone dot pointed at stands over the pins too. Pictured at rest and pointed at.
        /// </summary>
        private static IEnumerator MarksOverPinsTold(Probe p, Minimap map, Vector3 between)
        {
            _overPinsBefore = (Settings.MarksOverPins, Settings.PipsOverPins, Settings.PinsNearPointer);
            Settings.MarksOverPins = MarksOverPins.Pointed;
            Settings.PipsOverPins = true;
            Settings.PinsNearPointer = PinsNearPointer.Third;
            _testPin = AddTestPin(map, between);
            if (_testPin == null) p.Note("no pin of the game's could be laid; the pips and the spread are checked over no pin");
            // A hair of zoom, so the marks are placed again under the settings now set.
            map.LargeZoom *= 1.001f;
            yield return Until(() => MapSpots.PipsOverPinsForTest, 5);
            p.Check(MapSpots.PipsOverPinsForTest, "with Pips over pins on, the pips stand over the game's pins and their names");
            yield return Picture(p, "stacked marks under a pin of the game's, their pips over it");

            var stacked = MapSpots.StackedDotForTest();
            if (stacked is Vector2 at)
            {
                PointerTo(at);
                yield return Until(() => MapSpots.FanOverPinsForTest && MapSpots.PinsFadedForTest > 0, 3);
                p.Check(MapSpots.FanOverPinsForTest && MapSpots.PinsFadedForTest > 0, "pointed at, the spread stands over the pins, the pin beside it faded to a third",
                    $"over the pins {MapSpots.FanOverPinsForTest}, {Numbers.Count(MapSpots.PinsFadedForTest)} faded");
                yield return Picture(p, "stacked marks pointed at over a pin of the game's, the pin faded");
                PointerTo(at + new Vector2(Screen.width / 4f, 0f));
                yield return Until(() => MapSpots.FannedShown == 0 && MapSpots.PinsFadedForTest == 0, 3);
                p.Check(MapSpots.PinsFadedForTest == 0, "the pointer gone, the pin stands at full again", $"{Numbers.Count(MapSpots.PinsFadedForTest)} still faded");
            }
            else p.Check(false, "a stacked mark stands on the screen to point at over a pin");

            if (MapSpots.LoneDotForTest() is Vector2 lone)
            {
                PointerTo(lone);
                yield return Until(() => MapSpots.LiftedForTest, 3);
                p.Check(MapSpots.LiftedForTest && MapSpots.Pointed?.Ring != null, "a single dot pointed at stands over the pins while the pointer rests on it");
                PointerTo(lone + new Vector2(Screen.width / 4f, 0f));
                yield return Until(() => !MapSpots.LiftedForTest, 3);
                p.Check(!MapSpots.LiftedForTest, "the pointer gone, it stands under the pins again");
            }
            else p.Check(false, "a lone dot stands on the screen to point at");
            OverPinsAsTheyWere();
        }

        /// <summary>
        /// The lone copper dot clicked on the large map, through the map's own click as a player's
        /// is (<c>Minimap.OnMapLeftClick</c>): its kind's page opens in Scry and the map goes back to
        /// the minimap. Pictured, then the large map opened again where it stood.
        /// </summary>
        private static IEnumerator DotClickOpensPage(Probe p, Minimap map, string kind)
        {
            var lone = MapSpots.LoneDotForTest();
            if (lone == null)
            {
                p.Check(false, "a lone dot stands on the screen to click");
                yield break;
            }
            var (zoom, panned) = (map.LargeZoom, MapPanned(map));
            PointerTo(lone.Value);
            yield return null;
            MapSpots.PressedForTest(lone.Value);
            map.OnMapLeftClick();
            yield return Until(() => Session.IsOpen && X.Selected != null, 3);
            var wanted = X.Find(kind)?.Key;
            p.Check(MapMarkButtons.DotOpenedForTest == wanted && map.m_mode == Minimap.MapMode.Small && Session.IsOpen && X.Selected?.Key == wanted,
                "a click on a dot opens its kind's page in Scry, the map going back to the minimap", $"{MapMarkButtons.DotOpenedForTest}, {map.m_mode}, {X.Selected?.Key}");
            yield return Settled();
            yield return Picture(p, "a dot clicked on the map, its page open");
            Session.Hide();
            map.SetMapMode(Minimap.MapMode.Large);
            map.LargeZoom = zoom;
            MapPanned(map) = panned;
            yield return null;
        }

        /// <summary>
        /// A pin of the game's laid as the player lays one, not saved, by the game's own call; its
        /// last parameter, of a type in an assembly the self-test does not build against, at its
        /// default. None where the game has no such call.
        /// </summary>
        private static Minimap.PinData AddTestPin(Minimap map, Vector3 at)
        {
            var add = HarmonyLib.AccessTools.Method(typeof(Minimap), "AddPin");
            if (add == null) return null;
            var given = add.GetParameters();
            var args = new object[given.Length];
            var first = new object[] { at, Minimap.PinType.Icon3, "Self-test pin", false, false, 0L };
            for (var i = 0; i < args.Length; i++)
            {
                args[i] = i < first.Length ? first[i]
                    : given[i].HasDefaultValue && given[i].DefaultValue != null ? given[i].DefaultValue
                    : given[i].ParameterType.IsValueType ? System.Activator.CreateInstance(given[i].ParameterType) : null;
            }
            return add.Invoke(map, args) as Minimap.PinData;
        }

        /// <summary>The settings as they were and the pin laid taken off again, should the case have stopped before it did.</summary>
        private static void OverPinsAsTheyWere()
        {
            if (_overPinsBefore is (MarksOverPins marks, bool pips, PinsNearPointer near))
            {
                Settings.MarksOverPins = marks;
                Settings.PipsOverPins = pips;
                Settings.PinsNearPointer = near;
            }
            _overPinsBefore = null;
            if (_testPin != null && Minimap.instance != null) Minimap.instance.RemovePin(_testPin);
            _testPin = null;
        }
    }
}
