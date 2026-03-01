using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Vessel", menuName = "ScriptableObjects/Vessel/Vessel", order = 1)]
    [System.Serializable]
    public class SO_Ship : ScriptableObject
    {
        [SerializeField] public VesselClassType Class;
        [SerializeField] public string Name;
        [SerializeField] public string Description;
        [FormerlySerializedAs("SelectedIcon")]
        [SerializeField] public Sprite IconActive;
        [FormerlySerializedAs("Icon")]
        [SerializeField] public Sprite IconInactive;
        [SerializeField] public Sprite PreviewImage;
        [SerializeField] public Sprite SquadImage;
        [SerializeField] public Sprite TrailPreviewImage;
        [SerializeField] public Sprite CardSilohoutteActive;
        [FormerlySerializedAs("CardSilohoutte")]
        [SerializeField] public Sprite CardSilohoutteInactive;
        [SerializeField] public List<SO_ShipAbility> Abilities;
        [SerializeField] public List<SO_Captain> Captains;
        [FormerlySerializedAs("TrainingGames")]
        [SerializeField] public List<SO_ArcadeGame> Games;
        [SerializeField] public List<SO_TrainingGame> TrainingGames;
        [SerializeField] public GameplayParameter gameplayParameter1 = new GameplayParameter("Casual", "Challenging", .5f);
        [SerializeField] public GameplayParameter gameplayParameter2 = new GameplayParameter("Relaxing", "Thrilling", .5f);
        [SerializeField] public GameplayParameter gameplayParameter3 = new GameplayParameter("Solo", "Social", .5f);

        /// <summary>
        /// A flag indicating whether the Vessel Class is locked.
        /// Set externally by CaptainManager when ship unlock state changes.
        /// </summary>
        [NonSerialized] public bool IsLocked = true;
    }

    [System.Serializable]
    public struct GameplayParameter
    {
        public string LeftHandLabel;
        public string RightHandLabel;
        [Range(0,1)]
        public float Value;

        public GameplayParameter(string leftHandLabel, string rightHandLabel, float value)
        {
            LeftHandLabel = leftHandLabel;
            RightHandLabel = rightHandLabel;
            Value = value;
        }
    }
}
