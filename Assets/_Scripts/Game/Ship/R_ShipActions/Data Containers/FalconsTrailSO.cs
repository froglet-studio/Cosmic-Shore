using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "FalconTrail", menuName = "ScriptableObjects/Vessel Actions/Falcon Trail")]
    public class FalconsTrailSO : ShipActionExecutorBase
    {


       void StartAction(ActionExecutorRegistry registry)
        {
            Debug.Log("Trail Started");


        }
        void StopAction(ActionExecutorRegistry execs)
        {
            
        }




    }
}
