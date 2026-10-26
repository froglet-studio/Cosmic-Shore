using UnityEngine;

namespace CosmicShore.UI
{
	public class JustRotate : MonoBehaviour
	{
		public bool canRotate = true;
		[SerializeField] float speed = 10;
		public float Speed => speed;
        [SerializeField] Vector3 direction;
        public Vector3 Direction => direction;

        void Update()
		{
			if (canRotate)
			{
				if (direction == null)
					direction = Vector3.forward;
				
				transform.Rotate(speed * direction * Time.deltaTime);
			}
		}
	}
}