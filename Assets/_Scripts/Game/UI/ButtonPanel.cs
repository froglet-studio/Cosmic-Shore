using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class ButtonPanel : MonoBehaviour
    {
        public bool onBottomEdge = false;
        [SerializeField] List<Vector2> buttonPositions = new List<Vector2>();
        [SerializeField] List<Vector2> bottomEdgeButtonPositions = new List<Vector2>();
        [SerializeField] List<GameObject> buttons = new List<GameObject>();

        // Start is called before the first frame update
        void Start()
        {
            PositionButtons(onBottomEdge);
        }

        public void PositionButtons(bool bottomEdge)
        {
            if (bottomEdge)
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    buttons[i].GetComponent<RectTransform>().anchoredPosition = bottomEdgeButtonPositions[i];
                }
            }
            else
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    buttons[i].GetComponent<RectTransform>().anchoredPosition = buttonPositions[i];
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
