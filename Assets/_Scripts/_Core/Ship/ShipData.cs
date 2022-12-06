using UnityEngine;

[System.Serializable]
public class ShipData : MonoBehaviour
{
    public float inputSpeed = 1;
    public float speedMultiplier = 1;
    public float speed;

    public bool boost;
    public Vector3 velocityDirection;
    public Quaternion blockRotation;

    

    private void Update()
    {
        speed = inputSpeed * speedMultiplier;
    }
}