using System.Linq;
using CosmicShore.Core;
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
        
        public float Color1Radius = 0.5f;
        public float Color2Radius = 0.5f;
        public float Color3Radius = 0.5f;
        [SerializeField] float upperBound = 300000;

        Material material;

        void Awake()
        {
            var imageComponent = GetComponent<Image>();
            material = new Material(imageComponent.material);
            imageComponent.material = material;
        }

        void Update()
        {
            // Use MiniGameData to get data about Volume Remaining.
            var roundStats = miniGameData.GetSortedListInDecendingOrderBasedOnVolumeRemaining();

            float Vol(Teams t) => roundStats.FirstOrDefault(rs => rs.Team == t)?.VolumeRemaining ?? 0f;

            float greenVolume = Vol(Teams.Jade);
            float redVolume   = Vol(Teams.Ruby);
            float goldVolume  = Vol(Teams.Gold);

            AdjustRadii(greenVolume / upperBound, redVolume / upperBound, goldVolume / upperBound);


            /*if (StatsManager.Instance != null)
            {
                var teamStats = StatsManager.Instance.TeamStats;
                var greenVolume = teamStats.ContainsKey(Teams.Jade) ? teamStats[Teams.Jade].VolumeRemaining : 0f;
                var redVolume = teamStats.ContainsKey(Teams.Ruby) ? teamStats[Teams.Ruby].VolumeRemaining : 0f;
                var goldVolume = teamStats.ContainsKey(Teams.Gold) ? teamStats[Teams.Gold].VolumeRemaining : 0f;
                AdjustRadii(greenVolume / upperBound, redVolume / upperBound, goldVolume / upperBound);
            }*/
        }

        public void UpdateUI()
        {
            if (material)
            {
                material.SetFloat("_Radius1", Color1Radius);
                material.SetFloat("_Radius2", Color2Radius);
                material.SetFloat("_Radius3", Color3Radius);
            }
        }

        // Call this function to update the radii dynamically
        public void AdjustRadii(float r1, float r2, float r3)
        {
            Color1Radius = r1;
            Color2Radius = r2;
            Color3Radius = r3;
            UpdateUI();
        }
    }
}