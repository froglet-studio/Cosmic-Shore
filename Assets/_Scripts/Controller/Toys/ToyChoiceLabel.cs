using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The ONE exception to "freestyle toys carry no text": the word over a painting's completion
    /// choice gates - SHARE and REPAINT.
    ///
    /// <para>Every other toy switch is read by its ring and its body, and the Toy Box menu is where
    /// a player learns what a toy is called. These two cannot be: they appear side by side, on the
    /// same neutral ring and sphere, at the moment a painting finishes, and they do opposite things -
    /// one exports the picture, the other erases it. Colour alone cannot carry "which of these two
    /// throws my painting away", and there is no menu between the player and that choice to learn
    /// it from.</para>
    ///
    /// <para>It lives in its own file so <c>Tools/Build/toy_switch_ring_geometry.py --check</c> can
    /// exempt exactly this and nothing else: a TextMeshPro type anywhere else in a toy source still
    /// fails the build.</para>
    /// </summary>
    public static class ToyChoiceLabel
    {
        /// <summary>
        /// Hang <paramref name="text"/> clear above a choice gate whose switch ring has radius
        /// <paramref name="ringRadius"/>, reading from every approach direction.
        ///
        /// <para>Sized exactly as it was before toys lost their text: the font is
        /// <c>contentRadius x 1.425</c> with the content at 0.79 of the ring, and the height clears
        /// the ring's tube plus half a text block (TMP anchors world text at its MIDDLE).</para>
        /// </summary>
        public static TMP_Text Add(Transform gate, string text, Color color, float ringRadius)
        {
            float contentRadius = ringRadius * 0.79f;
            float fontSize = Mathf.Max(8f, contentRadius * 1.425f);
            float height = ringRadius * (1f + ToyFactory.RingTubeFraction) + fontSize * 0.85f;

            // 3D TextMeshPro uses a RectTransform - create it up front so AddComponent is safe.
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(gate, false);
            go.transform.localPosition = Vector3.up * height;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
            go.AddComponent<Billboard>();
            return tmp;
        }

        /// <summary>
        /// Faces its transform away from the main camera each LateUpdate, so the word reads from
        /// every side. One rotation write per frame, on a label that exists only while a finished
        /// painting is waiting on the choice.
        /// </summary>
        sealed class Billboard : MonoBehaviour
        {
            Camera _cam;

            void LateUpdate()
            {
                if (!_cam) _cam = Camera.main;
                if (!_cam) return;
                // Forward points AWAY from the camera - TextMeshPro's readable face looks at the viewer.
                Vector3 away = transform.position - _cam.transform.position;
                if (away.sqrMagnitude < 1e-6f) return;
                // Directly above/below, world-up is colinear with the view and LookRotation's
                // implicit up degenerates (the text rolls) - use the camera's own up there.
                Vector3 up = Mathf.Abs(Vector3.Dot(away.normalized, Vector3.up)) > 0.98f
                    ? _cam.transform.up
                    : Vector3.up;
                transform.rotation = Quaternion.LookRotation(away, up);
            }
        }
    }
}
