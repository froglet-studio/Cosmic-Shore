using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The squad's REAL vessels standing on the hangar's pads, drawn over the Squads tab's video (Docs/MENU_REDESIGN_B.md
    /// section 202; Shombith, 2026-11-14, of the vessel-swap check: "Yeah lets build that"). The hangar clips used to bake
    /// three hulls into the picture, so changing a seat's vessel changed nothing behind the cards. The clips now play
    /// with their pads empty, and this layer stands each seat's hull on its pad from the clips' own camera
    /// (<see cref="ArkBackdropConfigSO.HangarView"/>), breathing with the clip's loop so the two never slide apart.
    ///
    /// <para>An open seat's pad stands empty; an offline mate's hull is powered down (the class livery on its domain
    /// slots, no pad glow, no hover); a live hull wears the squad's domain and floats. The hulls are display copies
    /// harvested off the prefab assets by <see cref="VesselModelBuilder.TryBuildLive"/> (no gameplay component wakes) and
    /// rebuilt only when the line-up, the domain or the worn colour set changes.</para>
    ///
    /// <para>Drawn by a disabled, hand-stepped camera on a private stage parked far outside every other camera, into a
    /// transparent texture shown by a RawImage laid exactly over the picture (the same rect, stretched the same way).
    /// No post-processing: URP's post pass writes alpha 1 over the whole target (HangarVesselStage found it).</para>
    /// </summary>
    public sealed class SquadVesselLayer : MonoBehaviour
    {
        static readonly Vector3 StageOrigin = new(0f, -130000f, -30000f);
        const int Seats = SavedSquad.SlotCount;

        ArkBackdropConfigSO _config;
        RawImage _surface;
        Camera _camera;
        RenderTexture _target;
        Transform _stage;
        Light _lamp;
        readonly GameObject[] _hulls = new GameObject[Seats];
        readonly float[] _lift = new float[Seats];
        readonly Light[] _padLights = new Light[Seats];
        readonly SquadsPage.HangarSeat[] _seats = new SquadsPage.HangarSeat[Seats];
        Domains _domain;
        SO_ColorSet _set;
        bool _built;
        bool _shown;
        float _nextRender;

        /// <summary>A layer over <paramref name="picture"/>: its sibling just above it, on the same rect, hidden until
        /// <see cref="Show"/>.</summary>
        public static SquadVesselLayer Create(RectTransform picture, ArkBackdropConfigSO config)
        {
            var go = new GameObject("SquadVessels", typeof(RectTransform));
            go.layer = picture.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(picture.parent, false);
            rt.SetSiblingIndex(picture.GetSiblingIndex() + 1);
            rt.anchorMin = picture.anchorMin;
            rt.anchorMax = picture.anchorMax;
            rt.pivot = picture.pivot;
            rt.offsetMin = picture.offsetMin;
            rt.offsetMax = picture.offsetMax;
            var layer = go.AddComponent<SquadVesselLayer>();
            layer._config = config;
            layer._surface = go.AddComponent<RawImage>();
            layer._surface.raycastTarget = false;
            layer._surface.enabled = false;
            return layer;
        }

        /// <summary>Stands <paramref name="squads"/>' shown line-up on the pads (rebuilding only what changed) and shows the
        /// layer; hides it when no squad is shown or the config has the vessels off.</summary>
        public void Show(SquadsPage squads)
        {
            var view = _config ? _config.Hangar : default;
            if (!squads || !squads.HasHangar || !view.enabled || !_config.VesselPrefabs || view.pads == null)
            {
                Hide();
                return;
            }
            EnsureRig(view);
            bool changed = !_built || _domain != squads.HangarDomain || _set != ColorTrace.Worn;
            for (int i = 0; i < Seats; i++)
                if (!squads.GetHangarSeat(i).Equals(_seats[i])) changed = true;
            if (changed)
            {
                _domain = squads.HangarDomain;
                _set = ColorTrace.Worn;
                for (int i = 0; i < Seats; i++)
                {
                    _seats[i] = squads.GetHangarSeat(i);
                    Build(i, view);
                }
                _built = true;
            }
            _shown = true;
            _surface.enabled = true;
            _nextRender = 0f;
        }

        /// <summary>Hides the layer and lets its texture go; the hulls stay built for the next <see cref="Show"/>.</summary>
        public void Hide()
        {
            _shown = false;
            if (_surface) _surface.enabled = false;
            ReleaseTarget();
        }

        /// <summary>Poses the camera and the hulls at <paramref name="loopPhase"/> (0..1 through the clip's loop; below 0,
        /// the layer's own clock) and draws them.</summary>
        public void Tick(float loopPhase)
        {
            if (!_shown || !_camera || !_target) return;
            var view = _config.Hangar;
            int rate = Mathf.Max(1, view.renderRate);
            if (Time.unscaledTime < _nextRender) return;
            _nextRender = Time.unscaledTime + 1f / rate;
            float t = loopPhase >= 0f ? loopPhase : Mathf.Repeat(Time.unscaledTime / Mathf.Max(0.1f, view.loopSeconds), 1f);
            Pose(view, t);
            _camera.Render();
        }

        void Pose(ArkBackdropConfigSO.HangarView view, float t)
        {
            // r67_ark_tabs_render.py's camera, turned into Unity's y up: it sways across once a loop, bobs twice, and its aim
            // swings across once a quarter-loop out of step.
            float a = t * Mathf.PI * 2f;
            var position = StageOrigin + view.cameraPosition +
                           new Vector3(view.cameraSway.x * Mathf.Sin(a), view.cameraSway.y * Mathf.Sin(2f * a), 0f);
            var aim = StageOrigin + view.cameraAim + new Vector3(view.aimSway * Mathf.Cos(a), 0f, 0f);
            _camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(aim - position, Vector3.up));
            _camera.fieldOfView = view.verticalFov;
            _camera.aspect = (float)_target.width / _target.height;

            for (int i = 0; i < Seats; i++)
            {
                var hull = _hulls[i];
                if (!hull) continue;
                bool live = _seats[i].Live;
                float phase = PadIndex(i) * view.phaseStep;
                float bob = live ? view.hover * Mathf.Sin(a + phase) : 0f;
                float rock = live ? view.rockDegrees * Mathf.Sin(a + 1.7f * phase) : 0f;
                hull.transform.SetPositionAndRotation(
                    StageOrigin + Pad(view, i) + Vector3.up * (_lift[i] + bob),
                    Facing * Quaternion.AngleAxis(rock, Vector3.forward));
            }
        }

        // Every hull faces the camera across the bay, nose to the door, as the clips' line-up did.
        static Quaternion Facing => Quaternion.LookRotation(Vector3.back, Vector3.up);

        static Vector3 Pad(ArkBackdropConfigSO.HangarView view, int seat) =>
            seat < view.pads.Length ? view.pads[seat] : Vector3.zero;

        // The render's own pad order, left to right (seat two, the player, seat three): the hover phases follow it.
        static int PadIndex(int seat) => seat switch { 0 => 1, 1 => 0, _ => seat };

        void Build(int seat, ArkBackdropConfigSO.HangarView view)
        {
            if (_hulls[seat]) Destroy(_hulls[seat]);
            _hulls[seat] = null;
            var s = _seats[seat];
            var light = _padLights[seat];
            if (light) light.enabled = false;
            if (!s.Filled || seat >= view.pads.Length) return;
            if (!_config.VesselPrefabs.TryGetShipPrefab(s.Vessel, out Transform prefab, reportMissing: false)) return;
            var livery = s.Live ? DomainMaterial(_domain) : _config.OfflineLivery;
            if (!VesselModelBuilder.TryBuildLive(prefab, view.hullLength * 0.5f, livery, out var model)) return;
            if (s.Live) VesselVisionShading.StampDisplayModel(model.transform, SignalColor(_domain));
            model.transform.SetParent(_stage, true);
            SetLayerRecursive(model, _stage.gameObject.layer);
            // Stand it on its pad: the hull's lowest point `clearance` above the floor, whatever the hull's shape.
            model.transform.SetPositionAndRotation(StageOrigin + Pad(view, seat), Facing);
            float bottom = float.MaxValue;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                bottom = Mathf.Min(bottom, r.bounds.min.y);
            _lift[seat] = bottom < float.MaxValue ? view.clearance + (StageOrigin.y + Pad(view, seat).y - bottom) : view.clearance;
            _hulls[seat] = model;

            if (!s.Live) return;
            if (!light)
            {
                light = new GameObject($"Pad{seat}").AddComponent<Light>();
                light.transform.SetParent(_stage, false);
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                _padLights[seat] = light;
            }
            light.transform.position = StageOrigin + Pad(view, seat) + Vector3.up * 0.6f;
            light.color = SignalColor(_domain);
            light.intensity = view.padLightIntensity;
            light.range = view.padLightRange;
            light.enabled = true;
        }

        void EnsureRig(ArkBackdropConfigSO.HangarView view)
        {
            var size = new Vector2Int(Mathf.Max(64, view.layerSize.x), Mathf.Max(64, view.layerSize.y));
            if (_target && (_target.width != size.x || _target.height != size.y)) ReleaseTarget();
            if (!_target)
            {
                _target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "SquadVessels",
                    antiAliasing = 4,
                };
                _target.Create();
                _surface.texture = _target;
                if (_camera) _camera.targetTexture = _target;
            }

            if (!_stage)
            {
                var stage = new GameObject("SquadVesselStage") { hideFlags = HideFlags.DontSave };
                stage.transform.position = StageOrigin;
                _stage = stage.transform;
                _lamp = new GameObject("Lamp").AddComponent<Light>();
                _lamp.transform.SetParent(_stage, false);
                _lamp.type = LightType.Point;
                _lamp.shadows = LightShadows.None;
            }
            // A white lamp over the player's pad: the hulls take their colour from their own materials and the pads' glow.
            _lamp.transform.position = StageOrigin + Pad(view, 0) + view.lampOffset;
            _lamp.color = Color.white;
            _lamp.intensity = view.lampIntensity;
            _lamp.range = view.lampRange;

            if (_camera) return;
            var go = new GameObject("SquadVesselCamera") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_stage, false);
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;                      // stepped by hand in Tick
            _camera.targetTexture = _target;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // the hangar's video shows through
            _camera.cullingMask = ~LayerMask.GetMask("UI");
            _camera.useOcclusionCulling = false;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 200f;
            OffscreenCameraSetup.AdoptGameCameraImage(_camera, postProcessing: false, antiAliasing: false, shadows: false);
            _camera.allowMSAA = true;
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data)
            {
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }
        }

        /// <summary>The domain SHIP material the live ship wears (the theme builds the sets at runtime); null keeps the
        /// prefab's authored accent.</summary>
        static Material DomainMaterial(Domains domain)
        {
            var sets = ThemeManager.Data != null ? ThemeManager.Data.TeamMaterialSets : null;
            if (sets == null) return null;
            return sets.TryGetValue(domain, out var set) && set ? set.ShipMaterial : null;
        }

        static Color SignalColor(Domains domain)
        {
            var colors = ThemeManager.Data != null ? ThemeManager.Data.ColorSet : null;
            return colors ? colors.GetDomainSignalColor(domain) : Color.white;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        void ReleaseTarget()
        {
            if (!_target) return;
            if (_surface) _surface.texture = null;
            if (_camera) _camera.targetTexture = null;
            _target.Release();
            Destroy(_target);
            _target = null;
        }

        /// <summary>Everything goes: the hulls, the stage, the camera and the texture (on leaving the Ark).</summary>
        public void Release()
        {
            Hide();
            for (int i = 0; i < Seats; i++)
            {
                if (_hulls[i]) Destroy(_hulls[i]);
                _hulls[i] = null;
                _padLights[i] = null;
                _seats[i] = default;
            }
            if (_stage) Destroy(_stage.gameObject);   // the camera, the lamp and the pad lights are its children
            _stage = null;
            _camera = null;
            _lamp = null;
            _built = false;
        }

        void OnDestroy() => Release();
    }
}
