using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Chum's reef, kept pure so the edit-mode tests (and the generator's Python twin) can hold it without a scene.
    ///
    /// <para><b>A reef is SHOALS of neutral prisms</b> laid by the mode on every peer from the intensity alone (no seed,
    /// no random draw): <see cref="ShoalCount"/> balls of prisms on a golden-angle spiral over the sphere, a band of the
    /// way from the nucleus to the membrane. A shoal is a centre prism and <see cref="Shells"/> concentric shells of
    /// prisms on a Fibonacci sphere, <see cref="Spacing"/> apart, so neighbours never touch (the prism is
    /// <see cref="PrismSize"/> on a side, under the spacing). The prisms are <see cref="Domains.Blue"/> (no team), so every
    /// Angler's brush paints them and every grazer eats them outside the nucleus.</para>
    ///
    /// <para><b>The crust.</b> From I3 the outermost shell is SHIELDED: no grazer eats shielded mass (LOCKED), so the
    /// wild herds cannot strip a crusted shoal, and an Angler cracks it with Paint Shields (its Space level-5 upgrade,
    /// which the card starts those rungs at) or its hull.</para>
    /// </summary>
    public static class ChumReef
    {
        public const int DomainSlots = 3;

        /// <summary>The prism's side, u (a cube).</summary>
        public const float PrismSize = 7f;

        /// <summary>Centre-to-centre distance between neighbouring prisms on a shell, and between shells, u.</summary>
        public const float Spacing = 15f;

        /// <summary>One rung of the reef.</summary>
        public readonly struct Rung
        {
            public readonly int ShoalCount;
            public readonly int Shells;
            public readonly float BandFraction;
            public readonly bool Crusted;

            public Rung(int shoalCount, int shells, float bandFraction, bool crusted)
            {
                ShoalCount = shoalCount;
                Shells = shells;
                BandFraction = bandFraction;
                Crusted = crusted;
            }
        }

        /// <summary>I1 Shallows, I2 Reef, I3 Crusted, I4 Barrier (CHUM.md). The generator mirrors this table.</summary>
        public static Rung ForIntensity(int intensity) => Mathf.Clamp(intensity, 1, 4) switch
        {
            1 => new Rung(8, 2, 0.30f, false),
            2 => new Rung(12, 2, 0.45f, false),
            3 => new Rung(12, 3, 0.45f, true),
            _ => new Rung(16, 3, 0.60f, true),
        };

        /// <summary>Jade 0, Ruby 1, Gold 2; -1 for Blue (no team) and anything else.</summary>
        public static int Slot(Domains domain) => domain switch
        {
            Domains.Jade => 0,
            Domains.Ruby => 1,
            Domains.Gold => 2,
            _ => -1,
        };

        /// <summary>Prisms on shell <paramref name="shell"/> (1-based): the sphere's area over one prism's patch.</summary>
        public static int ShellCount(int shell) =>
            shell <= 0 ? 1 : Mathf.Max(6, Mathf.RoundToInt(4f * Mathf.PI * shell * shell));

        /// <summary>Prisms in one shoal of <paramref name="shells"/> shells: the centre plus every shell.</summary>
        public static int ShoalPrisms(int shells)
        {
            int n = 1;
            for (int s = 1; s <= shells; s++) n += ShellCount(s);
            return n;
        }

        /// <summary>Every prism the rung lays.</summary>
        public static int TotalPrisms(Rung rung) => rung.ShoalCount * ShoalPrisms(rung.Shells);

        /// <summary>The <paramref name="index"/>-th of <paramref name="count"/> directions of a golden-angle spiral
        /// over the sphere (unit).</summary>
        public static Vector3 SpiralDirection(int index, int count)
        {
            const float golden = 2.39996323f;
            count = Mathf.Max(1, count);
            float y = 1f - 2f * (index + 0.5f) / count;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float th = index * golden;
            return new Vector3(ring * Mathf.Cos(th), y, ring * Mathf.Sin(th));
        }

        /// <summary>Where shoal <paramref name="index"/> sits, relative to the cell's centre.</summary>
        public static Vector3 ShoalCentre(Rung rung, int index, float nucleusRadius, float membraneRadius)
        {
            float shoal = rung.Shells * Spacing + PrismSize;
            float inner = nucleusRadius + shoal;
            float outer = Mathf.Max(inner, membraneRadius - shoal);
            float r = Mathf.Lerp(inner, outer, Mathf.Clamp01(rung.BandFraction));
            return SpiralDirection(index, rung.ShoalCount) * r;
        }

        /// <summary>One prism to lay, relative to the cell's centre.</summary>
        public readonly struct Piece
        {
            public readonly Vector3 Position;
            public readonly int Shoal;
            public readonly bool Shielded;

            public Piece(Vector3 position, int shoal, bool shielded)
            {
                Position = position;
                Shoal = shoal;
                Shielded = shielded;
            }
        }

        /// <summary>Every prism of the rung, shoal by shoal (cleared first). Pure: the same list on every peer.</summary>
        public static void Lay(Rung rung, float nucleusRadius, float membraneRadius, List<Piece> into)
        {
            into.Clear();
            for (int s = 0; s < rung.ShoalCount; s++)
            {
                Vector3 centre = ShoalCentre(rung, s, nucleusRadius, membraneRadius);
                into.Add(new Piece(centre, s, false));
                for (int shell = 1; shell <= rung.Shells; shell++)
                {
                    int n = ShellCount(shell);
                    bool crust = rung.Crusted && shell == rung.Shells;
                    for (int k = 0; k < n; k++)
                        into.Add(new Piece(centre + SpiralDirection(k, n) * (shell * Spacing), s, crust));
                }
            }
        }

        /// <summary>
        /// What each pilot's school has eaten of the reef, so the results can say it and the controller credits each
        /// prism once. Keyed by pilot name (the RoundStats key every peer shares).
        /// </summary>
        public sealed class EatenBook
        {
            readonly Dictionary<string, int> _eaten = new();

            public int Eaten(string pilot) => pilot != null && _eaten.TryGetValue(pilot, out var n) ? n : 0;

            public void Credit(string pilot)
            {
                if (pilot == null) return;
                _eaten[pilot] = Eaten(pilot) + 1;
            }

            public void Clear() => _eaten.Clear();
        }
    }
}
