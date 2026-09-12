using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Holds the commerce de-scope that ships in the invite build
    /// (<c>Docs/STEAM_RELEASE_TASKS.md</c> R4): nothing in this window sells anything, so no path
    /// may reach a surface that takes money, and every de-scoped entry must read as a deliberate
    /// state rather than as a dead button.
    ///
    /// <para>Some of these are source-text assertions, the same shape as the other law tests in
    /// this suite (<c>ToySwitchVocabularyTests</c>, <c>PrismOcclusionCoverageTests</c>): the facts
    /// being held are CALL-SITE facts — which files may open the purchase modal, which branch the
    /// checkout listener is wired in — and no compiler sees those.</para>
    ///
    /// <para>The test that matters most is <see cref="MissingConfigFailsClosed"/>. Every other
    /// config in the project falls back to the behaviour that shipped before it existed; this one
    /// must fall back to the DE-SCOPED state, because a money surface that comes back because an
    /// asset failed to load is the one failure nobody would notice until a player hit it.</para>
    /// </summary>
    public class CommerceDeScopeTests
    {
        static string ScriptRoot => Path.Combine(Application.dataPath, "_Scripts");

        static string Source(string relativePath)
        {
            var path = Path.Combine(ScriptRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"Expected source file is missing: {relativePath}");
            return File.ReadAllText(path);
        }

        static IEnumerable<string> AllSources() =>
            Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories);

        // ------------------------------------------------------------------
        // The posture itself

        [Test]
        public void ShippedConfigIsDeScoped()
        {
            var config = Resources.Load<SO_CommerceAvailability>("CommerceAvailability");
            Assert.IsNotNull(config,
                "Resources/CommerceAvailability is missing. The code defaults are de-scoped too, so " +
                "this is not a live defect - but the asset is where the paid-EA conversion happens, " +
                "so its absence means there is nothing to flip.");

            Assert.AreEqual(MenuAvailability.Unavailable, config.AvailabilityFor(CommerceSurface.StoreScreen),
                "The Store screen should read Unavailable: nothing in it is purchasable this window, " +
                "so 'this is not built' is the honest state.");
            Assert.AreEqual(MenuAvailability.Locked, config.AvailabilityFor(CommerceSurface.Episodes),
                "Episodes should read Locked: the content exists and the entitlement is real, the " +
                "player simply cannot buy one YET.");
            Assert.AreEqual(MenuAvailability.Locked, config.AvailabilityFor(CommerceSurface.CatalogPurchase),
                "Catalog purchases should read Locked.");
            Assert.IsFalse(config.AllowRealMoneyCheckout,
                "Real-money checkout must stay off until a backend verifies orders " +
                "(Docs/MENU_PROGRESSION_AND_IAP.md section 5).");
        }

        [Test]
        public void MissingConfigFailsClosed()
        {
            var fresh = ScriptableObject.CreateInstance<SO_CommerceAvailability>();
            try
            {
                foreach (CommerceSurface surface in System.Enum.GetValues(typeof(CommerceSurface)))
                    Assert.IsFalse(fresh.IsAvailable(surface),
                        $"A config with no asset behind it reports {surface} as Available. The code " +
                        "defaults MUST be the de-scoped state - an absent asset has to close the " +
                        "money surfaces, never open them.");

                Assert.IsFalse(fresh.AllowRealMoneyCheckout,
                    "A config with no asset behind it allows real-money checkout.");
            }
            finally { Object.DestroyImmediate(fresh); }
        }

        [Test]
        public void EveryLockedSurfaceGivesAReason()
        {
            var config = Resources.Load<SO_CommerceAvailability>("CommerceAvailability")
                         ?? ScriptableObject.CreateInstance<SO_CommerceAvailability>();

            foreach (CommerceSurface surface in System.Enum.GetValues(typeof(CommerceSurface)))
            {
                if (config.AvailabilityFor(surface) != MenuAvailability.Locked) continue;

                // Locked stays pressable on purpose: the press is how the player is TOLD. A Locked
                // surface with no wording answers with a bare sting, which is the unexplained
                // refusal this de-scope exists to replace. (Unavailable is exempt - it is inert and
                // has nothing to say, and its dimming is the whole message.)
                Assert.IsFalse(string.IsNullOrWhiteSpace(config.MessageFor(surface)),
                    $"{surface} is Locked but carries no reason, so pressing it stings without " +
                    "explaining. Either give it a message or make it Unavailable.");
            }
        }

        // ------------------------------------------------------------------
        // No path reaches the purchase confirmation modal

        // The ONLY two files allowed to open PurchaseConfirmationModal, and therefore the only two
        // that have to carry the gate. Adding a row here is the deliberate act.
        static readonly Dictionary<string, string> ModalOpeners = new()
        {
            ["UI/Elements/Buttons/PurchaseCard.cs"] = "the Store's purchase cards (all three kinds)",
            ["UI/Views/HangarCaptainsView.cs"] = "the Hangar's captain upgrade",
        };

        [Test]
        public void OnlyTheKnownPathsOpenThePurchaseModal()
        {
            var openers = new List<string>();
            foreach (var file in AllSources())
            {
                var name = Path.GetFileName(file);

                // This file NAMES the call in order to look for it. Excluding it by name rather than
                // by folder, so the exclusion cannot quietly widen to every test in the suite.
                if (name == nameof(CommerceDeScopeTests) + ".cs") continue;

                if (!File.ReadAllText(file).Contains("ConfirmationModal.ModalWindowIn()")) continue;
                openers.Add(name);
            }

            var expected = ModalOpeners.Keys.Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEquivalent(expected, openers.Distinct().OrderBy(n => n).ToArray(),
                "The set of files that open the purchase confirmation modal has changed. Every one " +
                "of them must carry the commerce gate, so a new opener is a new place the de-scope " +
                "can leak (Docs/STEAM_RELEASE_TASKS.md R4).");
        }

        [Test]
        public void EveryModalOpenerIsGated()
        {
            foreach (var (relativePath, why) in ModalOpeners.Select(kv => (kv.Key, kv.Value)))
            {
                var text = Source(relativePath);
                Assert.IsTrue(text.Contains("CommerceSurface.CatalogPurchase"),
                    $"{relativePath} ({why}) opens the purchase confirmation modal without asking " +
                    "the commerce posture first.");
            }
        }

        [Test]
        public void CheckoutChokePointIsGated()
        {
            var text = Source("System/IAPManager.cs");

            Assert.IsTrue(text.Contains("AllowRealMoneyCheckout"),
                "IAPManager does not consult the commerce posture. OpenCheckout is the choke point " +
                "both purchase entry points share - gating it there is what stops an un-gated or " +
                "re-enabled screen opening a payment page.");

            // One OpenURL CALL, and it sits after the gate. If a second appears the gate is no
            // longer a choke point, which is the only structural way this can regress.
            //
            // Matched with the opening paren on purpose: the class summary carries a
            // <see cref="Application.OpenURL"/>, and counting the bare name scores the prose.
            const string callSite = "Application.OpenURL(";
            Assert.AreEqual(1, CountOccurrences(text, callSite),
                "IAPManager has more than one Application.OpenURL call, so OpenCheckout is no longer " +
                "the single choke point the gate relies on.");
            Assert.Less(text.IndexOf("AllowRealMoneyCheckout", System.StringComparison.Ordinal),
                        text.IndexOf(callSite, System.StringComparison.Ordinal),
                "The checkout gate must precede Application.OpenURL.");
        }

        [Test]
        public void EpisodeCheckoutIsWiredOnlyWhenEpisodesAreAvailable()
        {
            var text = Source("UI/Screens/EpisodeScreen.cs");

            Assert.IsTrue(text.Contains("episode.priceUsd > 0f && episodesAvailable"),
                "An episode counts as purchasable on price ALONE. While episodes are de-scoped a " +
                "priced episode must render no price and wire no checkout listener - a price on " +
                "screen is the offer, whether or not the button works.");

            Assert.IsTrue(text.Contains("TryOpenEpisodes"),
                "EpisodeScreen no longer gates the panel opening, so the live cold-boot path " +
                "(Profile -> UnlockVesselButton -> ShowPanel) reaches the Support Us surface again.");
        }

        // ------------------------------------------------------------------
        // What must NOT have changed

        [Test]
        public void SoftCurrencyLoopIsUntouched()
        {
            // Crystals earned from match placement and spent on vessel unlocks are the live UGS
            // loop and are part of the invite build. They pass through none of the de-scoped
            // surfaces, and nothing here should ever start gating them.
            foreach (var relativePath in new[]
                     {
                         "System/VesselUnlock/VesselUnlockSystem.cs",
                         "UI/Scoreboard.cs",
                         "UI/Views/PlayerDataService.cs",
                     })
            {
                var text = Source(relativePath);
                Assert.IsFalse(text.Contains("CommerceSurface") || text.Contains("SO_CommerceAvailability"),
                    $"{relativePath} now consults the commerce de-scope. Crystal earning and vessel " +
                    "unlocking must keep working in the invite build - only the real-money and " +
                    "catalog purchase paths are de-scoped.");
            }
        }

        [Test]
        public void ThereIsExactlyOneLockedLook()
        {
            // The de-scope adds no presentation of its own: it says WHICH state a surface is in and
            // hands the state to MenuAvailabilityView, which is the one place a MenuAvailability
            // becomes pixels and a response.
            foreach (var relativePath in new[]
                     {
                         "ScriptableObjects/SO_CommerceAvailability.cs",
                         "UI/Elements/CommerceAffordance.cs",
                     })
            {
                var text = Source(relativePath);

                Assert.IsTrue(text.Contains("MenuAvailabilityView"),
                    $"{relativePath} does not route through MenuAvailabilityView.");

                foreach (var forbidden in new[] { "new Color(", "dimTint", "SetActive(", "MenuAudioCategory" })
                    Assert.IsFalse(text.Contains(forbidden),
                        $"{relativePath} contains '{forbidden}', which is presentation. A second " +
                        "implementation of the locked look is the thing this de-scope was required " +
                        "not to grow - extend MenuAvailabilityView instead.");
            }
        }

        [Test]
        public void NoCommerceSurfaceReportsItselfToAnalytics()
        {
            foreach (var relativePath in new[]
                     {
                         "UI/Screens/StoreScreen.cs",
                         "UI/Screens/EpisodeScreen.cs",
                         "UI/Modals/PurchaseConfirmationModal.cs",
                         "UI/Views/HangarCaptainsView.cs",
                         "UI/Elements/Buttons/PurchaseCard.cs",
                         "UI/Elements/Buttons/PurchaseItemCard.cs",
                         "System/IAPManager.cs",
                     })
            {
                var text = Source(relativePath);
                foreach (var marker in new[] { "AnalyticsService", "RecordEvent", "CustomEvent" })
                    Assert.IsFalse(text.Contains(marker),
                        $"{relativePath} reports to analytics. No event may claim a purchase surface " +
                        "was shown while the surfaces are de-scoped (Docs/STEAM_RELEASE_TASKS.md R4).");
            }
        }

        static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, index = 0;
            while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}
