using UnityEngine;
using UnityEngine.UI;

namespace Amoebius.Core.Input
{
    public class Gyro : MonoBehaviour
    {
        //[System.Serializable]
        public Transform gyroTransform;

        Quaternion displacementQ = Quaternion.identity;

        private bool useGryo = true;





        // Start is called before the first frame update
        void Start()
        {

            if (SystemInfo.supportsGyroscope)
            {

                UnityEngine.Input.gyro.enabled = true;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (SystemInfo.supportsGyroscope && useGryo)
            {
                //updates GameObjects rotation from input devices gyroscope
                gyroTransform.rotation = GyroToUnity(UnityEngine.Input.gyro.attitude * Quaternion.Inverse(displacementQ));
                
            }
        }

        //Coverts Android and Mobile Device Quaterion into Unity Quaterion  TODO: Test
        private Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, q.y, -q.z, -q.w);
        }

        public void FlipUseGyro()
        {
            //TODO check if isLocalPlayer in multiplayer
            useGryo = Utils.Flip(useGryo);
        }

        public void SetGyroHome()
        {
            displacementQ = UnityEngine.Input.gyro.attitude;
        }
    }
}

