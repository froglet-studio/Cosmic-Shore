using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The woken omni's game motion on the Elemental Converter's stage (Omni Motion Lab round 5): its squares, rhombi and
    /// pentagons swelled to the game's thicker plates, and its squares turned as the game turns them
    /// (<see cref="SquareTurnFit"/>, the fit <c>CrystalSpaceSquares</c>' blend shapes are built from). Fitted once from
    /// the combine data (<see cref="OmniCombineData"/>); <c>OmniCombinePlayer</c> poses it every frame on the cadence.
    /// Held by <c>Tools/Build/converter_round5_harness</c> on the shipped data.
    /// </summary>
    public sealed class OmniCombineMotion
    {
        /// <summary>Per omni vertex (xyz): its move to the thicker plate, at full swell.</summary>
        public readonly float[] ThickOffset;
        public readonly SquareTurnFit[] Squares;
        /// <summary>Per square: its corners (omni vertex indices).</summary>
        public readonly int[][] SquareCorners;

        OmniCombineMotion(float[] thickOffset, SquareTurnFit[] squares, int[][] squareCorners)
        {
            ThickOffset = thickOffset;
            Squares = squares;
            SquareCorners = squareCorners;
        }

        /// <summary>
        /// Every non-Mass plate's offset to <paramref name="thickness"/> times its thickness (about its own centre, along
        /// outer face centre - inner face centre, as <c>CrystalSpaceSquares</c> thickens the game's body), and each square
        /// plate's turns, fitted to the combine data's three square keys (pentagon, Step1, Step2). Refused, named, when the
        /// keys are missing or a square's key is not a rigid turn.
        /// </summary>
        public static bool TryFit(OmniCombineData data, float omniRadius, float thickness, out OmniCombineMotion motion, out string problem)
        {
            motion = null;
            var ov = data.omni.verts;
            int n = ov.Length / 3;
            var thickOffset = new float[ov.Length];
            var rest = new Vector3[n];
            for (int i = 0; i < n; i++) rest[i] = new Vector3(ov[3 * i], ov[3 * i + 1], ov[3 * i + 2]);
            var byPlate = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                if (!byPlate.TryGetValue(data.omni.plate[i], out var list)) byPlate[data.omni.plate[i]] = list = new List<int>();
                list.Add(i);
            }

            var keys = data.squares?.keys;
            if (keys == null || keys.Length < 3)
            {
                problem = "the combine data carries no square keys (pentagon, Step1, Step2): re-run Tools/Build/author_omni_combine_asset.py";
                return false;
            }
            var offsets = new Dictionary<int, Vector3>[3];
            for (int k = 0; k < 3; k++)
            {
                offsets[k] = new Dictionary<int, Vector3>();
                for (int j = 0; j < keys[k].verts.Length; j++)
                    offsets[k][keys[k].verts[j]] = new Vector3(keys[k].delta[3 * j], keys[k].delta[3 * j + 1], keys[k].delta[3 * j + 2]);
            }
            Vector3 Offset(int k, int i) => offsets[k].TryGetValue(i, out var d) ? d : Vector3.zero;

            var turns = new List<SquareTurnFit>();
            var corners = new List<int[]>();
            float tolerance = 1e-3f * omniRadius;
            foreach (var pair in byPlate)
            {
                int owner = data.omni.plateOwner[pair.Key];
                if (owner == OmniCombineCore.Mass) continue;
                var idx = pair.Value;
                Vector3 centre = Vector3.zero;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (int i in idx)
                {
                    centre += rest[i];
                    lo = Mathf.Min(lo, rest[i].magnitude);
                    hi = Mathf.Max(hi, rest[i].magnitude);
                }
                centre /= idx.Count;
                float mid = (lo + hi) / 2f;
                Vector3 outer = Vector3.zero, inner = Vector3.zero;
                int no = 0, ni = 0;
                foreach (int i in idx)
                    if (rest[i].magnitude > mid) { outer += rest[i]; no++; }
                    else { inner += rest[i]; ni++; }
                if (no > 0 && ni > 0)
                {
                    var normal = (outer / no - inner / ni).normalized;
                    foreach (int i in idx)
                    {
                        var d = normal * (Vector3.Dot(rest[i] - centre, normal) * (thickness - 1f));
                        thickOffset[3 * i] = d.x;
                        thickOffset[3 * i + 1] = d.y;
                        thickOffset[3 * i + 2] = d.z;
                    }
                }
                if (owner != OmniCombineCore.Space) continue;
                // Measured on the thin corners, as the game measures them.
                if (!SquareTurnFit.TryFit(idx, rest, centre, i => Offset(0, i), i => Offset(1, i), i => Offset(2, i),
                                          tolerance, out var turn, out problem))
                    return false;
                turns.Add(turn);
                corners.Add(idx.ToArray());
            }
            if (turns.Count == 0)
            {
                problem = "the combine's omni has no square plates to turn";
                return false;
            }
            motion = new OmniCombineMotion(thickOffset, turns.ToArray(), corners.ToArray());
            problem = null;
            return true;
        }

        /// <summary>The omni's rest vertices with its plates <paramref name="swell"/> (0..1) of the way to the thicker
        /// plates, into <paramref name="into"/>.</summary>
        public void Swell(float[] verts, float swell, float[] into)
        {
            for (int i = 0; i < verts.Length; i++) into[i] = verts[i] + ThickOffset[i] * swell;
        }

        /// <summary>Every square <paramref name="e"/> (eased, 0..1) of the way through its pentagon or triangle turn: each
        /// corner of <paramref name="rest"/> turned rigidly and hopped, written into <paramref name="pose"/>.</summary>
        public void TurnSquares(float[] rest, float[] pose, bool pentagon, float e, float pentagonHop, float triangleLift, float triangleSide)
        {
            for (int sq = 0; sq < Squares.Length; sq++)
            {
                Squares[sq].At(pentagon, e, pentagonHop, triangleLift, triangleSide, out var rotation, out var shift);
                foreach (int i in SquareCorners[sq])
                {
                    var v = rotation * new Vector3(rest[3 * i], rest[3 * i + 1], rest[3 * i + 2]) + shift;
                    pose[3 * i] = v.x;
                    pose[3 * i + 1] = v.y;
                    pose[3 * i + 2] = v.z;
                }
            }
        }
    }
}
