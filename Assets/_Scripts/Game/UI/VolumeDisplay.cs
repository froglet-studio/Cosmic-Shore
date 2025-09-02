using System;
using CosmicShore.SOAP;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(Image))]
    public class VolumeDisplay : MonoBehaviour
    {
        [SerializeField]
        MiniGameDataSO miniGameData; 

        [SerializeField] float upperBound = 300000;
        
        Vector4 colorRadii;
        Material material;
        
        bool allowUpdate;

        void Awake()
        {
            var imageComponent = GetComponent<Image>();
            material = new Material(imageComponent.material);
            imageComponent.material = material;
        }

        private void OnEnable()
        {
            miniGameData.OnMiniGameStart += OnMiniGameStart;
            miniGameData.OnMiniGameTurnEnd += OnMiniGameTurnEnd;
        }

        private void OnDisable()
        {
            miniGameData.OnMiniGameStart -= OnMiniGameStart;
            miniGameData.OnMiniGameTurnEnd -= OnMiniGameTurnEnd;
        }

        void OnMiniGameStart()
        {
            allowUpdate = true;
        }
        
        void OnMiniGameTurnEnd()
        {
            allowUpdate = false;
        }

        void Update()
        {
            if (!allowUpdate)
                return;
            
            var teamVolumes = miniGameData.GetTeamVolumes();
            colorRadii = new Vector4(
                teamVolumes.x / upperBound, 
                teamVolumes.y / upperBound, 
                teamVolumes.z / upperBound,
                teamVolumes.w / upperBound);
            
            UpdateUI();
            
            /*if (StatsManager.Instance != null)
            {
                var teamStats = StatsManager.Instance.TeamStats;
                var greenVolume = teamStats.ContainsKey(Teams.Jade) ? teamStats[Teams.Jade].VolumeRemaining : 0f;
                var redVolume = teamStats.ContainsKey(Teams.Ruby) ? teamStats[Teams.Ruby].VolumeRemaining : 0f;
                var goldVolume = teamStats.ContainsKey(Teams.Gold) ? teamStats[Teams.Gold].VolumeRemaining : 0f;
                AdjustRadii(greenVolume / upperBound, redVolume / upperBound, goldVolume / upperBound);
            }*/
        }

        void UpdateUI()
        {
            if (!material) return;
            material.SetFloat("_Radius1", colorRadii.x);
            material.SetFloat("_Radius2", colorRadii.y);
            material.SetFloat("_Radius3", colorRadii.w);
            
            /*material.SetFloat("_Radius1", Color1Radius);
            material.SetFloat("_Radius2", Color2Radius);
            material.SetFloat("_Radius3", Color3Radius);*/
        }

        // Call this function to update the radii dynamically
        /*void AdjustRadii(float r1, float r2, float r3)
        {
            Color1Radius = r1;
            Color2Radius = r2;
            Color3Radius = r3;
        }*/
    }
}