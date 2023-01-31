using System.Collections;
using System.ComponentModel;
using UnityEngine;

namespace StarWriter.Core
{
    public class AOEExplosion : MonoBehaviour
    {
        public float speed = 5f; // TODO: use the easing of the explosion to change this over time
        protected const float PI_OVER_TWO = Mathf.PI / 2;
        protected Vector3 MaxScaleVector;

        [SerializeField] public float MaxScale = 200f;
        [SerializeField] protected float ExplosionDuration = 2f;
        [SerializeField] protected float ExplosionDelay = .2f;
        Quaternion containerRotation;
        Vector3 containerPosition;
        protected GameObject container;

        Material material;
        Teams team;
        Ship ship;
        [HideInInspector] public Material Material { get { return material; } set { material = new Material(value); } }
        [HideInInspector] public Teams Team { get => team; set => team = value; }
        [HideInInspector] public Ship Ship { get => ship; set => ship = value; }

        protected virtual void Start()
        {
            container = new GameObject();
            container.name = "AOEContainer";
            container.transform.SetPositionAndRotation(containerPosition, containerRotation);
            container.transform.parent = transform.parent;
            transform.SetParent(container.transform, false); // SetParent with false to take container's world position
            MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);
            StartCoroutine(ExplodeCoroutine());
        }

        protected virtual IEnumerator ExplodeCoroutine()
        {
            yield return new WaitForSeconds(ExplosionDelay);

            if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                meshRenderer.material = material;

            var elapsedTime = 0f;
            while (elapsedTime < ExplosionDuration)
            {
                elapsedTime += Time.deltaTime;
                container.transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO));
                material.SetFloat("_Opacity", Mathf.Clamp((MaxScaleVector - container.transform.localScale).magnitude / MaxScaleVector.magnitude, 0, 1));
                yield return null;
            }

            Destroy(container.gameObject);
        }

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            containerRotation = rotation;
            containerPosition = position;
        }
    }
}