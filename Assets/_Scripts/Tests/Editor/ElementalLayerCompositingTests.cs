#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins <see cref="ResourceSystem.CompositeEffectiveLevel"/> — how the elemental layers
    /// stack — and with it the LOCKED <b>maintained-mechanism law</b>: no sustained/held
    /// mechanism may HOLD an element above integer level 10; the 10..15 overcharge band belongs
    /// to TRANSIENTS only, so a player always gets to feel a reward above 10 and the drain
    /// always restores the headroom to feel the next one.
    ///
    /// <para>Three mechanisms live under that law today — temporary effects (which decay to
    /// zero), crystal-earned base overcharge (which bleeds down via
    /// <c>RecoverBaseLevels</c>) and the comeback bonus (which fills toward 10 and never past).
    /// A FOURTH, the domain fauna buff, was removed in Sep 2026 (`Docs/ECOSYSTEM.md` §15) and
    /// took <c>DomainFaunaBuffTests</c> with it. This file exists because the law outlived that
    /// mechanic and a LOCKED law with no pin is a law nobody notices breaking.</para>
    /// </summary>
    [TestFixture]
    public class ElementalLayerCompositingTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void BaseAndTemporary_SimplyAdd()
        {
            Assert.AreEqual(0.7f,
                ResourceSystem.CompositeEffectiveLevel(0.4f, 0.3f, 0f), Tolerance,
                "Base + temporary modifiers add with no cap below the ceiling.");
        }

        [Test]
        public void ComebackFillsOnlyTheRoomBelowTheCeiling()
        {
            // Plenty of room -> the whole bonus lands.
            Assert.AreEqual(0.6f,
                ResourceSystem.CompositeEffectiveLevel(0.2f, 0f, 0.4f), Tolerance);
            // Straddling the ceiling -> only the room below it is granted.
            Assert.AreEqual(1.0f,
                ResourceSystem.CompositeEffectiveLevel(0.8f, 0f, 0.5f), Tolerance,
                "The comeback layer must fill to level 10 and stop.");
            // No room at all -> nothing.
            Assert.AreEqual(1.0f,
                ResourceSystem.CompositeEffectiveLevel(1.0f, 0f, 0.5f), Tolerance);
        }

        [Test]
        public void ComebackNeverRidesOnBaseOvercharge()
        {
            // Base already above 10 (draining) gets no charity on top of it.
            Assert.AreEqual(1.2f,
                ResourceSystem.CompositeEffectiveLevel(1.2f, 0f, 0.6f), Tolerance,
                "A sustained layer must not extend base overcharge.");
        }

        [Test]
        public void ComebackYieldsToEarnedPower()
        {
            // base + a temporary effect already reach the ceiling -> the bonus is zero.
            Assert.AreEqual(1.0f,
                ResourceSystem.CompositeEffectiveLevel(0.6f, 0.4f, 0.5f), Tolerance,
                "The comeback layer must yield to power the pilot already has.");
        }

        [Test]
        public void OnlyTransientsReachTheOverchargeBand_AndItClampsAt15()
        {
            Assert.AreEqual(1.3f,
                ResourceSystem.CompositeEffectiveLevel(0f, 1.3f, 0f), Tolerance,
                "A temporary effect must be felt above the sustained ceiling.");
            Assert.AreEqual(1.5f,
                ResourceSystem.CompositeEffectiveLevel(0f, 2.0f, 0f), Tolerance,
                "The overcharge band clamps at level 15.");
            // And no sustained layer can get there on its own.
            Assert.AreEqual(1.0f,
                ResourceSystem.CompositeEffectiveLevel(0f, 0f, 2.0f), Tolerance,
                "No sustained layer may HOLD an element above level 10.");
        }

        [Test]
        public void TheDeficitBandClampsAtMinus5()
        {
            Assert.AreEqual(-0.5f,
                ResourceSystem.CompositeEffectiveLevel(0f, -2.0f, 0f), Tolerance,
                "The deficit band clamps at level -5.");
            // A deficit leaves EXTRA room for the comeback layer - which is the point of it.
            Assert.AreEqual(1.0f,
                ResourceSystem.CompositeEffectiveLevel(-0.3f, 0f, 1.3f), Tolerance,
                "A comeback bonus may lift a debuffed element all the way to the ceiling.");
        }
    }
}
#endif
