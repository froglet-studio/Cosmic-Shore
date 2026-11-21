using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The boot world's loading readout (Docs/MENU_REDESIGN_B.md section 25): a slim bar, never a
    /// headline. These pin what the bar says in each phase of the build.
    /// </summary>
    public class EnvironmentLoadVeilTests
    {
        [Test]
        public void Bar_FillsToTheLayProgress_WhileLaying()
        {
            Assert.AreEqual(new Vector2(0f, 0.4f), EnvironmentLoadVeil.BarSpan(true, 0.4f, false, 3f));
        }

        [Test]
        public void Bar_IsFull_WhileThePrismsSettle()
        {
            Assert.AreEqual(new Vector2(0f, 1f), EnvironmentLoadVeil.BarSpan(false, 1f, true, 3f));
        }

        [Test]
        public void Bar_DriftsAShortSegment_BeforeTheBuildStarts()
        {
            // The scene is still settling and nothing is laid yet: the bar must not claim progress.
            for (float t = 0f; t < 6f; t += 0.37f)
            {
                var span = EnvironmentLoadVeil.BarSpan(false, 0f, false, t);
                Assert.That(span.x, Is.InRange(0f, 1f));
                Assert.That(span.y, Is.InRange(0f, 1f));
                Assert.That(span.y - span.x, Is.EqualTo(0.22f).Within(1e-4f));
            }
        }
    }
}
