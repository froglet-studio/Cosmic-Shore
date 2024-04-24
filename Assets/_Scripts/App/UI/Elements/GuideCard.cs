using CosmicShore.App.UI.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class GuideCard : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] Sprite LockIcon;
        [SerializeField] Sprite MassBackground;
        [SerializeField] Sprite ChargeBackground;
        [SerializeField] Sprite SpaceBackground;
        [SerializeField] Sprite TimeBackground;
        [SerializeField] Color ActiveBorderColor = new Color(1f, .5f, .25f);
        [SerializeField] Color InActiveBorderColor = Color.white;
        [SerializeField] public bool Locked;

        [Header("Placeholder Locations")]
        [FormerlySerializedAs("VesselName")]
        [SerializeField] TMP_Text GuideName;
        [SerializeField] Image BorderImage;
        [SerializeField] Image BackgroundImage;
        [FormerlySerializedAs("VesselImage")]
        [SerializeField] Image GuideImage;
        [SerializeField] Image LockImage;
        [SerializeField] public int Index;  // Serialized for debugging

        SO_Guide guide;
        public SO_Guide Guide
        {
            get { return guide; }
            set
            {
                guide = value;
                UpdateCardView();
            }
        }

        public SquadMenu SquadMenu;

        void Start()
        {
            UpdateCardView();
            GetComponent<Button>().onClick.RemoveAllListeners();
            GetComponent<Button>().onClick.AddListener(OnCardClicked);
        }

        void UpdateCardView()
        {
            if (guide == null) return;

            //SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == gameMode).FirstOrDefault();
            GuideName.text = guide.Name;
            GuideImage.sprite = guide.Image;
            LockImage.sprite = LockIcon;
            LockImage.gameObject.SetActive(Locked);

            switch (guide.PrimaryElement)
            {
                case Element.Mass:
                    BackgroundImage.sprite = MassBackground;
                    break;
                case Element.Charge:
                    BackgroundImage.sprite = ChargeBackground;
                    break;
                case Element.Space:

                    BackgroundImage.sprite = SpaceBackground;
                    break;
                case Element.Time:
                    BackgroundImage.sprite = TimeBackground;
                    break;
            }
        }

        public void Active(bool active = true)
        {
            BorderImage.color = active ? ActiveBorderColor : InActiveBorderColor;
        }

        public void OnCardClicked()
        {
            Debug.Log($"GuideCard - Clicked: Guide Name: {guide.Name}");

            if (SquadMenu != null)
            {
                SquadMenu.AssignGuide(this);
            }
        }

        public void OnUpgradeButtonClicked()
        {

        }
    }
}