using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class Minimap : MonoBehaviour
    {
        [SerializeField] Camera Camera;
        [SerializeField] Player Player;
        [SerializeField] float CameraRadius;
        [SerializeField] Cell activeNode;

        IVessel vessel;

        void Start()
        {
            vessel = Player.Vessel;
        }

        void Update()
        {
            Camera.transform.position = (-vessel.Transform.forward * CameraRadius) + activeNode.transform.position;
            Camera.transform.LookAt(activeNode.transform.position, vessel.Transform.up);
        }

        public void SetActiveNode(Cell node)
        {
            activeNode = node;
        }
    }
}