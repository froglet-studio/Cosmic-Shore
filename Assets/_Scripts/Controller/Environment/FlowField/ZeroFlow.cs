
namespace CosmicShore.Gameplay
{
    ﻿using UnityEngine;

    [CreateAssetMenu(fileName = "ZeroFlowData", menuName = "ScriptableObjects/Flow/ZeroFlow", order = 30)]
    [System.Serializable] 
    public class ZeroFlow : FlowFieldSO
    {
        public ZeroFlow()
        {
            fieldThickness = 200;
            fieldWidth = 200;
            fieldHeight = 700;
            fieldMax = .7f;
        }

        override public Vector3 FlowVector(Transform node)
        {
            return Vector3.zero;
        }
    }
}
