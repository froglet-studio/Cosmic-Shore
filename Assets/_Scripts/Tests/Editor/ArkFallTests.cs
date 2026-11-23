using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// An Ark's fall (Docs/Pilots/ARTIFACTS.md §5, §8.4): who earns the hold, the one-step transfer,
    /// the spill when nobody earned it, and loose artifacts anyone can take. Felling a live Ark in
    /// the Arkway is a hand test (Docs/UNITY_VERIFICATION_CHECKLIST.md).
    /// </summary>
    public class ArkFallTests
    {
        readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            ArtifactPickup.StrikeAll();
            foreach (var go in _spawned) if (go) Object.DestroyImmediate(go);
            _spawned.Clear();
            ArkHold.ClearRegistryForTests();
        }

        Ark MakeArk(Domains domain, Vector3 at)
        {
            var ark = Ark.Create(null, at, Quaternion.identity);
            _spawned.Add(ark.gameObject);
            ark.SetDomainForTests(domain);
            return ark;
        }

        static Domains? Lookup(string name) => name switch
        {
            "raider" => Domains.Ruby,
            "crewmate" => Domains.Jade,
            _ => null,
        };

        [Test]
        public void ARivalForceEarnsTheFall()
        {
            Assert.AreEqual(Domains.Ruby, ArkFall.Credit("raider", false, Domains.Jade, Lookup));
        }

        [Test]
        public void FaunaAnUnknownForceAndTheOwnCrewEarnNothing()
        {
            Assert.IsNull(ArkFall.Credit("raider", byCreature: true, Domains.Jade, Lookup), "a creature kill credits nobody");
            Assert.IsNull(ArkFall.Credit(null, false, Domains.Jade, Lookup));
            Assert.IsNull(ArkFall.Credit("stranger", false, Domains.Jade, Lookup), "a force whose side is unknown");
            Assert.IsNull(ArkFall.Credit("crewmate", false, Domains.Jade, Lookup), "the Ark's own squad is not a thief");
        }

        [Test]
        public void TheHoldStowsAndGivesUpItsArtifacts()
        {
            var ark = MakeArk(Domains.Jade, Vector3.zero);
            Assert.IsTrue(ark.Hold.Stow("Javelin"));
            Assert.IsTrue(ark.Hold.Stow("Aegis"));
            Assert.IsFalse(ark.Hold.Stow(""), "an empty id is no artifact");
            var into = new List<string>();
            Assert.AreEqual(2, ark.Hold.TakeAll(into));
            CollectionAssert.AreEqual(new[] { "Javelin", "Aegis" }, into);
            Assert.AreEqual(0, ark.Hold.Artifacts.Count);
        }

        [Test]
        public void AnEmptyHoldSettlesToNothing()
        {
            var ark = MakeArk(Domains.Jade, Vector3.zero);
            Assert.AreEqual(ArkFall.Verdict.Nothing, ArkFall.Settle(ark.Hold).Verdict);
            Assert.AreEqual(0, ArtifactPickup.Count);
        }

        [Test]
        public void AFallNobodyEarnedSpillsEveryArtifact()
        {
            // No hull prism was destroyed by anyone, so nobody is credited.
            var ark = MakeArk(Domains.Jade, new Vector3(0, 0, 500));
            ark.Hold.Stow("Javelin");
            ark.Hold.Stow("Aegis");
            var outcome = ArkFall.Settle(ark.Hold);
            Assert.AreEqual(ArkFall.Verdict.Spill, outcome.Verdict);
            Assert.AreEqual(2, outcome.Count);
            Assert.AreEqual(2, ArtifactPickup.Count);
            Assert.AreEqual(0, ark.Hold.Artifacts.Count, "the hold is emptied in the same step");
            Assert.IsNotNull(ArtifactPickup.FindNear(ark.Position, 200f));
        }

        [Test]
        public void TheRaidersArkIsFoundWhereverItIs()
        {
            var home = MakeArk(Domains.Jade, Vector3.zero);
            var raiders = MakeArk(Domains.Ruby, new Vector3(5000, 0, 0));
            Assert.AreSame(raiders.Hold, ArkHold.FindHome(Domains.Ruby, except: home.Hold));
            Assert.IsNull(ArkHold.FindHome(Domains.Gold), "a squad with no Ark here receives nothing");
        }

        [Test]
        public void SpilledArtifactsLandApartOnTheSpillSphere()
        {
            const int n = 6;
            var points = Enumerable.Range(0, n).Select(i => ArkFall.SpillOffset(i, n, 40f)).ToArray();
            foreach (var p in points) Assert.AreEqual(40f, p.magnitude, 0.01f);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    Assert.Greater((points[i] - points[j]).magnitude, 5f, "two spilled artifacts must not land together");
        }

        [Test]
        public void ALooseArtifactHasOneHolderAtATime()
        {
            var pickup = ArtifactPickup.Spill("Javelin", Vector3.zero);
            var a = new GameObject("carrier a").AddComponent<PayloadCarrier>();
            var b = new GameObject("carrier b").AddComponent<PayloadCarrier>();
            _spawned.Add(a.gameObject);
            _spawned.Add(b.gameObject);

            Assert.IsTrue(pickup.TryClaim(a));
            Assert.IsFalse(pickup.TryClaim(b), "a carried artifact cannot be taken from its carrier");
            Assert.IsNull(ArtifactPickup.FindNear(Vector3.zero, 10f), "a carried artifact is not loose");
            pickup.Drop();
            Assert.IsTrue(pickup.TryClaim(b), "let go, it is anyone's again");
        }

        [Test]
        public void ALooseArtifactIsOnlyFoundInReach()
        {
            ArtifactPickup.Spill("Javelin", new Vector3(100, 0, 0));
            Assert.IsNull(ArtifactPickup.FindNear(Vector3.zero, 50f));
            Assert.IsNotNull(ArtifactPickup.FindNear(Vector3.zero, 150f));
            Assert.IsNull(ArtifactPickup.Spill("", Vector3.zero), "an empty id spills nothing");
        }

        [Test]
        public void UncollectedArtifactsAreLostWhenTheInstanceEnds()
        {
            ArtifactPickup.Spill("Javelin", Vector3.zero);
            ArtifactPickup.Spill("Aegis", Vector3.one);
            Assert.AreEqual(2, ArtifactPickup.StrikeAll());
            Assert.AreEqual(0, ArtifactPickup.Count);
        }

        [Test]
        public void ThePickupIsCutFacetsFacingOut()
        {
            Assert.IsTrue(ArtifactPickup.FacesPointOutward());
        }
    }
}
