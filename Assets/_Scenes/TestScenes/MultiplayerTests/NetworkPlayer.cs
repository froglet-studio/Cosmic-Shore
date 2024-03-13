using System;
using CosmicShore.Utility.ClassExtensions;
using Unity.Netcode;

namespace CosmicShore
{
    public class NetworkPlayer : NetworkBehaviour
    {
        private int _counter = 0;
        // Start is called before the first frame update
        private void Awake()
        {
            ++_counter;
            this.LogWithClassMethod("Awake", _counter.ToString());
        }

        private void OnEnable()
        {
            ++_counter;
            this.LogWithClassMethod("OnEnable", _counter.ToString());
        }

        private void OnValidate()
        {
            ++_counter;
            this.LogWithClassMethod("OnValidate", _counter.ToString());
        }

        void Start()
        {
            ++_counter;
            this.LogWithClassMethod("Start", _counter.ToString());
        }

        public override void OnNetworkSpawn()
        {
            ++_counter;
            this.LogWithClassMethod("OnNetworkSpawn", _counter.ToString());
        }

        public override void OnNetworkDespawn()
        {
            ++_counter;
            this.LogWithClassMethod("OnNetworkDespawn", _counter.ToString());
        }

        void OnDestory()
        {
            ++_counter;
            
        }

        void OnDisable()
        {
            ++_counter;
            this.LogWithClassMethod("OnDisable", _counter.ToString());
        }
    }
}
