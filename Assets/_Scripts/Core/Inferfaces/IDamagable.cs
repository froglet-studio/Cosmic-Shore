using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamagable
{
    void TakeDamage(float amount);
    void Respawn(Vector3 point);
}
