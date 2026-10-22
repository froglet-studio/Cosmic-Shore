using NUnit.Framework;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pad's steering curve (<see cref="GamepadFlightConfigSO"/>): it must keep every reading's
    /// sign, leave centre at centre, never reorder deflections, and be the identity at exponent 1 and
    /// sensitivity 1 (the pad as it flew with no curve).
    /// </summary>
    [TestFixture]
    public class GamepadFlightConfigTests
    {
        [TestCase(0.3f)]
        [TestCase(-0.3f)]
        [TestCase(1f)]
        [TestCase(-1f)]
        public void KeepsTheSign(float value)
        {
            Assert.That(Mathf.Sign(GamepadFlightConfigSO.Shape(value, 1.5f, 0.85f)), Is.EqualTo(Mathf.Sign(value)));
        }

        [Test]
        public void CentreStaysCentre()
        {
            Assert.That(GamepadFlightConfigSO.Shape(0f, 1.5f, 0.85f), Is.EqualTo(0f));
        }

        [Test]
        public void IsOdd_SoItCommutesWithInvertY()
        {
            for (float v = 0f; v <= 1f; v += 0.1f)
                Assert.That(GamepadFlightConfigSO.Shape(-v, 1.7f, 0.9f),
                    Is.EqualTo(-GamepadFlightConfigSO.Shape(v, 1.7f, 0.9f)).Within(1e-6f));
        }

        [Test]
        public void IsMonotonic()
        {
            float previous = GamepadFlightConfigSO.Shape(-1f, 1.5f, 0.85f);
            for (float v = -0.95f; v <= 1.001f; v += 0.05f)
            {
                float shaped = GamepadFlightConfigSO.Shape(v, 1.5f, 0.85f);
                Assert.That(shaped, Is.GreaterThanOrEqualTo(previous));
                previous = shaped;
            }
        }

        [Test]
        public void ExponentOneSensitivityOne_IsTheUncurvedPad()
        {
            for (float v = -1f; v <= 1.001f; v += 0.1f)
                Assert.That(GamepadFlightConfigSO.Shape(v, 1f, 1f), Is.EqualTo(v).Within(1e-6f));
        }

        [Test]
        public void SoftensSmallDeflectionsMoreThanFullStick()
        {
            float smallRatio = GamepadFlightConfigSO.Shape(0.2f, 1.5f, 1f) / 0.2f;
            float fullRatio = GamepadFlightConfigSO.Shape(1f, 1.5f, 1f) / 1f;
            Assert.That(smallRatio, Is.LessThan(fullRatio));
            Assert.That(fullRatio, Is.EqualTo(1f).Within(1e-6f), "full stick keeps the hull's full rate at sensitivity 1");
        }

        [Test]
        public void ShippedDefaults_AreTheDocumentedOnes()
        {
            var config = ScriptableObject.CreateInstance<GamepadFlightConfigSO>();
            Assert.That(config.ResponseExponent, Is.EqualTo(1.5f));
            Assert.That(config.SteeringSensitivity, Is.EqualTo(0.85f));
            Object.DestroyImmediate(config);
        }
    }
}
