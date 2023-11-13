using UnityEngine;

namespace CosmicShore.App.UI
{
	public class JustRotate : MonoBehaviour
	{
		public bool canRotate = true;
		public float speed = 10;

		void Update()
		{
			if (canRotate)
				transform.Rotate(speed * Vector3.forward * Time.deltaTime);
		}
	}
}