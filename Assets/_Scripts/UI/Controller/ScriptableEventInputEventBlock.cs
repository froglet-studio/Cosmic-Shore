using System;
using UnityEngine;

namespace CosmicShore.UI
{

    [CreateAssetMenu(fileName = "Event_InputEventBlock",
        menuName = "ScriptableObjects/SOAP/Events/InputEventBlock")]
    public sealed class ScriptableEventInputEventBlock : ScriptableObject
    {
        public event Action<InputEventBlockPayload> OnRaised;
        public void Raise(InputEventBlockPayload payload) => OnRaised?.Invoke(payload);
    }
}