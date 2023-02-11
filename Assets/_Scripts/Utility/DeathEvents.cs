using UnityEngine;

public class DeathEvents : MonoBehaviour
{
    public delegate void DeathBegin();
    public static event DeathBegin OnDeathBegin;

    private void OnEnable()
    {
        //TrailBlock.OnTrailCollision += Die;
    }
    private void OnDisable()
    {
        //TrailBlock.OnTrailCollision -= Die;
    }

    public void Die(string uuid, float fuelAmount)
    {
        OnDeathBegin?.Invoke();
    }
}