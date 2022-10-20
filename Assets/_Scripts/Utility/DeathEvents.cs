using UnityEngine;

public class DeathEvents : MonoBehaviour
{

    public delegate void DeathBegin();
    public static event DeathBegin OnDeathBegin;


    private void OnEnable()
    {
        Trail.OnTrailCollision += Die;
    }

    private void OnDisable()
    {
        Trail.OnTrailCollision -= Die;
    }

    void Die(string uuid, float fuelAmount)
    {
        OnDeathBegin?.Invoke();
    }

}
