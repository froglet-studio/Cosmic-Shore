#if UNITY_EDITOR
using System.IO;
using CosmicShore.Core;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A fresh profile (no <c>squad.data</c>) with no default captains - the shipped state since the captain system
    /// was removed from vessels, because <c>PortSquadView</c>'s captain list is empty - loads an EMPTY squad. Building the
    /// default squad from null captains threw a NullReferenceException on every fresh profile's Menu_Main.
    /// </summary>
    public class SquadSystemTests
    {
        static string SquadPath => Application.persistentDataPath + "/squad.data";

        [Test]
        public void FreshProfile_WithNoCaptains_LoadsAnEmptySquad_WithoutThrowing()
        {
            byte[] saved = File.Exists(SquadPath) ? File.ReadAllBytes(SquadPath) : null;
            var leader = SquadSystem.DefaultLeader;
            var rogueOne = SquadSystem.DefaultRogueOne;
            var rogueTwo = SquadSystem.DefaultRogueTwo;
            var captains = SquadSystem.CaptainList;
            try
            {
                if (saved != null) File.Delete(SquadPath);
                SquadSystem.DefaultLeader = SquadSystem.DefaultRogueOne = SquadSystem.DefaultRogueTwo = null;
                SquadSystem.CaptainList = null;

                Assert.DoesNotThrow(SquadSystem.Init, "a fresh profile with no captains");
                Assert.DoesNotThrow(() => SquadSystem.LoadSquad());
                Assert.IsNull(SquadSystem.SquadLeader);
                Assert.IsNull(SquadSystem.RogueOne);
                Assert.IsNull(SquadSystem.RogueTwo);
                Assert.IsFalse(File.Exists(SquadPath), "a squad of null captains must not be saved");
            }
            finally
            {
                SquadSystem.DefaultLeader = leader;
                SquadSystem.DefaultRogueOne = rogueOne;
                SquadSystem.DefaultRogueTwo = rogueTwo;
                SquadSystem.CaptainList = captains;
                if (saved != null) File.WriteAllBytes(SquadPath, saved);
                else if (File.Exists(SquadPath)) File.Delete(SquadPath);
            }
        }
    }
}
#endif
