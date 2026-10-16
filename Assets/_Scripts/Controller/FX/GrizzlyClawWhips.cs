using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The plasma claw's look (design ask, 2026-10-16): FOUR stiff, red WHIP CORDS per trigger instead of a flat cone,
    /// mimicking the Brittle Star's Charge Mode whips but "more rigid and red". Each cord is a
    /// <see cref="BrittleStarCord"/> - the same wave-equation rope the Brittle Star steps - held at a high fixed
    /// rigidity, so it reads as a near-straight claw finger that still lags a turn and shivers when the hull flicks.
    ///
    /// <para><b>Cosmetic only, every peer draws its own.</b> What burns is still the claw's cone
    /// (<c>GrizzlyFlamethrowerActionExecutor.IgniteCone</c>): the four cords fan across that cone's half-angle at its
    /// reach - rooted at one point, opening round a ring and curling in at the tips like a claw - so they are its
    /// visible envelope and the visual does not lie about what catches fire.</para>
    ///
    /// <para>Drawn with the Thresher chain's fresnel tube material (one <see cref="LineRenderer"/> per cord, colours
    /// through a <see cref="MaterialPropertyBlock"/> so the shared material is never instanced). The rim dims as the
    /// cone's energy cell empties.</para>
    /// </summary>
    public sealed class GrizzlyClawWhips : MonoBehaviour
    {
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static Material s_fallbackMaterial;

        BrittleStarCord[] _cords;
        LineRenderer[] _lines;
        Vector3[] _drawn;
        MaterialPropertyBlock _mpb;
        bool _visible;
        Vector3 _apex, _axis;
        float _length;

        /// <summary>Gets (or creates) the whip set called <paramref name="setName"/> hanging off this vessel.</summary>
        public static GrizzlyClawWhips EnsureFor(Transform vessel, string setName)
        {
            if (vessel == null) return null;
            foreach (var existing in vessel.GetComponentsInChildren<GrizzlyClawWhips>(true))
                if (existing.name == setName) return existing;

            var go = new GameObject(setName);
            go.transform.SetParent(vessel, false);
            return go.AddComponent<GrizzlyClawWhips>();
        }

        /// <summary>
        /// Step and draw the cords for one frame: the fingers root at <paramref name="apex"/> and open round a ring. <paramref name="apex"/> is the claw's muzzle in world space,
        /// <paramref name="aim"/> the cone's axis (forward = +z), <paramref name="length"/> / <paramref name="halfAngle"/>
        /// the cone this pull makes, <paramref name="fill01"/> the cone's energy cell.
        /// </summary>
        public void Show(GrizzlyFlamethrowerActionSO so, Vector3 apex, Quaternion aim, float length, float halfAngle,
                         float fill01, float dt)
        {
            if (!so || !Ensure(so)) return;
            if (!_visible)
            {
                SetLinesEnabled(true);
                _visible = true;
            }

            var settings = new BrittleStarCord.Settings
            {
                LooseWave = so.WhipLooseWave,
                RigidWave = so.WhipRigidWave,
                LooseDamping = so.WhipLooseDamping,
                RigidDamping = so.WhipRigidDamping,
                Pose = so.WhipPose,
            };

            // A CLAW, not a fan (design ask, 2026-10-16: "the whips come out as an X shape. I want them to start at a
            // point and cone outward like a set of claws"): every finger roots at the one muzzle point and opens
            // outward around a ring on the cone's surface, evenly spaced; Draw then curls each tip back toward the
            // axis, so the set reads as talons closing round what they burn. (The first cut laid the roots in a row
            // alternating up and down, which from the chase camera read as an X.)
            int n = _cords.Length;
            Vector3 right = aim * Vector3.right;
            float tilt = halfAngle * so.WhipFanFraction;
            for (int i = 0; i < n; i++)
            {
                float azimuth = so.WhipRingPhaseDegrees + 360f * i / n;
                Quaternion local = Quaternion.AngleAxis(azimuth, Vector3.forward) * Quaternion.AngleAxis(-tilt, Vector3.right);
                Vector3 dir = aim * (local * Vector3.forward);
                Vector3 anchor = apex + right * ((i - 0.5f * (n - 1)) * so.WhipRootSpacing);   // 0 spacing = one point
                _cords[i].Step(anchor, dir, length, so.WhipRigidity, settings, dt);
            }

            _apex = apex;
            _axis = aim * Vector3.forward;
            _length = length;
            Draw(so, fill01);
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            if (_cords != null)
                foreach (var cord in _cords) cord.Clear();
            SetLinesEnabled(false);
        }

        void Draw(GrizzlyFlamethrowerActionSO so, float fill01)
        {
            Color body = so.WhipBodyColor;
            body.a = 1f;
            Color rim = so.WhipRimColor * Mathf.Lerp(so.WhipEmptyBrightness, 1f, Mathf.Clamp01(fill01));
            rim.a = 1f;
            _mpb.Clear();
            _mpb.SetColor(DarkColorId, body);
            _mpb.SetColor(BrightColorId, rim);

            int sub = Mathf.Max(1, so.WhipRenderSubdivisions);
            int drawn = ThresherChainLinks.SmoothedCount(_cords[0].Count, sub);
            if (_drawn == null || _drawn.Length != drawn) _drawn = new Vector3[drawn];

            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                var cord = _cords[i];
                if (!cord.Seeded)
                {
                    line.positionCount = 0;
                    continue;
                }
                // Thick at the root, a point at the tip: a claw finger, not a rope.
                line.startWidth = so.WhipWidth;
                line.endWidth = so.WhipWidth * 0.2f;
                line.SetPropertyBlock(_mpb);
                line.startColor = line.endColor = rim;   // the vertex-colour fallback material
                int written = ThresherChainLinks.Smooth(cord.Points, sub, _drawn);
                Curl(so.WhipCurl, written);
                if (line.positionCount != written) line.positionCount = written;
                line.SetPositions(_drawn);
            }
        }

        /// <summary>Bend each drawn finger back toward the claw's axis, more toward the tip (s squared), so a straight
        /// cord reads as a curved talon. Cosmetic: the cord's own physics, and the cone that burns, are untouched.</summary>
        void Curl(float curl, int count)
        {
            if (curl <= 0f || _length <= 1e-4f) return;
            for (int p = 0; p < count; p++)
            {
                Vector3 offset = _drawn[p] - _apex;
                float along = Vector3.Dot(offset, _axis);
                float s = Mathf.Clamp01(along / _length);
                Vector3 radial = offset - _axis * along;
                _drawn[p] -= radial * (curl * s * s);
            }
        }

        bool Ensure(GrizzlyFlamethrowerActionSO so)
        {
            int count = Mathf.Max(1, so.WhipsPerSide);
            int links = Mathf.Max(2, so.WhipLinks);
            if (_lines != null && _lines.Length == count && _cords[0].Links == links) return true;

            var material = so.WhipMaterial ? so.WhipMaterial : FallbackMaterial();
            if (!material) return false;   // headless: no graphics, no visuals

            if (_lines != null)
                foreach (var old in _lines)
                    if (old) Destroy(old.gameObject);

            _mpb ??= new MaterialPropertyBlock();
            _cords = new BrittleStarCord[count];
            _lines = new LineRenderer[count];
            for (int i = 0; i < count; i++)
            {
                _cords[i] = new BrittleStarCord(links);
                var go = new GameObject($"Whip{i}");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;   // the fresnel tube is computed across a VIEW-aligned ribbon
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.positionCount = 0;
                line.enabled = _visible;
                _lines[i] = line;
            }
            return true;
        }

        void SetLinesEnabled(bool on)
        {
            if (_lines == null) return;
            foreach (var line in _lines)
                if (line) line.enabled = on;
        }

        static Material FallbackMaterial()
        {
            if (s_fallbackMaterial) return s_fallbackMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            s_fallbackMaterial = new Material(shader) { name = "GrizzlyClawWhip (runtime)", renderQueue = 3000 };
            return s_fallbackMaterial;
        }
    }
}
