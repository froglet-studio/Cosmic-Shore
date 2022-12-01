using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fire : MonoBehaviour
{
    [SerializeField] GameObject projectilePrefab;
    public float speed = 10;
    public float projectileTime = 5;
    public float firePeriod = .2f;
    bool onCooldown = false;
    // Start is called before the first frame update
    private void OnEnable()
    {
        AIGunner.OnFire += FireGun;
    }

    private void OnDisable()
    {
        AIGunner.OnFire -= FireGun;
    }

    private void FireGun()
    {
        if (onCooldown)
        {
            return;
        }
        else
        {
            var projectile = Instantiate(projectilePrefab);
            projectile.transform.rotation = Quaternion.LookRotation(transform.up);
            projectile.transform.position = transform.position + projectile.transform.forward * 2;
            //projectile.transform.localScale = Vector3.one * .3f;
            StartCoroutine(MoveProjectileCoroutine(projectile));
            onCooldown = true;
            StartCoroutine(CooldownCoroutine());
        }


    }

    private IEnumerator CooldownCoroutine()
    {
        yield return new WaitForSeconds(firePeriod);
        onCooldown = false;
    }

    private IEnumerator MoveProjectileCoroutine(GameObject projectile)
    {
        var time = 0f;
        while (time < projectileTime)
        {
            time += Time.deltaTime;
            yield return null;
            projectile.transform.position += projectile.transform.forward * speed * Time.deltaTime;
        }
        Destroy(projectile);
    }
}
