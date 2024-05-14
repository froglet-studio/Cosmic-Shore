using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CosmicShore.Integrations.PlayFab.PlayFabTests
{
    public class PlayFabCatalogTests
    {
        // A Test behaves as an ordinary method
        [Test]
        public void PlayFabCatalogTestsSimplePasses()
        {
            // Use the Assert class to test conditions
            var num1 = 1;
            var num2 = 1;
            Assert.That(num1 == num2, "Numbers are equal.");
        }

        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator PlayFabCatalogTestsWithEnumeratorPasses()
        {
            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
        }
    }
}
