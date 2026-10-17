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
    /// reach - rooted at one point, fanned slightly across the hull's width and hooked down at the tips like a claw -
    /// so they sit inside it and the visual does not lie about what catches fire.</para>
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
        Vector3 _apex, _axis, _down;
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
        /// Step and draw the cords for one frame: the fingers root at <paramref name="apex"/> and fan out slightly. <paramref name="apex"/> is the claw's muzzle in world space,
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

            // A CLAW (design asks, 2026-10-16 and -17: "the whips come out as an X shape... I want one convergence
            // point and they spread out slightly like claws"). Every finger roots at the ONE muzzle point and they open
            // in a single flat fan across the hull's width, only a few degrees apart (whipSpreadDegrees, independent of
            // the burn cone), so from the chase camera they read as a hand of talons, not a starburst. Draw then hooks
            // every tip the same way, down, like a claw. Two earlier layouts read as an X: roots in a row alternating
            // up and down, and fingers spread evenly round the full cone (four rays round a point ARE an X or a +).
            int n = _cords.Length;
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) : 0.5f;
                float yaw = Mathf.Lerp(-so.WhipSpreadDegrees, so.WhipSpreadDegrees, t);
                Vector3 dir = aim * (Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward);
                _cords[i].Step(apex, dir, length, so.WhipRigidity, settings, dt);
            }

            _apex = apex;
            _axis = aim * Vector3.forward;
            _down = aim * Vector3.down;
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
                Hook(so.WhipHook, written);
                if (line.positionCount != written) line.positionCount = written;
                line.SetPositions(_drawn);
            }
        }

        /// <summary>Hook every drawn finger the same way - down, more toward the tip (s squared) - so a straight cord
        /// reads as a curved talon and the set as one claw. <paramref name="hook"/> is the tip's drop as a fraction of
        /// the claw's length. Cosmetic: the cord's own physics, and the cone that burns, are untouched.</summary>
        void Hook(float hook, int count)
        {
            if (hook <= 0f || _length <= 1e-4f) return;
            for (int p = 0; p < count; p++)
            {
                float s = Mathf.Clamp01(Vector3.Dot(_drawn[p] - _apex, _axis) / _length);
                _drawn[p] += _down * (hook * _length * s * s);
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
