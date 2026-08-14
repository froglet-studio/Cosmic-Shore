#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A prism's DEATH VISUAL must wear the colours of the mass it came from.
    ///
    /// WHY THIS MATTERS:
    /// The debris palette used to be resolved from the dying prism's DOMAIN alone, at the
    /// PLAIN tier, so a danger prism - which on screen is a frosty shielded base under a hot
    /// domain-independent danger rim - shattered into ordinary domain-coloured debris and read
    /// as though a plain prism had died. These tests pin the tier composition
    /// (<see cref="SO_ColorSet.GetPrismKindColors"/>, the single source both
    /// <c>ThemeManager</c> and <c>PrismFactory</c> paint from), the "which tier is this prism
    /// wearing" read that feeds it, and the danger tier's detonation gain.
    ///
    /// Everything here is pure: no play mode, no scene, no ECS world.
    /// </summary>
    [TestFixture]
    public class PrismDeathVisualTierTests
    {
        SO_ColorSet _colors;

        // Distinct, easily-identified stand-ins for every field the tier composition reads,
        // so a wrong pairing names itself in the failure message rather than looking plausible.
        static readonly Color JadePlainRim = new(0.10f, 0f, 0f, 1f);
        static readonly Color JadePlainBase = new(0.11f, 0f, 0f, 1f);
        static readonly Color JadeShieldRim = new(0.20f, 0f, 0f, 1f);
        static readonly Color JadeShieldBase = new(0.21f, 0f, 0f, 1f);
        static readonly Color JadeSuperRim = new(0.30f, 0f, 0f, 1f);
        static readonly Color JadeSuperBase = new(0.31f, 0f, 0f, 1f);
        static readonly Color RubyShieldBase = new(0.41f, 0f, 0f, 1f);
        static readonly Color SharedDanger = new(0.90f, 0.5f, 0.1f, 1f);

        [SetUp]
        public void SetUp()
        {
            _colors = ScriptableObject.CreateInstance<SO_ColorSet>();
            _colors.EnvironmentColors = new EnvironmentColorSet { Danger = SharedDanger };
            _colors.JadeColors = new DomainColorSet
            {
                InsideBlockColor = JadePlainRim,
                OutsideBlockColor = JadePlainBase,
                ShieldedInsideBlockColor = JadeShieldRim,
                ShieldedOutsideBlockColor = JadeShieldBase,
                SuperShieldedInsideBlockColor = JadeSuperRim,
                SuperShieldedOutsideBlockColor = JadeSuperBase,
            };
            _colors.RubyColors = new DomainColorSet
            {
                InsideBlockColor = new Color(0.50f, 0f, 0f, 1f),
                OutsideBlockColor = new Color(0.51f, 0f, 0f, 1f),
                ShieldedOutsideBlockColor = RubyShieldBase,
            };
            // GoldColors / BlueColors deliberately left null - the unauthored-domain case.
        }

        [TearDown]
        public void TearDown()
        {
            if (_colors != null) Object.DestroyImmediate(_colors);
            _colors = null;
        }

        // ── The tier composition ────────────────────────────────────────────────

        [Test]
        public void PlainKind_UsesThePlainPair()
        {
            _colors.GetPrismKindColors(_colors.JadeColors, PrismKind.Plain, out var bright, out var dark);

            Assert.AreEqual(JadePlainRim, bright);
            Assert.AreEqual(JadePlainBase, dark);
        }

        [Test]
        public void ShieldedKind_UsesTheShieldedPair()
        {
            _colors.GetPrismKindColors(_colors.JadeColors, PrismKind.Shielded, out var bright, out var dark);

            Assert.AreEqual(JadeShieldRim, bright);
            Assert.AreEqual(JadeShieldBase, dark);
        }

        [Test]
        public void SuperShieldedKind_UsesTheSuperShieldedPair()
        {
            _colors.GetPrismKindColors(_colors.JadeColors, PrismKind.SuperShielded, out var bright, out var dark);

            Assert.AreEqual(JadeSuperRim, bright);
            Assert.AreEqual(JadeSuperBase, dark);
        }

        [Test]
        public void DangerKind_ComposesSharedDangerRimOverTheDomainsShieldedBase()
        {
            // Docs/PALETTE.md section 4.3: the danger tier has no colour fields of its own.
            _colors.GetPrismKindColors(_colors.JadeColors, PrismKind.Danger, out var bright, out var dark);

            Assert.AreEqual(SharedDanger, bright, "Danger rim must be the shared EnvironmentColors.Danger.");
            Assert.AreEqual(JadeShieldBase, dark, "Danger base must be the domain's SHIELDED base face.");
            Assert.AreNotEqual(JadePlainBase, dark, "Danger must NOT fall back to the plain base face.");
        }

        [Test]
        public void DangerRim_IsDomainIndependent()
        {
            _colors.GetPrismKindColors(_colors.JadeColors, PrismKind.Danger, out var jadeRim, out var jadeBase);
            _colors.GetPrismKindColors(_colors.RubyColors, PrismKind.Danger, out var rubyRim, out var rubyBase);

            Assert.AreEqual(jadeRim, rubyRim, "One danger rim for every domain - danger is not safe to its own domain.");
            Assert.AreEqual(JadeShieldBase, jadeBase);
            Assert.AreEqual(RubyShieldBase, rubyBase, "The base is still what says WHOSE the prism was.");
        }

        [Test]
        public void DangerDebris_DoesNotWearThePlainDomainPalette()
        {
            // The reported defect, as a regression guard: danger debris used to be tinted
            // identically to plain-domain debris.
            _colors.GetPrismKindColors(_colors.JadeColors, PrismKind.Plain, out var plainRim, out var plainBase);
            _colors.GetPrismKindColors(_colors.JadeColors, PrismKind.Danger, out var dangerRim, out var dangerBase);

            Assert.AreNotEqual(plainRim, dangerRim);
            Assert.AreNotEqual(plainBase, dangerBase);
        }

        [Test]
        public void TryGetPrismKindColors_ResolvesAnAuthoredDomain()
        {
            Assert.IsTrue(_colors.TryGetPrismKindColors(Domains.Jade, PrismKind.Danger, out var bright, out var dark));
            Assert.AreEqual(SharedDanger, bright);
            Assert.AreEqual(JadeShieldBase, dark);
        }

        [Test]
        public void TryGetPrismKindColors_FailsClosedOnAnUnauthoredDomain()
        {
            // PrismFactory drops to the pooled route (and its own warning) rather than
            // tinting debris from a half-populated theme.
            Assert.IsFalse(_colors.TryGetPrismKindColors(Domains.Gold, PrismKind.Danger, out _, out _));
        }

        // ── Which tier is a prism wearing ───────────────────────────────────────

        [Test]
        public void KindOf_DefaultsToPlain()
        {
            Assert.AreEqual(PrismKind.Plain, PrismKinds.Of(new PrismProperties()));
        }

        [Test]
        public void KindOf_NullPropertiesReadPlain()
        {
            Assert.AreEqual(PrismKind.Plain, PrismKinds.Of((PrismProperties)null));
        }

        [Test]
        public void KindOf_ReadsEachFlag()
        {
            Assert.AreEqual(PrismKind.Danger, PrismKinds.Of(new PrismProperties { IsDangerous = true }));
            Assert.AreEqual(PrismKind.Shielded, PrismKinds.Of(new PrismProperties { IsShielded = true }));
            Assert.AreEqual(PrismKind.SuperShielded, PrismKinds.Of(new PrismProperties { IsSuperShielded = true }));
        }

        [Test]
        public void KindOf_SuperShieldWinsOverCorruptCompanionFlags()
        {
            // The flags are mutually exclusive by construction; if one ever leaks, report the
            // state that actually governs gameplay - super-shield is what makes a prism
            // invulnerable and stops an AOE dead.
            var corrupt = new PrismProperties { IsSuperShielded = true, IsDangerous = true, IsShielded = true };
            Assert.AreEqual(PrismKind.SuperShielded, PrismKinds.Of(corrupt));
        }

        // ── The danger detonation gain ──────────────────────────────────────────

        [Test]
        public void DetonationGain_IsUnityForEveryTierButDanger()
        {
            Assert.AreEqual(1f, PrismExplosion.DetonationGain(PrismKind.Plain, 1.6f));
            Assert.AreEqual(1f, PrismExplosion.DetonationGain(PrismKind.Shielded, 1.6f));
            Assert.AreEqual(1f, PrismExplosion.DetonationGain(PrismKind.SuperShielded, 1.6f));
        }

        [Test]
        public void DetonationGain_AppliesTheAuthoredMultiplierToDanger()
        {
            Assert.AreEqual(1.6f, PrismExplosion.DetonationGain(PrismKind.Danger, 1.6f), 1e-5f);
        }

        [Test]
        public void DetonationGain_NeverCollapsesDebrisToAStandstill()
        {
            // A zero/negative multiplier would clamp every danger prism's debris to the speed
            // floor times nothing - i.e. mass that dies without animating out (continuity law).
            Assert.Greater(PrismExplosion.DetonationGain(PrismKind.Danger, 0f), 0f);
            Assert.Greater(PrismExplosion.DetonationGain(PrismKind.Danger, -3f), 0f);
        }

        [Test]
        public void DetonationGain_OfOneLeavesDangerIdenticalToPlain()
        {
            // The documented "palette only" configuration, so the character is fully tunable
            // back to off from the prefab without touching code.
            Assert.AreEqual(PrismExplosion.DetonationGain(PrismKind.Plain, 1f),
                            PrismExplosion.DetonationGain(PrismKind.Danger, 1f));
        }
    }
}
#endif
