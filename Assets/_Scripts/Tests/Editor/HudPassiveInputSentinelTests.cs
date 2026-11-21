using System;
using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The HUD's press resolver must never read <c>FullSpeedStraightAction</c> as a button press.
    ///
    /// <para>The input enum's zero is both a REAL event (raised while the throttle is buried and the
    /// stick is centred) and the "no button" sentinel a passive ability map entry is authored with.
    /// Resolving it through the map pressed the FIRST passive card whenever a pilot flew flat out -
    /// the Squirrel's joust card, reported as "a full speed indicator on the joust icon". This is a
    /// logic rule with no type to break, so no offline compile gate can see it regress.</para>
    /// </summary>
    public class HudPassiveInputSentinelTests
    {
        [Test]
        public void FullSpeedStraightIsThePassiveSentinel()
        {
            Assert.IsTrue(VesselHUDController.IsPassiveSentinel(InputEvents.FullSpeedStraightAction));
        }

        [Test]
        public void EveryRealControlStillPressesItsCard()
        {
            foreach (InputEvents ev in Enum.GetValues(typeof(InputEvents)))
            {
                if (ev == InputEvents.FullSpeedStraightAction) continue;
                Assert.IsFalse(VesselHUDController.IsPassiveSentinel(ev),
                    $"{ev} is a real control; treating it as passive would stop its card lighting.");
            }
        }
    }
}
