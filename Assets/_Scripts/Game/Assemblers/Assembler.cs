using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public abstract class Assembler : MonoBehaviour
    {

        public abstract void StartBonding();
        public abstract int Depth { get; set; }

    }
}
