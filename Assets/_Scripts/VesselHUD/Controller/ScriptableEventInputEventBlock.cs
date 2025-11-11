using System;
using UnityEngine;

namespace CosmicShore.Game
{

    [CreateAssetMenu(fileName = "ScriptableEventInputEventBlock",
        menuName = "ScriptableObjects/Events/Input Event Block")]
    public sealed class ScriptableEventInputEventBlock : ScriptableObject
    {
        public event Action<InputEventBlockPayload> OnRaised;
        public void Raise(InputEventBlockPayload payload) => OnRaised?.Invoke(payload);
    }
}