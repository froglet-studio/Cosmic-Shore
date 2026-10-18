#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Every crew vessel keeps the one notion of a crew vessel (.claude/skills/duo-vessel/SKILL.md, "One notion of a crew
    /// vessel"): one hull class that carries ONE seat (an <see cref="ICrewSeat"/>), a card that names both subvessels
    /// (SO_Vessel.CrewNames joined by CrewNameTemplate) while Name stays the one-word key, and a place on the Vessel
    /// Changer's roster. Tools/Build/check_crew_vessels.py holds the same from the files, with negative controls.
    /// </summary>
    public class CrewVesselTests
    {
        [TestCase(VesselClassType.Goby, "Goby", "Goby and Pistol", "Pistol")]
        [TestCase(VesselClassType.Wrasse, "Wrasse", "Wrasse and Grouper", "Wrasse")]
        public void CrewHull_CarriesOneSeat_AndItsCardNamesBoth(VesselClassType type, string hull, string card, string seat)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Prefabs/Spacevessels/{hull}.prefab");
            Assert.IsNotNull(prefab, $"no {hull}.prefab");
            var seats = prefab.GetComponents<ICrewSeat>();
            Assert.AreEqual(1, seats.Length, $"{hull}: a crew hull carries exactly one seat");
            Assert.AreEqual(seat, seats[0].SeatName);
            Assert.IsTrue(prefab.TryGetComponent(out IVesselStatus status));
            Assert.AreEqual(type, status.VesselType);

            var cls = AssetDatabase.LoadAssetAtPath<SO_Vessel>($"Assets/_SO_Assets/Classes/SO_Class_{hull}.asset");
            Assert.IsNotNull(cls);
            Assert.AreEqual(hull, cls.Name, "Name stays the vessel's key (the hangar's cloud unlocks)");
            Assert.AreEqual(card, cls.CardName, "the card names both subvessels");
            CollectionAssert.Contains(cls.CrewNames, seats[0].SeatName, "the seat is one of the subvessels its card names");
            CollectionAssert.Contains(ToyVesselRoster.Default, type, "the Vessel Changer offers only the roster");
        }
    }
}
#endif
