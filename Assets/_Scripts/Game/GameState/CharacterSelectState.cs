using CosmicShore.App.UI.Views;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;
using VContainer;

namespace CosmicShore.Game.GameState
{
    public class CharacterSelectState : GameStateBehaviour
    {
        public override GameState ActiveState => GameState.CharSelect;

        protected override void Awake()
        {
            base.Awake();
        }
    }
}
