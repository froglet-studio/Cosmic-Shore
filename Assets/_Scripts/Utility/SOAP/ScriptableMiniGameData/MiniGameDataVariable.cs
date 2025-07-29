using System;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.SOAP
{
    [CreateAssetMenu(fileName = "scriptable_variable_" + nameof(MiniGameData), menuName = "Soap/ScriptableVariables/"+ nameof(MiniGameData))]
    public class MiniGameDataVariable : ScriptableVariable<MiniGameData>
    {
        public event Action OnInitialize;
        public event Action OnStartMiniGame;
        public event Action OnEndMiniGame;
        
        public void InvokeInitialize() => OnInitialize?.Invoke();
        public void InvokeStartMiniGame() => OnStartMiniGame?.Invoke();
        public void InvokeEndMiniGame() => OnEndMiniGame?.Invoke();
    }
}
