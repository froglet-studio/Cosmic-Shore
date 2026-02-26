using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Models.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "SO_ConnectingPanelSpriteList",
        menuName = "ScriptableObjects/UI/ConnectingPanelSpriteList")]
    public class SO_ConnectingPanelSpriteList : ScriptableObject
    {
        [SerializeField] private List<Sprite> sprites = new();

        public Sprite GetRandomSprite()
        {
            if (sprites == null || sprites.Count == 0)
                return null;

            return sprites[Random.Range(0, sprites.Count)];
        }
    }
}
