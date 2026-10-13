using System;
using UnityEngine;

namespace CosmicShore.UI
{
    [RequireComponent(typeof(Animator))]
    public class SceneTransitionModal : MonoBehaviour
    {
        private readonly int OPEN = Animator.StringToHash("open");

        [SerializeField]
        Animator _animator;

        private void Awake()
        {
            EnsureFullBleed();
            TransitionDoor(false);
        }

        public void TransitionDoor(bool open) => _animator.SetBool(OPEN, open);

        /// <summary>
        /// The doors are a curtain, so the modal must fill the whole screen
        /// (<c>Docs/UI_ARCHITECTURE_AUDIT.md</c> §1.3, full-bleed).
        ///
        /// <para>Open, each door rests just outside the modal's rect; closed, they cover it. Menu_Main
        /// nests the modal in <c>Safe Area/ModalWindows</c>, and on a notched phone that rect is inset.
        /// The open doors then sat in the cutout margins as grey metal plates, and the closed ones
        /// left the margins uncovered. A desktop never shows it, because its safe area is the
        /// screen.</para>
        ///
        /// <para>The fix hoists the modal to the root canvas, just above the layer it came from, so
        /// draw order against that layer's siblings is unchanged. The Animator animates the doors
        /// by path below this object, so the move does not touch the clips. It is a no-op where the
        /// modal is already a root child (GameCanvas).</para>
        /// </summary>
        private void EnsureFullBleed()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (!canvas) return;

            Transform root = canvas.rootCanvas.transform;
            if (transform.parent == root) return;

            Transform layer = transform;
            while (layer.parent && layer.parent != root)
                layer = layer.parent;
            if (layer.parent != root) return;

            transform.SetParent(root, false);
            transform.SetSiblingIndex(layer.GetSiblingIndex() + 1);
        }
    }
}