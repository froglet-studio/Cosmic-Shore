using System.Collections;
using UnityEngine;

public enum Team
{
    None,
    Green,
    Red,
}

public enum EntityType
{
    Ship,
    TrailBlock,
    Explosion,
    Skimmer,
    Crystal,
}

public interface IEntity
{
    Team Team { get; set; }
    EntityType EntityType { get; }
}


// TODO: namespace
// TODO: add IBlockImpact interface
public class AOEExplosion : MonoBehaviour, IEntity
{
    [SerializeField] float MaxScale = 200f;
    [SerializeField] float ExplosionDuration = 2f;
    [SerializeField] float ExplosionDelay = .2f;
    public float speed = .5f;
    [SerializeField] GameObject Geometry;

    Team team;
    EntityType entityType = EntityType.Explosion;
    public Team Team { get => team; set => team = value; }
    public EntityType EntityType { get => entityType; }

    void Start()
    {
        StartCoroutine(ExplodeCoroutine());
    }

    IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);
        // TODO: leverage explosion duration to derive the scale increment factor
        // TODO: apply some kind of easing
        //while (MaxScale > Geometry.transform.localScale.x)
        while (MaxScale > transform.localScale.x)
        {
            transform.localScale += new Vector3(speed, speed, speed);
            yield return null;
        }

        transform.localScale = new Vector3(.1f, .1f, .1f);

        Destroy(gameObject);
    }
}