using System.Collections;
using UnityEngine;

public class Gun : MonoBehaviour
{
    [SerializeField] GameObject projectilePrefab;

    public float speed = 10;
    public float projectileTime = 5;
    public float firePeriod = .2f;
    public Teams Team;
    bool onCooldown = false;
 
    public void FireGun()
    {
        if (onCooldown)
            return;
        
        onCooldown = true;

        var projectile = Instantiate(projectilePrefab);
        projectile.transform.rotation = Quaternion.LookRotation(transform.up);
        projectile.transform.position = transform.position + projectile.transform.forward * 2;
        projectile.transform.parent = transform;
        projectile.GetComponent<Projectile>().Velocity = projectile.transform.forward * speed;
        projectile.GetComponent<Projectile>().Team = Team;

        StartCoroutine(MoveProjectileCoroutine(projectile));
        StartCoroutine(CooldownCoroutine());
    }
    IEnumerator CooldownCoroutine()
    {
        yield return new WaitForSeconds(firePeriod);
        onCooldown = false;
    }

    IEnumerator MoveProjectileCoroutine(GameObject projectile)
    {
        var elapsedTime = 0f;
        while (elapsedTime < projectileTime)
        {
            elapsedTime += Time.deltaTime;
            projectile.transform.position += projectile.transform.forward * speed * Time.deltaTime;
            yield return null;
        }

        Destroy(projectile);
    }
}