using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    // Part of ArcadeGameConfigureModal (Docs/REFACTOR_BACKLOG.md M13): this region moved here verbatim from ArcadeGameConfigureModal.cs.
    public partial class ArcadeGameConfigureModal
    {
        #region Favorites

        public void ToggleFavorite()
        {
            if (_selectedGame == null) return;

            if (arcadeExploreView != null)
                arcadeExploreView.ToggleFavorite();

            bool favorited = FavoriteSystem.IsFavorited(_selectedGame.Mode);
            if (selectedGameFavoriteIcon != null)
                selectedGameFavoriteIcon.Favorited = favorited;
            if (_activePanel) _activePanel.RefreshFavorite(favorited);
        }

        #endregion
    }
}
