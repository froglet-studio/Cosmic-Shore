#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The arena roster contract: an arena card is a card the pilot can bring SEVERAL hulls to,
    /// and the launch window's carousel must offer every one of them, drawn as itself.
    ///
    /// <para>Two defects this holds shut, both shipped at once and reported as one ("the vessel
    /// select only let me pick the Squirrel and something that looked like the Sparrow, but was
    /// the Scarab"): the carousel filtered the card's <c>Vessels</c> by the HANGAR's purchase lock
    /// (six of eight class assets are authored locked, so two hulls survived), and the Scarab's
    /// <c>IconActive</c> was a codex bake byte-identical to the Sparrow's (the baker read the
    /// prefab asset, where the Scarab's wrapped Sparrow renderer is still enabled).</para>
    ///
    /// <para>The icon test compares FILE BYTES, not sprite identity - the two sprites were
    /// different assets with the same pixels, which is exactly the shape a reference check
    /// cannot see.</para>
    /// </summary>
    public class ArenaRosterTests
    {
        const string ArenaGamesPath = "Assets/_SO_Assets/Games/GameLists/ArenaGames.asset";
        const string ArcadeGamesPath = "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset";
        const string RuntimeGameDataPath = "Assets/_SO_Assets/Game Data/Runtime GameData.asset";
        const string ModalPath = "Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs";

        static List<SO_ArcadeGame> ArenaCards()
        {
            var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(ArenaGamesPath);
            Assert.IsNotNull(list, $"{ArenaGamesPath} is missing");
            Assert.IsNotNull(list.Games);
            Assert.IsNotEmpty(list.Games, "the arena roster is empty");
            return list.Games.Where(g => g).ToList();
        }

        [Test]
        public void RuntimeGameData_WiresThisRoster_SoTheStartingElementBaselineCanSkipArenaCards()
        {
            // GameDataSO.SyncFromArcadeGame asks THIS asset whether the launching card is an
            // arena one, and withholds the intensity-1 level-5 baseline when it is - an arena
            // grid is balanced BY its own StartingElements table, and two of the four cards
            // solve that spread by leaving their anchor hulls at rest. Unwired, the baseline is
            // withheld from every card in the game, which is a silent loss of the whole rule on
            // ~46 arcade cards. Nothing on a card separates the two sets, so the roster is the
            // only authority there is.
            var gameData = AssetDatabase.LoadAssetAtPath<GameDataSO>(RuntimeGameDataPath);
            Assert.IsNotNull(gameData, $"{RuntimeGameDataPath} is missing");
            Assert.IsNotNull(gameData.ArenaGames,
                $"{RuntimeGameDataPath}: ArenaGames is unwired - assign {ArenaGamesPath}");
            Assert.AreEqual(AssetDatabase.GetAssetPath(gameData.ArenaGames), ArenaGamesPath,
                "the runtime game data must read the SAME roster the Arena screen draws, or the " +
                "two can disagree about which cards are arena cards");
        }

        [Test]
        public void NoArenaCard_IsAlsoAnArcadeCard()
        {
            // The two rosters are what the baseline rule divides the fleet by, so a card in both
            // would be an arena card that the rule treats as arcade on whichever list is asked.
            var arcade = AssetDatabase.LoadAssetAtPath<SO_GameList>(ArcadeGamesPath);
            Assert.IsNotNull(arcade, $"{ArcadeGamesPath} is missing");
            foreach (var card in ArenaCards())
                Assert.IsFalse(arcade.Games != null && arcade.Games.Contains(card),
                    $"{card.name} is on BOTH the arena and arcade rosters");
        }

        [Test]
        public void EveryArenaCard_ListsSeveralHulls_EachWithBothIcons()
        {
            foreach (var card in ArenaCards())
            {
                Assert.IsNotNull(card.Vessels, $"{card.name}: Vessels is null");
                Assert.GreaterOrEqual(card.Vessels.Count, 2,
                    $"{card.name}: an arena card seats SEVERAL hulls; a one-hull card belongs in ArcadeGames");
                foreach (var vessel in card.Vessels)
                {
                    Assert.IsNotNull(vessel, $"{card.name}: a null entry in Vessels is a dead carousel slot");
                    Assert.IsNotNull(vessel.IconActive,
                        $"{card.name}: {vessel.name} has no IconActive - the carousel draws it as nothing");
                    Assert.IsNotNull(vessel.IconInactive,
                        $"{card.name}: {vessel.name} has no IconInactive");
                }
            }
        }

        [Test]
        public void EveryArenaCard_NoTwoHullsWearTheSameIconArt()
        {
            foreach (var card in ArenaCards())
            {
                var byHash = new Dictionary<string, List<string>>();
                foreach (var vessel in card.Vessels.Where(v => v && v.IconActive))
                {
                    string path = AssetDatabase.GetAssetPath(vessel.IconActive.texture);
                    Assert.IsFalse(string.IsNullOrEmpty(path), $"{vessel.name}: IconActive is not an asset");
                    string hash;
                    using (var md5 = MD5.Create())
                        hash = System.BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path)));
                    if (!byHash.TryGetValue(hash, out var owners)) byHash[hash] = owners = new List<string>();
                    owners.Add($"{vessel.name} ({path})");
                }
                foreach (var pair in byHash)
                    Assert.AreEqual(1, pair.Value.Count,
                        $"{card.name}: these hulls wear byte-identical icon art and cannot be told apart in the " +
                        $"carousel: {string.Join(", ", pair.Value)}");
            }
        }

        [Test]
        public void Carousel_OffersEveryHullTheCardLists_NeverTheHangarLock()
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ModalPath);
            Assert.IsTrue(File.Exists(path), $"{ModalPath} is missing");
            var source = File.ReadAllText(path);

            int start = source.IndexOf("void BuildAvailableShips(", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "BuildAvailableShips is gone - the carousel roster moved; move this test with it");
            int end = source.IndexOf("void InitializeDefaultShipFromAvailable(", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start);
            string body = source.Substring(start, end - start);

            Assert.IsFalse(body.Contains("IsLocked"),
                "BuildAvailableShips consults SO_Vessel.IsLocked. The hangar's purchase lock gates the HANGAR; " +
                "a card's Vessels list is the authority on what a mode admits (an arcade card already flies " +
                "its one hull whether or not the pilot bought it). Filtering here left two hulls in every " +
                "arena carousel.");
        }
    }
}
#endif
