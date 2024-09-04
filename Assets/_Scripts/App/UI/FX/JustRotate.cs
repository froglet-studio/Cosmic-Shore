using UnityEngine;

namespace CosmicShore.App.UI.FX
{
	public class JustRotate : MonoBehaviour
	{
		public bool canRotate = true;
		public float speed = 10;
        public Vector3 direction;

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