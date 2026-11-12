using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.UI
{
    /// <summary>Which part of the Ark a tab looks at (<see cref="ArkInteriorConfigSO"/>).</summary>
    public enum ArkRoom
    {
        None = 0,
        Hangar = 1,
        Pier = 2,
        Artifacts = 3,
    }

    /// <summary>
    /// The Ark screen's 3D, built in code on the converter's off-screen stage (<see cref="ElementalConverterView"/>): the
    /// menu is INSIDE the Ark (Docs/MENU_REDESIGN_B.md sections 166-168). It holds
    /// <list type="bullet">
    /// <item>the Ark's own model, dressed as the gameplay Ark dresses it (the domain's ship material on the Domain slot, every
    /// material drawn double-sided and opaque at the definition's slot brightness - <see cref="ArkModel"/>), its clam doors
    /// swung clear of the mouth, and its hull prisms in the model's sockets as plain renderers (a picture, not mass);</item>
    /// <item>the PIER: the hangar's floor carried out of the mouth into the cell on girders, the domain's trim down both
    /// edges, lamp posts with a prism each, and the round deck in its middle where Garrett's omni combine plays
    /// (<see cref="CombineAnchor"/>);</item>
    /// <item>the ARTIFACTS ROOM: the Reliquary's platform (the found artifacts on pedestals round a column of light) in the
    /// Gallery's hall (alcoves for the ones still to be found, a porthole at its end), well away from the hull.</item>
    /// </list>
    /// Everything is in the Ark model's own space under this transform. Only the room a tab looks at is active
    /// (<see cref="Show"/>): a room nobody can see costs nothing. The set pieces wear copies of the model's own Body,
    /// Domain and Window materials and the theme's prism materials; no new asset is needed. The copies are this stage's and
    /// die with it.
    /// </summary>
    public sealed class ArkInteriorStage : MonoBehaviour
    {
        // The model's material slots (ArkReverse.fbx), told apart as ArkModel tells them apart.
        const int SlotDomain = 0, SlotBody = 1, SlotWindow = 2, SlotEngine = 3;
        // A prism's box collider is 1.025 on a side (BlueBlock.prefab); the shield meshes circumscribe it.
        static readonly Vector3 PrismHalfExtents = Vector3.one * 0.5125f;
        static readonly int ColorMultiplierId = Shader.PropertyToID("_ColorMultiplier");
        static readonly float[] PedestalDegrees = { -75f, -27f, 27f, 75f };

        ArkInteriorConfigSO _cfg;
        Domains _domain;
        GameObject _hull, _model, _pier, _artifacts;
        Transform _combineAnchor;
        Light _crystalLight;
        float _crystalBase;
        readonly List<Object> _owned = new();
        readonly List<(Transform t, float turnsPerSecond)> _spinners = new();
        Material _body, _trim, _window, _block, _shielded;
        Mesh _cube, _cylinder, _torusThick, _torusThin;

        /// <summary>Where the combine plays: over the pier's round deck, its spread shrunk to fit (<see cref="FitCombine"/>).</summary>
        public Transform CombineAnchor => _combineAnchor;

        /// <summary>The room shown now.</summary>
        public ArkRoom Room { get; private set; } = ArkRoom.None;

        /// <summary>Builds the stage under <paramref name="parent"/>. Null (with an error) when the config cannot build it.</summary>
        public static ArkInteriorStage Build(Transform parent, ArkInteriorConfigSO config, Domains domain)
        {
            if (!config || !config.ArkModel || !config.ArkModel.Model)
            {
                CSDebug.LogError("[ArkInteriorStage] No ArkInteriorConfig, or it names no Ark model: the Ark screen has no 3D. " +
                                 "Run Tools/Build/author_ark_interior_assets.py.");
                return null;
            }
            var go = new GameObject("ArkInterior") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var stage = go.AddComponent<ArkInteriorStage>();
            stage._cfg = config;
            stage._domain = domain;
            stage.BuildAll();
            stage.Show(ArkRoom.None);
            return stage;
        }

        void BuildAll()
        {
            _cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            _cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            _torusThick = Own(TorusMesh(1f, 0.012f, 128, 8));
            _torusThin = Own(TorusMesh(1f, 0.007f, 128, 8));
            var theme = ThemeManager.Data;
            if (theme?.TeamMaterialSets != null && theme.TeamMaterialSets.TryGetValue(_domain, out var set) && set)
            {
                _block = set.BlockMaterial;
                _shielded = theme.GetTeamShieldedBlockMaterial(_domain);
            }
            BuildModel(theme);
            _pier = new GameObject("Pier");
            _pier.transform.SetParent(transform, false);
            BuildPier(_pier.transform);
            _artifacts = new GameObject("ArtifactsRoom");
            _artifacts.transform.SetParent(transform, false);
            BuildArtifactsRoom(_artifacts.transform);
        }

        /// <summary>Shows one room: the hull for the hangar and the pier (the pier also shows its deck), the artifacts room
        /// on its own. Everything else is switched off, not hidden.</summary>
        public void Show(ArkRoom room)
        {
            Room = room;
            if (_hull) _hull.SetActive(room is ArkRoom.Hangar or ArkRoom.Pier);
            if (_pier) _pier.SetActive(room == ArkRoom.Pier);
            if (_artifacts) _artifacts.SetActive(room == ArkRoom.Artifacts);
        }

        /// <summary>Shrinks the combine so its whole spread (<paramref name="framingRadius"/>, the player's own) fits the deck.</summary>
        public void FitCombine(float framingRadius)
        {
            if (!_combineAnchor || framingRadius <= 1e-3f) return;
            _combineAnchor.localScale = Vector3.one * (_cfg.CombineRadius / framingRadius);
        }

        /// <summary>The camera for <paramref name="room"/> at <paramref name="seconds"/> (unscaled): its view from the config,
        /// in this stage's space, with the view's slow drift.</summary>
        public void PoseCamera(Camera camera, ArkRoom room, float seconds)
        {
            var v = room switch
            {
                ArkRoom.Pier => _cfg.PierView,
                ArkRoom.Artifacts => _cfg.ArtifactsView,
                _ => _cfg.HangarView,
            };
            var from = transform.TransformPoint(v.position);
            var at = transform.TransformPoint(v.lookAt);
            var look = Quaternion.LookRotation(at - from, transform.up);
            if (v.driftDegrees > 0f) look = Quaternion.AngleAxis(v.driftDegrees * Mathf.Sin(seconds * 0.35f), transform.up) * look;
            camera.transform.SetPositionAndRotation(from, look);
            camera.fieldOfView = v.verticalFov;
            camera.nearClipPlane = _cfg.ClipPlanes.x;
            camera.farClipPlane = _cfg.ClipPlanes.y;
            camera.backgroundColor = _cfg.CellBackground;
        }

        /// <summary>The room's own motion: the artifacts turn, the crystals' light breathes.</summary>
        public void Tick(float dt, float seconds)
        {
            if (Room == ArkRoom.Artifacts)
                foreach (var (t, turns) in _spinners)
                    if (t) t.Rotate(0f, 360f * turns * dt, 0f, Space.Self);
            if (Room == ArkRoom.Pier && _crystalLight)
                _crystalLight.intensity = _crystalBase * (1f + 0.2f * Mathf.Sin(seconds * Mathf.PI * 2f * 0.5f));
        }

        void OnDestroy()
        {
            foreach (var o in _owned)
                if (o) Destroy(o);
            _owned.Clear();
        }

        // ── The Ark ─────────────────────────────────────────────────────────────

        void BuildModel(ThemeManagerDataContainerSO theme)
        {
            var def = _cfg.ArkModel;
            _hull = new GameObject("Hull");                 // the model and its prisms, in this stage's (the model's) space
            _hull.transform.SetParent(transform, false);
            _model = Instantiate(def.Model, _hull.transform, false);
            _model.name = "ArkModel";
            _model.transform.localScale *= def.Scale;
            Dress(def, theme);
            PoseDoors();
            BuildSocketPrisms(def);
            // The cold light of the cell in at the open mouth.
            Light("Mouth", new Vector3(0f, -17.5f, 85f), LightType.Point, _cfg.CellLight, _cfg.CellLightIntensity * 4f, 60f, _hull.transform);
        }

        /// <summary>The gameplay Ark's dressing (<see cref="ArkModel"/>): the Domain slot wears the domain's ship material, and
        /// every material is drawn from a copy that renders both faces, is opaque and writes depth, at the slot's brightness.</summary>
        void Dress(ArkModelDefinitionSO def, ThemeManagerDataContainerSO theme)
        {
            Material ship = theme?.TeamMaterialSets != null && theme.TeamMaterialSets.TryGetValue(_domain, out var set) && set
                ? set.ShipMaterial
                : null;
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in _model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;   // the doors move off their bind bounds
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!source) continue;
                    int slot = SlotOf(def, source);
                    if (slot == SlotDomain && ship) source = ship;
                    if (!copies.TryGetValue(source, out var copy))
                    {
                        copy = Own(new Material(source) { name = source.name + " (Ark interior)", doubleSidedGI = true });
                        if (copy.HasProperty("_Cull")) copy.SetFloat("_Cull", 0f);
                        if (copy.HasProperty("_CullMode")) copy.SetFloat("_CullMode", 0f);
                        if (copy.HasProperty("_RenderFace")) copy.SetFloat("_RenderFace", 0f);
                        MakeOpaque(copy);
                        if (copy.HasProperty(ColorMultiplierId))
                            copy.SetFloat(ColorMultiplierId, copy.GetFloat(ColorMultiplierId) * Brightness(def, slot));
                        copies[source] = copy;
                        if (slot == SlotBody && !_body) _body = copy;
                        if (slot == SlotDomain && !_trim) _trim = copy;
                        if (slot == SlotWindow && !_window) _window = copy;
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
            // A model missing a slot still builds: the set pieces fall back to what there is.
            _body ??= _trim ?? _window;
            _trim ??= _window ?? _body;
            _window ??= _trim;
        }

        static int SlotOf(ArkModelDefinitionSO def, Material source)
        {
            for (int j = 0; j < def.DomainReplacesMaterials.Count; j++)
                if (def.DomainReplacesMaterials[j] == source) return SlotDomain;
            string n = source.name;
            if (n.Contains("Engine")) return SlotEngine;
            if (n.Contains("Screen") || n.Contains("Window")) return SlotWindow;
            return SlotBody;
        }

        static float Brightness(ArkModelDefinitionSO def, int slot) => slot switch
        {
            SlotDomain => def.DomainBrightness,
            SlotBody => def.BodyBrightness,
            SlotWindow => def.WindowBrightness,
            SlotEngine => def.EngineBrightness,
            _ => 1f,
        };

        static void MakeOpaque(Material m)
        {
            if (!m.HasProperty("_Surface")) return;
            m.SetFloat("_Surface", 0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 1f);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 0f);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", 1f);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", 0f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)RenderQueue.Geometry;
        }

        /// <summary>The clam doors swung on past the model's open take, about the model's x axis, so the mouth is a clear
        /// frame onto the pier (the take's own rotation, applied as the rig applies every take: <see cref="ArkRigTable.PoseLocal"/>).</summary>
        void PoseDoors()
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in _model.GetComponentsInChildren<Transform>(true))
                bones.TryAdd(t.name, t);
            PoseDoor(bones, "hangar.door.T", _cfg.TopDoorDegrees);
            PoseDoor(bones, "hangar.door.B", _cfg.BottomDoorDegrees);
        }

        void PoseDoor(Dictionary<string, Transform> bones, string name, float degrees)
        {
            if (!bones.TryGetValue(name, out var bone))
            {
                CSDebug.LogWarning($"[ArkInteriorStage] The Ark model has no bone '{name}': its door stays shut.", this);
                return;
            }
            var restInModel = Quaternion.Inverse(transform.rotation) * bone.rotation;
            bone.localRotation = ArkRigTable.PoseLocal(bone.localRotation, restInModel, Quaternion.AngleAxis(degrees, Vector3.right));
        }

        /// <summary>The hull's prisms in the model's own sockets, each socket's kind at its size: plain renderers in the
        /// theme's prism materials (the hull is a picture here, not conserved mass).</summary>
        void BuildSocketPrisms(ArkModelDefinitionSO def)
        {
            if (!_block || !_cfg.PrismBoxMesh) return;
            var root = new GameObject("HullPrisms").transform;
            root.SetParent(_hull.transform, false);
            var theme = ThemeManager.Data;
            var danger = theme.GetTeamDangerousBlockMaterial(_domain);
            var octa = OctahedronMeshGenerator.GetSharedShieldMesh(PrismHalfExtents);
            var stella = StellatedOctahedronMeshGenerator.GetSharedShieldMesh(PrismHalfExtents);
            foreach (var lay in ArkModel.SocketLays(def, _domain))
            {
                (Mesh mesh, Material mat) = lay.Kind switch
                {
                    PrismKind.Shielded => (octa, _shielded ? _shielded : _block),
                    PrismKind.SuperShielded => (stella, _block),
                    PrismKind.Danger => (_cfg.PrismBoxMesh, danger ? danger : _block),
                    _ => (_cfg.PrismBoxMesh, _block),
                };
                var go = Piece("Prism", mesh, mat, root);
                go.transform.SetLocalPositionAndRotation(lay.Point.Position, lay.Point.Rotation);   // model space, scaled
                go.transform.localScale = lay.Point.Scale;
            }
        }

        // ── The pier ────────────────────────────────────────────────────────────

        void BuildPier(Transform p)
        {
            float z0 = _cfg.PierStartZ, L = _cfg.PierLength, W = _cfg.PierWidth, top = _cfg.PierTopY;
            float mid = z0 + L * 0.5f;
            Box(p, "Deck", new Vector3(0f, top - 0.45f, mid), new Vector3(W, 0.9f, L), _body);
            Box(p, "Spine", new Vector3(0f, top - 2.4f, mid), new Vector3(4f, 3f, L - 4f), _body);
            for (float z = z0 + 2f; z < z0 + L; z += 8f)
                Box(p, "Girder", new Vector3(0f, top - 1.6f, z), new Vector3(W - 1f, 1.4f, 0.6f), _body);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Box(p, "Trim", new Vector3(sx * (W * 0.5f - 0.45f), top + 0.04f, mid), new Vector3(0.12f, 0.08f, L - 0.6f), _trim);
                Box(p, "Trim", new Vector3(sx * (W * 0.5f + 0.02f), top - 0.45f, mid), new Vector3(0.06f, 0.06f, L), _trim);
                for (float z = z0 + 6f; z < z0 + L - 2f; z += _cfg.LampSpacing)
                {
                    if (Mathf.Abs(z - _cfg.DeckZ) < _cfg.DeckRadius + 1.8f) continue;     // the round deck keeps its edge clear
                    Box(p, "Post", new Vector3(sx * (W * 0.5f - 0.6f), top + 0.7f, z), new Vector3(0.3f, 1.4f, 0.3f), _body);
                    if (_block && _cfg.PrismBoxMesh)
                    {
                        var lamp = Piece("Lamp", _cfg.PrismBoxMesh, _block, p);
                        lamp.transform.localPosition = new Vector3(sx * (W * 0.5f - 0.6f), top + 1.75f, z);
                        lamp.transform.localScale = new Vector3(0.22f, 0.6f, 0.22f);
                    }
                }
            }
            Box(p, "Trim", new Vector3(0f, top + 0.04f, z0 + L - 0.3f), new Vector3(W - 1f, 0.08f, 0.12f), _trim);

            // The round deck, ringed in the domain; the combine plays over it.
            var deck = new Vector3(0f, top, _cfg.DeckZ);
            Cylinder(p, "RoundDeck", deck + Vector3.up * 0.12f, _cfg.DeckRadius, 0.3f, _body);
            Ring(p, deck + Vector3.up * 0.3f, _cfg.DeckRadius - 0.2f, true);
            Ring(p, deck + Vector3.up * 0.3f, _cfg.DeckRadius * 0.72f, false);
            _combineAnchor = new GameObject("CombineAnchor").transform;
            _combineAnchor.SetParent(p, false);
            _combineAnchor.localPosition = deck + Vector3.up * _cfg.CombineHeight;

            _crystalBase = _cfg.CrystalLightIntensity;
            _crystalLight = Light("CrystalLight", deck + Vector3.up * 4f, LightType.Point, _cfg.CrystalLight, _crystalBase, 26f, p);
            Light("CellLight", new Vector3(0f, 18f, mid), LightType.Point, _cfg.CellLight, _cfg.CellLightIntensity * 6f, 90f, p);
        }

        // ── The artifacts room ──────────────────────────────────────────────────

        void BuildArtifactsRoom(Transform r)
        {
            var O = _cfg.ArtifactsRoomOrigin;
            float hx = _cfg.HallSize.x, hz = _cfg.HallSize.y, h = _cfg.HallSize.z, t = 0.6f;
            // The Gallery's hall: floor, ceiling, walls, ribs and beams every three metres, the domain's trim.
            Box(r, "Floor", O + new Vector3(0f, -t * 0.5f, 0f), new Vector3(2 * hx + 2 * t, t, 2 * hz + 2 * t), _body);
            Box(r, "Ceiling", O + new Vector3(0f, h + t * 0.5f, 0f), new Vector3(2 * hx + 2 * t, t, 2 * hz + 2 * t), _body);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Box(r, "Wall", O + new Vector3(sx * (hx + t * 0.5f), h * 0.5f, 0f), new Vector3(t, h, 2 * hz), _body);
                Box(r, "Trim", O + new Vector3(sx * (hx - 0.55f), 0.12f, 0f), new Vector3(0.06f, 0.06f, 2 * hz - 0.6f), _trim);
                Box(r, "Trim", O + new Vector3(sx * (hx - 0.55f), h - 0.75f, 0f), new Vector3(0.06f, 0.06f, 2 * hz - 0.6f), _trim);
                Box(r, "Runner", O + new Vector3(sx * 1.6f, 0.03f, 0f), new Vector3(0.06f, 0.04f, 2 * hz - 1f), _trim);
            }
            Box(r, "Back", O + new Vector3(0f, h * 0.5f, -hz - t * 0.5f), new Vector3(2 * hx, h, t), _body);
            Box(r, "End", O + new Vector3(0f, h * 0.5f, hz + t * 0.5f), new Vector3(2 * hx, h, t), _body);
            int ribs = Mathf.Max(1, Mathf.RoundToInt(2 * hz / 3f));
            for (int k = 0; k <= ribs; k++)
            {
                float z = -hz + k * (2 * hz / ribs);
                for (int sx = -1; sx <= 1; sx += 2)
                    Box(r, "Rib", O + new Vector3(sx * (hx - 0.25f), h * 0.5f, z), new Vector3(0.5f, h, 0.45f), _body);
                Box(r, "Beam", O + new Vector3(0f, h - 0.3f, z), new Vector3(2 * hx, 0.6f, 0.45f), _body);
            }
            // The porthole onto the cell at the hall's end.
            var port = O + new Vector3(0f, h * 0.5f, hz - 0.05f);
            var glass = Cylinder(r, "Porthole", port, 3.4f, 0.1f, _window);
            glass.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var rim = Ring(r, port + Vector3.back * 0.15f, 3.45f, true);
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // The alcoves down both walls: the slots for the artifacts still to be found, a dim prism in each.
            int n = _cfg.AlcovesPerWall;
            for (int k = 0; k < n; k++)
            {
                float z = -hz + 5f + k * ((2 * hz - 8f) / Mathf.Max(1, n - 1));
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    float xw = sx * (hx - 0.3f);
                    for (int dz = -1; dz <= 1; dz += 2)
                        Box(r, "Pilaster", O + new Vector3(xw - sx * 0.35f, h * 0.36f, z + dz * 1.6f), new Vector3(0.7f, h * 0.72f, 0.5f), _body);
                    Box(r, "Lintel", O + new Vector3(xw - sx * 0.35f, h * 0.72f + 0.25f, z), new Vector3(0.7f, 0.5f, 3.7f), _body);
                    Box(r, "Niche", O + new Vector3(xw - sx * 0.1f, h * 0.36f, z), new Vector3(0.2f, h * 0.68f, 2.7f), _window);
                    Box(r, "Plinth", O + new Vector3(xw - sx * 0.65f, 0.6f, z), new Vector3(1.3f, 1.2f, 1.6f), _body);
                    Box(r, "Trim", O + new Vector3(xw - sx * 1.31f, 1.18f, z), new Vector3(0.05f, 0.05f, 1.6f), _trim);
                    if (_shielded)
                    {
                        var slot = Piece("EmptySlot", OctahedronMeshGenerator.GetSharedShieldMesh(PrismHalfExtents), _shielded, r);
                        slot.transform.localPosition = O + new Vector3(xw - sx * 0.7f, 2.9f, z);
                        slot.transform.localScale = new Vector3(0.2f, 0.27f, 0.2f);
                    }
                }
            }

            // The Reliquary's platform in the hall's middle: the column of light, the found artifacts on pedestals round it.
            var P = O + new Vector3(0f, 0f, 2f);
            Cylinder(r, "Platform", P + Vector3.up * 0.15f, _cfg.PlatformRadii.x, 0.3f, _body);
            Ring(r, P + Vector3.up * 0.32f, _cfg.PlatformRadii.x - 0.2f, true);
            Ring(r, P + Vector3.up * 0.32f, 2.2f, false);
            Ring(r, P + Vector3.up * 0.32f, 3.0f, false);
            Cylinder(r, "LightColumn", P + Vector3.up * (h * 0.5f), 0.22f, h, _window);
            var crown = Ring(r, P + Vector3.up * (h - 0.2f), 1.2f, true);
            crown.name = "Crown";
            Light("ColumnLight", P + Vector3.up * 3f, LightType.Point, _cfg.CellLight, _cfg.CellLightIntensity * 3f, 18f, r);
            for (int i = 0; i < PedestalDegrees.Length; i++)
            {
                float a = PedestalDegrees[i] * Mathf.Deg2Rad;
                var foot = P + new Vector3(_cfg.PlatformRadii.y * Mathf.Sin(a), 0.3f, _cfg.PlatformRadii.y * Mathf.Cos(a));
                Cylinder(r, "Pedestal", foot + Vector3.up * 0.55f, 0.75f, 1.1f, _body);
                Ring(r, foot + Vector3.up * 1.12f, 0.68f, false);
                bool selected = i == 1;
                var artifact = BuildArtifact(i, r);
                artifact.localPosition = foot + Vector3.up * 2.75f;
                artifact.localScale = Vector3.one * (_cfg.ArtifactScale.x * (selected ? _cfg.ArtifactScale.y : 1f));
                _spinners.Add((artifact, selected ? 0.12f : 0.06f * (i % 2 == 0 ? 1f : -1f)));
                var spot = Light("ArtifactSpot", foot + Vector3.up * 8.5f, LightType.Spot, Color.white,
                    _cfg.ArtifactSpotIntensity * (selected ? 2f : 1f), 14f, r);
                spot.spotAngle = 28f;
                spot.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }
        }

        /// <summary>The four artifacts the game has (Assets/_SO_Assets/Pilots/Artifacts), each built from the game's prisms after
        /// what it does: Javelin a long blade, Hydra a core with five heads round it, Aftershock two stacked cores with a ring,
        /// Aegis a super-shield. Unit size; the caller scales it.</summary>
        Transform BuildArtifact(int index, Transform parent)
        {
            var root = new GameObject("Artifact").transform;
            root.SetParent(parent, false);
            if (!_block || !_cfg.PrismBoxMesh) return root;
            var octa = OctahedronMeshGenerator.GetSharedShieldMesh(PrismHalfExtents);
            var stella = StellatedOctahedronMeshGenerator.GetSharedShieldMesh(PrismHalfExtents);
            void Part(Mesh m, Vector3 at, Vector3 size)
            {
                var g = Piece("Part", m, _block, root);
                g.transform.localPosition = at;
                g.transform.localScale = size;
            }
            void Ring5(int count, float radius, float y, Vector3 size)
            {
                for (int k = 0; k < count; k++)
                {
                    float a = k * Mathf.PI * 2f / count;
                    Part(octa, new Vector3(radius * Mathf.Cos(a), y, radius * Mathf.Sin(a)), size);
                }
            }
            switch (index)
            {
                case 0: Part(_cfg.PrismBoxMesh, new Vector3(0f, -0.1f, 0f), new Vector3(0.12f, 1.05f, 0.12f)); break;   // Javelin
                case 1: Part(octa, Vector3.zero, new Vector3(0.12f, 0.17f, 0.12f)); Ring5(5, 0.46f, 0.08f, new Vector3(0.06f, 0.085f, 0.06f)); break;   // Hydra
                case 2: Part(octa, new Vector3(0f, 0.2f, 0f), new Vector3(0.14f, 0.18f, 0.14f)); Part(octa, new Vector3(0f, -0.22f, 0f), new Vector3(0.11f, 0.14f, 0.11f));
                    Ring5(6, 0.34f, -0.22f, new Vector3(0.035f, 0.05f, 0.035f)); break;   // Aftershock
                default: Part(stella, Vector3.zero, Vector3.one * 0.24f); break;   // Aegis
            }
            return root;
        }

        // ── Pieces ──────────────────────────────────────────────────────────────

        GameObject Piece(string name, Mesh mesh, Material material, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var go = Piece(name, _cube, material, parent);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            return go;
        }

        /// <summary>A disc or column: the built-in cylinder is 1 across and 2 tall.</summary>
        GameObject Cylinder(Transform parent, string name, Vector3 centre, float radius, float height, Material material)
        {
            var go = Piece(name, _cylinder, material, parent);
            go.transform.localPosition = centre;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            return go;
        }

        /// <summary>A flat ring of the domain's trim at <paramref name="radius"/> (the torus meshes are unit radius).</summary>
        GameObject Ring(Transform parent, Vector3 centre, float radius, bool thick)
        {
            var go = Piece("Ring", thick ? _torusThick : _torusThin, _trim, parent);
            go.transform.localPosition = centre;
            go.transform.localScale = Vector3.one * radius;
            return go;
        }

        Light Light(string name, Vector3 at, LightType type, Color color, float intensity, float range, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        T Own<T>(T o) where T : Object
        {
            _owned.Add(o);
            return o;
        }

        /// <summary>A torus in the XZ plane, major radius <paramref name="major"/>, tube radius <paramref name="minor"/>.</summary>
        static Mesh TorusMesh(float major, float minor, int segments, int sides)
        {
            var v = new Vector3[(segments + 1) * (sides + 1)];
            var n = new Vector3[v.Length];
            var tri = new int[segments * sides * 6];
            int k = 0;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    var normal = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v[k] = c * major + normal * minor;
                    n[k] = normal;
                    k++;
                }
            }
            k = 0;
            for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                int a0 = i * (sides + 1) + j, a1 = a0 + sides + 1;
                tri[k++] = a0; tri[k++] = a0 + 1; tri[k++] = a1;
                tri[k++] = a1; tri[k++] = a0 + 1; tri[k++] = a1 + 1;
            }
            var mesh = new Mesh { name = "ArkInterior torus", vertices = v, normals = n, triangles = tri, hideFlags = HideFlags.DontSave };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
