using UnityEngine;

public class Minimap : MonoBehaviour
{
    [SerializeField] Camera Camera;
    [SerializeField] Player Player;
    [SerializeField] float CameraRadius;
    [SerializeField] Node activeNode;
    // TODO: make node aware

    Ship ship;

    void Start()
    {
        ship = Player.Ship;
    }

    void Update()
    {
        Camera.transform.position = (-ship.transform.forward * CameraRadius) + activeNode.transform.position;
        Camera.transform.LookAt(activeNode.transform.position, ship.transform.up);
    }

    public void SetActiveNode(Node node)
    {
        activeNode = node;
    }
}
