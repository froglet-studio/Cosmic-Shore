using System.Collections.Generic;
using System.IO;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The plain payload (Docs/Pilots/ARTIFACTS.md §2): the shared super-shield crack, the parcel's
    /// seats, the hold's registry, and the button's honest name. The carry and the burst in a live
    /// Arkway are hand tests (Docs/UNITY_VERIFICATION_CHECKLIST.md).
    /// </summary>
    public class PayloadTests
    {
        readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go) Object.DestroyImmediate(go);
            _spawned.Clear();
            VesselSilence.ClearAll();
            ArkHold.ClearRegistryForTests();
        }

        // A bare prism: no team manager, so it reads as Blue mass.
        Prism MakePrism(bool super = false, bool shielded = false)
        {
            var go = new GameObject("payload test prism");
            _spawned.Add(go);
            var prism = go.AddComponent<Prism>();
            prism.prismProperties = new PrismProperties { IsSuperShielded = super, IsShielded = shielded };
            return prism;
        }

        [Test]
        public void TheCrackTakesASuperShieldOffARivalPrism()
        {
            var prism = MakePrism(super: true);
            Assert.AreEqual(SuperShieldCrack.Outcome.CrackedSuperShield,
                SuperShieldCrack.TryCrack(prism, "carrier", Domains.Jade, alsoShielded: true, leaveRegularShield: true));
        }

        [Test]
        public void ThePayloadCracksOrdinaryShieldsAndTheSniperDoesNot()
        {
            var prism = MakePrism(shielded: true);
            Assert.AreEqual(SuperShieldCrack.Outcome.CrackedShield,
                SuperShieldCrack.TryCrack(prism, "carrier", Domains.Jade, alsoShielded: true, leaveRegularShield: true));
            Assert.AreEqual(SuperShieldCrack.Outcome.Untouched,
                SuperShieldCrack.TryCrack(prism, "sniper", null, alsoShielded: false, leaveRegularShield: true));
        }

        [Test]
        public void PlainMassAndOwnMassAreNeverCracked()
        {
            Assert.AreEqual(SuperShieldCrack.Outcome.Untouched,
                SuperShieldCrack.TryCrack(MakePrism(), "carrier", Domains.Jade, true, true), "plain mass");
            Assert.AreEqual(SuperShieldCrack.Outcome.Untouched,
                SuperShieldCrack.TryCrack(MakePrism(super: true), "carrier", Domains.Blue, true, true),
                "the carrier's own domain's citadel");
        }

        [Test]
        public void ASilencedCarriersPayloadIsHarmless()
        {
            VesselSilence.Record("carrier", Domains.Jade, 10f);
            Assert.AreEqual(SuperShieldCrack.Outcome.Deflected,
                SuperShieldCrack.TryCrack(MakePrism(super: true), "carrier", Domains.Jade, true, true));
            Assert.AreEqual(SuperShieldCrack.Outcome.Deflected,
                SuperShieldCrack.TryCrack(MakePrism(shielded: true), "carrier", Domains.Jade, true, true));
        }

        [Test]
        public void TheParcelSeatsAreOneBundle()
        {
            Assert.AreEqual(Vector3.zero, PayloadCarrier.SeatOffset(0, 6, 1.6f), "the first prism sits at the centre");
            var seen = new HashSet<Vector3>();
            for (int i = 1; i < 6; i++)
            {
                var seat = PayloadCarrier.SeatOffset(i, 6, 1.6f);
                Assert.AreEqual(1.6f, seat.magnitude, 1e-4f, "every other prism rings the centre at the spacing");
                Assert.IsTrue(seen.Add(seat), "no two prisms share a seat");
            }
            Assert.AreEqual(Vector3.zero, PayloadCarrier.SeatOffset(0, 1, 1.6f));
        }

        [Test]
        public void ASquadFindsOnlyItsOwnArk()
        {
            var ark = Ark.Create(null, Vector3.zero, Quaternion.identity);
            _spawned.Add(ark.gameObject);
            // An Ark with no hull laid wears the default domain; no squad of another domain finds it.
            Assert.IsNotNull(ark.Hold, "every Ark is created with its hold");
            Assert.IsNull(ArkHold.FindFor(ark.Domain == Domains.Ruby ? Domains.Gold : Domains.Ruby, Vector3.zero));
        }

        [Test]
        public void TheButtonHasItsOwnHonestValue()
        {
            Assert.AreEqual(15, (int)InputEvents.PayloadAction, "a fixed value; Unity serializes the number");
            Assert.AreEqual(HintBinding.PadRightShoulder,
                CosmicShore.UI.InputHintBindingMap.BindingFor(InputEvents.PayloadAction, keyboard: false),
                "RB's chip names the payload, not the legacy FlipAction");
            Assert.AreEqual(HintBinding.KeyE,
                CosmicShore.UI.InputHintBindingMap.BindingFor(InputEvents.PayloadAction, keyboard: true));
        }

        [Test]
        public void ThePayloadConfigShipsEveryLineAuthoredAndEverySoundEmpty()
        {
            var cfg = Resources.Load<CosmicShore.ScriptableObjects.PayloadConfigSO>("PayloadConfig");
            Assert.IsNotNull(cfg, "Resources/PayloadConfig is written by Tools/Build/author_payload_assets.py");
            Assert.IsNotEmpty(cfg.BurstEffects, "a plain payload with no effect would crack nothing");
            Assert.IsTrue(cfg.GrabSound.IsNull && cfg.ReleaseSound.IsNull && cfg.RefusedSound.IsNull,
                "payload sounds ship empty (the FMOD rule)");
            Assert.IsTrue(File.Exists("Tools/Build/author_payload_assets.py"));
        }
    }
}
