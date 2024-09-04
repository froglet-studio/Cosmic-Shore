using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class PlayerCountButton : MonoBehaviour
    {
        [SerializeField] Image BorderImage;
        [SerializeField] Image MeepleImage;
        [SerializeField] Sprite BorderSpriteSelected;
        [SerializeField] Sprite BorderSpriteUnselected;
        [SerializeField] List<Sprite> SelectedSprites;
        [SerializeField] List<Sprite> UnselectedSprites;

        [SerializeField] Color32 ColorActive;
        [SerializeField] Color32 ColorInactive;

        Sprite PlayerCountSpriteActive;
        Sprite PlayerCountpriteInactive;
        public int Count { get; private set;  }

        public delegate void SelectDelegate(int playerCount);
        public event SelectDelegate OnSelect;

        public void Select()
        {
            OnSelect?.Invoke(Count);
        }

        public void SetPlayerCount(int count)
        {
            Count = count;
            PlayerCountSpriteActive = SelectedSprites[count-1];
            PlayerCountpriteInactive = UnselectedSprites[count-1];
        }

        public void SetSelected(bool selected)
        {
            if (selected)
            {
                BorderImage.sprite = BorderSpriteSelected;
                MeepleImage.sprite = PlayerCountSpriteActive;
            }
            else
            {
                BorderImage.sprite = BorderSpriteUnselected;
                MeepleImage.sprite = PlayerCountpriteInactive;
            }
        }

        public void SetActive(bool active)
        {
            GetComponent<Button>().enabled = active;

            if (active)
            {
                BorderImage.color = ColorActive;
                MeepleImage.color = ColorActive;    
            }
            else
            {
                BorderImage.color = ColorInactive;
                MeepleImage.color = ColorInactive;
            }
        }
    }
}