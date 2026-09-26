#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="SO_ColorSet.GetShieldedSignalColor"/> against the two things that made the
    /// Squirrel's omni crystal card wrong for three rounds, both of which are invisible to every
    /// other check this project has because they are facts about a COLOUR rather than about code.
    ///
    /// <para><b>1. It must depict the mass.</b> The authored base face is a third of the saturation
    /// range away from what shielded prisms render as, because they are HDR, they bloom, and ACES
    /// desaturates anything bright.</para>
    ///
    /// <para><b>2. It must stay out of the HUD's own vocabulary.</b> The same row draws the element
    /// petal ladder, whose five colours MEAN things (fire = deficit, grey = 0, white = +1, blue =
    /// +2, lime = +3). Jade's uncorrected shielded signal was <c>blueColor</c> to within 0.3 degrees
    /// of hue — so the icon was, literally, the colour that means "two upgrades in". That collision
    /// is arithmetic rather than bad luck: both are the same blue.</para>
    ///
    /// <para>These read the SHIPPED assets, so they fail on a palette edit that reopens either
    /// problem — which is the only way this class of defect can be caught without flying.</para>
    /// </summary>
    public class ShieldedSignalColorTests
    {
        // Measured off a screenshot of Jade shielded prisms: four samples, modal saturations
        // 0.660 / 0.630 / 0.583 / 0.630, mean 0.626.
        const float MeasuredRenderedSaturation = 0.626f;
        const float MeasuredTolerance = 0.06f;   // covers the 0.583-0.660 sample spread

        // Below this the icon and a petal read as the same colour at the ~60px the row draws at.
        // The shipped-and-wrong value cleared blueColor by 0.041; the corrected one clears by 0.164.
        const float MinLadderSaturationClearance = 0.10f;

        static readonly Domains[] Playable = { Domains.Jade, Domains.Ruby, Domains.Gold };

        static SO_ColorSet Palette()
        {
            var container = Resources.Load<ThemeManagerDataContainerSO>("ThemeManagerDataContainer")
                            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeManagerDataContainerSO>(
                                "Assets/_SO_Assets/ThemeManagerDataContainer.asset");
            Assert.IsNotNull(container, "The live ThemeManagerDataContainer is missing.");
            Assert.IsNotNull(container.ColorSet, "The live container names no ColorSet.");
            return container.ColorSet;
        }

        static ElementalBarsConfigSO Ladder()
        {
            var cfg = Resources.Load<ElementalBarsConfigSO>("ElementalBarsConfig");
            Assert.IsNotNull(cfg, "Resources/ElementalBarsConfig is missing.");
            return cfg;
        }

        static float Sat(Color c)
        {
            Color.RGBToHSV(c, out _, out float s, out _);
            return s;
        }

        static float Hue(Color c)
        {
            Color.RGBToHSV(c, out float h, out _, out _);
            return h * 360f;
        }

        [Test]
        public void TheSentinelIsRefused()
        {
            // Domains.Blue has a full row in the palette and TryGetColorSetByDomain returns it, so
            // without this an unresolved domain renders as a plausible TEAM rather than as a
            // failure. Alpha 0 is what makes the caller keep its white.
            Assert.AreEqual(0f, Palette().GetShieldedSignalColor(Domains.Blue).a, 0.0001f,
                "Blue is the no-team sentinel; a UI slot must never be able to paint it.");
        }

        [Test]
        public void JadeMatchesWhatShieldedPrismsRenderAs()
        {
            float s = Sat(Palette().GetShieldedSignalColor(Domains.Jade));
            Assert.AreEqual(MeasuredRenderedSaturation, s, MeasuredTolerance,
                $"Jade's shielded signal is saturation {s:F3}; shielded prisms MEASURE {MeasuredRenderedSaturation:F3} " +
                "on screen. An icon that copies the authored base face does not depict the mass it draws.");
        }

        [Test]
        public void NoDomainCollidesWithThePetalLadder()
        {
            var palette = Palette();
            var ladder = Ladder();
            var ladderColors = new[]
            {
                ("fire", ladder.fireColor), ("grey", ladder.greyColor), ("white", ladder.whiteColor),
                ("blue", ladder.blueColor), ("lime", ladder.limeColor),
            };

            foreach (var domain in Playable)
            {
                Color sig = palette.GetShieldedSignalColor(domain);
                Assert.Greater(sig.a, 0f, $"{domain} authors no shielded base face.");

                foreach (var (name, rung) in ladderColors)
                {
                    // Same hue is fine on its own - what makes two swatches indistinguishable is
                    // same hue AND same saturation at the same value.
                    float dHue = Mathf.Abs(Mathf.DeltaAngle(Hue(sig), Hue(rung)));
                    if (dHue > 20f) continue;

                    float dSat = Mathf.Abs(Sat(sig) - Sat(rung));
                    Assert.Greater(dSat, MinLadderSaturationClearance,
                        $"{domain}'s shielded signal is {dHue:F1} deg and {dSat:F3} saturation from the petal " +
                        $"ladder's '{name}', which on the same HUD row means a specific UPGRADE LEVEL. " +
                        "Two colours that close read as one.");
                }
            }
        }

        [Test]
        public void EveryPlayableDomainKeepsItsOwnHue()
        {
            // The lift must preserve hue, or all three domains drift toward one ice blue and the
            // card stops saying which team laid the mass.
            var palette = Palette();
            var hues = new float[Playable.Length];
            for (int i = 0; i < Playable.Length; i++)
                hues[i] = Hue(palette.GetShieldedSignalColor(Playable[i]));

            for (int i = 0; i < hues.Length; i++)
                for (int j = i + 1; j < hues.Length; j++)
                    Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(hues[i], hues[j])), 30f,
                        $"{Playable[i]} and {Playable[j]} render the same shielded hue.");
        }
    }
}
#endif
