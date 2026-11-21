using System;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// One hull of an Arena card's hull strip (the style-B configure tree's vessel step): the hull's
    /// icon and name on a <see cref="KitTile"/>. The tile shown as the pilot's current pick is the
    /// selected one; a hull another pilot has claimed is drawn locked and refuses with a shake.
    ///
    /// <para>The panel (<see cref="ArenaLaunchPanel"/>) binds the tile to its index and draws it; the
    /// modal decides what a tap does. A tile with no hull to show (the card lists fewer hulls than the
    /// strip has tiles) hides through its CanvasGroup.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Hull Tile")]
    public class KitHullTile : MonoBehaviour
    {
        [Tooltip("The tile: frame, focus ring, selected and locked looks. Its click is the pick.")]
        [SerializeField] private KitTile tile;
        [Tooltip("The hull's icon (SO_Vessel.IconActive).")]
        [SerializeField] private UnityEngine.UI.Image art;
        [Tooltip("The hull's name (Label role).")]
        [SerializeField] private KitText nameText;
        [Tooltip("The tile's own group: hidden while there is no hull for this slot.")]
        [SerializeField] private CanvasGroup group;

        private int _index = -1;
        private Action<int> _onPick;
        private bool _held;
        private bool _wired;

        public void Bind(int index, Action<int> onPick)
        {
            _index = index;
            _onPick = onPick;
            if (!tile || _wired) return;
            tile.onClick.AddListener(HandleClick);
            _wired = true;
        }

        public void Show(SO_Vessel ship, bool selected, bool heldByOther, bool interactable)
        {
            KitVisibility.SetShown(group, ship);
            _held = heldByOther;
            if (!ship) return;
            if (art)
            {
                art.sprite = ship.IconActive;
                art.enabled = ship.IconActive;
            }
            if (nameText) nameText.SetText(ship.Name);
            if (!tile) return;
            tile.IsSelected = selected;
            tile.IsLocked = heldByOther;
            tile.interactable = interactable;
        }

        private void HandleClick()
        {
            if (_held)
            {
                KitShake.Play(gameObject);
                return;
            }
            _onPick?.Invoke(_index);
        }
    }
}
