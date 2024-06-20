using UnityEngine;

namespace CosmicShore.Integrations.Architectures.Observer
{
    public abstract class Observer : MonoBehaviour
    {
        public abstract void Notify(Subject subject);
    }
}