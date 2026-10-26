using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for the ground a place's own modifiers dig, its liquid and its settling parts.</summary>
    internal static partial class SelfTest
    {
        private static (int Backdrop, bool Spin, string Choice, bool Inside)? _dugBefore;

        /// <summary>
        /// A place's own modifiers dig its ground on the stage as the game digs the world's under
        /// it: the tar pit stands in its bowl, the Ground backdrop's ground following it down, its tar
        /// settled in the bowl and drawn, small tar floating on the tar and big deposits on the pit's
        /// floor; a Burial Chambers' entrance, seen from outside, stands in its sunk stair pit. Each is
        /// pictured, and the backdrop, spin, ground and inside switch put back.
        /// </summary>
        private static IEnumerator DugGround(Probe p)
        {
            var pit = LocationNamed("TarPit1", "TarPit2", "TarPit3");
            var crypt = LocationNamed("Crypt3", "Crypt2", "Crypt4");
            if (pit == null && crypt == null) p.Skip("there is neither a tar pit nor a Burial Chambers to show");
            _dugBefore = (Stage.BackdropIndex, Stage.Spin, Stage.GroundChoice, Stage.Inside);
            Stage.Spin = false;
            Stage.GroundChoice = "Auto";
            Stage.BackdropIndex = 5;
            Stage.Inside = false;

            if (pit != null)
            {
                Select(pit);
                yield return Until(() => CopyOf(pit) != null && Stage.GroundShown != null && Stage.GroundSettled.ShapingCount > 0, 30);
                yield return new Wait(1);
                var settle = Stage.GroundSettled;
                var middle = settle.Middle;
                var floor = settle.Shape.HeightAt(0f, 0f);
                p.Check(floor < -4f, $"{pit.Name}'s modifiers dig its middle four metres and more down", $"{Numbers.Fixed(floor, 2)} m from {Numbers.Count(settle.ShapingCount)} modifiers");
                var mesh = Stage.GroundMeshHeightNear(middle.x, middle.z);
                p.Check(mesh is float down && down - middle.y < -3.5f, "the Ground backdrop's ground follows the pit down",
                    mesh is float h ? $"{Numbers.Fixed(h - middle.y, 2)} m at the middle" : "no ground laid");

                var liquids = settle.LiquidsLaid.ToList();
                var level = float.NaN;
                if (p.Check(liquids.Count == 1 && liquids[0].Wet > 0, "its tar settles in the pit", $"{Numbers.Count(liquids.Count)} liquids"))
                {
                    level = liquids[0].Level;
                    p.Check(level - middle.y > -1.2f && level - middle.y < -0.7f, "the tar lies about a metre under the flat ground, filling the bowl",
                        $"{Numbers.Fixed(level - middle.y, 2)} m, {Numbers.Count(liquids[0].Wet)} points under it");
                    var filter = liquids[0].At.GetComponent<MeshFilter>();
                    var renderer = liquids[0].At.GetComponent<MeshRenderer>();
                    var surface = filter != null ? filter.sharedMesh : null;
                    p.Check(surface != null && surface.vertexCount > 0 && renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy,
                        "its tar is drawn with the game's own material over a surface made as the game makes it",
                        $"{(surface != null ? Numbers.Count(surface.vertexCount) + " points" : "no mesh")}, {(renderer != null ? renderer.sharedMaterial.OrNull()?.shader.OrNull()?.name ?? "no material" : "no renderer")}");
                }

                var settled = settle.Settled.ToList();
                var falls = settled.Where(s => (s.How & PartRest.Fall) != 0).ToList();
                var floats = settled.Where(s => (s.How & PartRest.Floats) != 0).ToList();
                float GroundUnder(Vector3 at) => middle.y + settle.Shape.HeightAt(at.x - middle.x, at.z - middle.z);
                var offGround = falls.Count(s => Mathf.Abs(s.At.position.y - GroundUnder(s.At.position)) > 0.06f);
                p.Check(falls.Count >= 3 && offGround == 0, "its big tar rests on the pit's floor", $"{Numbers.Count(falls.Count)} falling, {Numbers.Count(offGround)} off the ground");
                var offTar = floats.Count(s => float.IsNaN(level) || Mathf.Abs(s.At.position.y - level - 0.1f) > 0.06f);
                p.Check(floats.Count >= 6 && offTar == 0, "its small tar floats on the tar", $"{Numbers.Count(floats.Count)} floating, {Numbers.Count(offTar)} off the tar");
                yield return Picture(p, "dug ground " + pit.Name);
            }

            if (crypt != null)
            {
                Select(crypt);
                yield return Until(() => CopyOf(crypt) != null && Stage.GroundShown != null && Stage.GroundSettled.ShapingCount > 0, 60);
                yield return new Wait(1);
                var settle = Stage.GroundSettled;
                var middle = settle.Middle;
                var lowest = 0f;
                var at = Vector2.zero;
                for (var x = -12; x <= 12; x++)
                {
                    for (var z = -12; z <= 12; z++)
                    {
                        var h = settle.Shape.HeightAt(x, z);
                        if (h >= lowest) continue;
                        lowest = h;
                        at = new Vector2(x, z);
                    }
                }
                p.Check(lowest < -2.5f, $"{crypt.Name}'s sinks dig its stair pit down", $"{Numbers.Fixed(lowest, 2)} m at its lowest");
                var mesh = Stage.GroundMeshHeightNear(middle.x + at.x, middle.z + at.y);
                p.Check(mesh is float down && down - middle.y < -2f, "the Ground backdrop's ground follows the stair pit down",
                    mesh is float h2 ? $"{Numbers.Fixed(h2 - middle.y, 2)} m there" : "no ground laid");
                yield return Picture(p, "dug ground " + crypt.Name);
            }
            DugGroundAsItWas();
        }

        /// <summary>The backdrop, spin, ground and inside switch put back as they were before <see cref="DugGround"/>.</summary>
        private static void DugGroundAsItWas()
        {
            if (_dugBefore is (int backdrop, bool spin, string choice, bool inside))
            {
                Stage.BackdropIndex = backdrop;
                Stage.Spin = spin;
                Stage.GroundChoice = choice;
                Stage.Inside = inside;
            }
            _dugBefore = null;
        }
    }
}
