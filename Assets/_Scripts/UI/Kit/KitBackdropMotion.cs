using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>How a frosted window backdrop moves (Docs/MENU_REDESIGN_B.md §62). Values are the shader's
    /// <c>_Motion</c> codes: keep them as they are.</summary>
    public enum BackdropMotion
    {
        Still = 0,
        /// <summary>H2: one soft ring rippling out from the centre through the hexagons.</summary>
        Ring = 1,
        /// <summary>H4: one soft band of light rising and falling behind the glass.</summary>
        Tide = 2,
        /// <summary>H6: the hexagons grow out from the centre when the window opens, then hold still.</summary>
        Grow = 3,
    }

    /// <summary>
    /// On a window's lattice backdrop (the Image drawn with <c>Lattice_Window.mat</c>): each time the window opens it
    /// picks one of <see cref="motions"/> at random and hands it to the shader, and while the window shows it keeps
    /// the frosted glass's blur running (<see cref="UIBackdropBlur"/>). The owner, 2026-10-24: "randomize H2, 4, 6
    /// everywhere".
    ///
    /// <para><b>Open means visible, not enabled.</b> A hub window stays active and fades through its CanvasGroup, so the
    /// open is the moment the backdrop's inherited alpha rises above zero; that is also when the blur starts, and it
    /// stops when the alpha falls back to zero or the backdrop is disabled.</para>
    ///
    /// <para><b>How it reaches the shader.</b> As vertex data, so every window can wear its own motion on the one shared
    /// material: UV1 = (motion + 1, the time the window opened, matching the shader's <c>_Time.y</c>), UV2 = the rect's
    /// size in canvas units (the ring is measured from the rect's centre). The mesh is rebuilt only on an open, so a
    /// showing window costs one alpha read per frame.</para>
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Backdrop Motion")]
    public class KitBackdropMotion : MonoBehaviour, IMeshModifier
    {
        [Header("Motion")]
        [Tooltip("Each open picks one of these at random. One entry always wears that one; empty is still.")]
        [SerializeField] List<BackdropMotion> motions = new() { BackdropMotion.Ring, BackdropMotion.Tide, BackdropMotion.Grow };
        [Tooltip("Keep the frosted glass's blur running while this backdrop shows. Off: the hexagons over the dark veil.")]
        [SerializeField] bool frosted = true;

        BackdropMotion _current;
        float _openedAt;
        bool _showing, _usingBlur;
        Graphic _graphic;

        Graphic graphic => _graphic ? _graphic : (_graphic = GetComponent<Graphic>());

        /// <summary>The motion the current (or last) open picked.</summary>
        public BackdropMotion Current => _current;

        // A mesh modifier is an IMeshModifier on the Graphic's GameObject (what BaseMeshEffect is); the Graphic asks
        // for it on every rebuild, so enabling or disabling this only has to dirty the mesh.
        void OnEnable()
        {
            if (graphic) graphic.SetVerticesDirty();
        }

        void OnDisable()
        {
            SetShowing(false);
            if (graphic) graphic.SetVerticesDirty();
        }

        void LateUpdate()
        {
            if (!graphic) return;
            SetShowing(graphic.canvasRenderer.GetInheritedAlpha() > 0.004f);
        }

        void SetShowing(bool showing)
        {
            if (showing == _showing) return;
            _showing = showing;
            if (showing)
            {
                _current = motions is { Count: > 0 } ? motions[Random.Range(0, motions.Count)] : BackdropMotion.Still;
                _openedAt = Time.timeSinceLevelLoad;
                if (graphic) graphic.SetVerticesDirty();
            }
            bool wantBlur = showing && frosted && isActiveAndEnabled;
            if (wantBlur == _usingBlur) return;
            _usingBlur = wantBlur;
            if (wantBlur) UIBackdropBlur.AddUser();
            else UIBackdropBlur.RemoveUser();
        }

        public void ModifyMesh(Mesh mesh) { }

        public void ModifyMesh(VertexHelper vh)
        {
            if (!isActiveAndEnabled || vh.currentVertCount == 0) return;

            // UV1 and UV2 reach the shader only when the root canvas outputs those channels.
            var canvas = graphic.canvas ? graphic.canvas.rootCanvas : null;
            const AdditionalCanvasShaderChannels channels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            if (canvas && (canvas.additionalShaderChannels & channels) != channels)
                canvas.additionalShaderChannels |= channels;

            // The rect's size in the canvas's units: the shader measures the ring there.
            Vector2 size = graphic.rectTransform.rect.size;
            if (canvas)
            {
                Vector3 own = graphic.rectTransform.lossyScale, root = canvas.transform.lossyScale;
                size = new Vector2(size.x * (root.x != 0f ? own.x / root.x : 1f), size.y * (root.y != 0f ? own.y / root.y : 1f));
            }

            var motion = new Vector2((int)_current + 1, _openedAt);
            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = motion;
                vertex.uv2 = size;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
