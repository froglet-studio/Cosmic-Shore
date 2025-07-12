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

        IShip ship;

        void Start()
        {
            ship = Player.Ship;
        }

        void Update()
        {
            Camera.transform.position = (-ship.Transform.forward * CameraRadius) + activeNode.transform.position;
            Camera.transform.LookAt(activeNode.transform.position, ship.Transform.up);
        }

        public void SetActiveNode(Cell node)
        {
            activeNode = node;
        }
    }
}