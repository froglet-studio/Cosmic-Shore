using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        const int BulkSpriteFrameCount = 12;
        const int BulkSpriteColumns = 4;
        const int BulkSpriteRows = 3;
        const string BulkSpriteResourceRoot = "Textures/BulkFilaments/";

        readonly List<BulkSpriteOverlayRuntime> _bulkSpriteOverlays = new();
        Material _filamentFlowSpriteMaterial;
        Material _latchRingSpriteMaterial;
        Material _tetherSpriteMaterial;
        Material _naniteSpriteMaterial;
        Material _powerDiamondSpriteMaterial;
        Material _rootSpriteMaterial;
        Material _faunaSpriteMaterial;

        void LoadBulkSpriteSheets()
        {
            _filamentFlowSpriteMaterial = MakeSpriteSheetMaterial("BulkFilamentFlowSheet", "Bulk Filament Flow Sprites", true);
            _latchRingSpriteMaterial = MakeSpriteSheetMaterial("BulkLatchRingFlareSheet", "Bulk Latch Ring Flare Sprites", true);
            _tetherSpriteMaterial = MakeSpriteSheetMaterial("BulkTetherCrackleSheet", "Bulk Tether Crackle Sprites", true);
            _naniteSpriteMaterial = MakeSpriteSheetMaterial("BulkNaniteInsectSheet", "Bulk Nanite Insect Sprites");
            _powerDiamondSpriteMaterial = MakeSpriteSheetMaterial("BulkPowerDiamondSheet", "Bulk Power Diamond Sprites");
            _rootSpriteMaterial = MakeSpriteSheetMaterial("BulkRootFlareSheet", "Bulk Root Flare Sprites", true);
            _faunaSpriteMaterial = MakeSpriteSheetMaterial("BulkFaunaSquidSheet", "Bulk Fauna Squid Sprites");
        }

        Material MakeSpriteSheetMaterial(string resourceName, string materialName, bool blackToAlpha = false)
        {
            Texture2D texture = Resources.Load<Texture2D>(BulkSpriteResourceRoot + resourceName);
            if (!texture)
            {
                CSDebug.LogWarning($"[BulkFilaments] Missing sprite sheet Resources/{BulkSpriteResourceRoot}{resourceName}.png.");
                return null;
            }

            Shader shader = Shader.Find("CosmicShore/BulkSpriteSheet")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Texture");
            var material = new Material(shader) { name = materialName };
            material.mainTexture = texture;
            SetMaterialFloat(material, "_Columns", BulkSpriteColumns);
            SetMaterialFloat(material, "_Rows", BulkSpriteRows);
            SetMaterialFloat(material, "_Glow", 0.92f);
            SetMaterialFloat(material, "_BlackToAlpha", blackToAlpha ? 1f : 0f);
            return material;
        }

        void ResetBulkSpriteOverlays()
        {
            _bulkSpriteOverlays.Clear();
        }

        void CreateFilamentSpriteOverlays(FilamentRuntime filament, System.Random random)
        {
            if (!_filamentFlowSpriteMaterial)
                return;

            int count = Mathf.Clamp(3 + Intensity, 4, 7);
            for (int i = 0; i < count; i++)
            {
                float distance = Mathf.Lerp(0f, filament.TravelLength, (i + 0.5f) / count);
                float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                var overlay = CreateBulkSpriteOverlay(
                    $"Filament Flow Sprite {filament.Index:00}-{i:00}",
                    _filamentFlowSpriteMaterial,
                    new Vector2(RandomRange(random, 34f, 62f), RandomRange(random, 5.5f, 9.5f)));
                overlay.Anchor = BulkSpriteAnchorKind.Filament;
                overlay.Filament = filament;
                overlay.Distance = distance;
                overlay.OrbitAngleRadians = angle;
                overlay.FrameRate = RandomRange(random, 15f, 24f);
                overlay.Phase = RandomRange(random, 0f, 12f);
                overlay.Tint = new Color(1f, 0.34f, 0.82f, 0.64f);
                overlay.Glow = 1.02f;
                _bulkSpriteOverlays.Add(overlay);
            }
        }

        void CreateRootSpriteOverlay(FilamentRuntime filament, float axis01, int variant)
        {
            if (!_rootSpriteMaterial)
                return;

            var overlay = CreateBulkSpriteOverlay(
                $"Filament Root Sprite {variant:00}",
                _rootSpriteMaterial,
                new Vector2(tubeRadius * 0.11f, tubeRadius * 0.11f));
            overlay.Anchor = BulkSpriteAnchorKind.Root;
            overlay.Filament = filament;
            overlay.RootAxis01 = axis01;
            overlay.FrameRate = 11f + variant % 5;
            overlay.Phase = variant * 0.9f;
            overlay.Tint = new Color(1f, 0.58f, 0.18f, 0.58f);
            overlay.Glow = 0.94f;
            _bulkSpriteOverlays.Add(overlay);
        }

        void CreateCrystalSpriteOverlay(Transform crystal, float hue, int filamentIndex, int crystalIndex)
        {
            if (!_powerDiamondSpriteMaterial)
                return;

            var overlay = CreateBulkSpriteOverlay(
                $"Power Diamond Sprite {filamentIndex:00}-{crystalIndex:00}",
                _powerDiamondSpriteMaterial,
                Vector2.one * 9.5f);
            overlay.Anchor = BulkSpriteAnchorKind.FollowTransform;
            overlay.Follow = crystal;
            overlay.FrameRate = 14f;
            overlay.Phase = crystalIndex * 1.7f;
            overlay.Tint = Color.HSVToRGB(hue, 0.82f, 1f);
            overlay.Tint.a = 0.7f;
            overlay.Glow = 1.1f;
            _bulkSpriteOverlays.Add(overlay);
        }

        void CreateNaniteSpriteOverlay(int index, Transform nanite)
        {
            if (!_naniteSpriteMaterial)
                return;

            var overlay = CreateBulkSpriteOverlay($"Nanite Insect Sprite {index:00}", _naniteSpriteMaterial, Vector2.one * Random.Range(5.2f, 8.4f));
            overlay.Anchor = BulkSpriteAnchorKind.FollowTransform;
            overlay.Follow = nanite;
            overlay.FrameRate = Random.Range(10f, 18f);
            overlay.Phase = index * 0.73f;
            overlay.Tint = new Color(1f, 0.18f, 0.12f, 0.82f);
            overlay.Glow = 0.96f;
            _bulkSpriteOverlays.Add(overlay);
        }

        void CreateFaunaSpriteOverlay(Transform fauna, int index, bool giant)
        {
            if (!_faunaSpriteMaterial)
                return;

            float scale = giant ? Random.Range(19f, 26f) : Random.Range(10f, 15f);
            var overlay = CreateBulkSpriteOverlay($"Bulk Fauna Squid Sprite {index:00}", _faunaSpriteMaterial, Vector2.one * scale);
            overlay.Anchor = BulkSpriteAnchorKind.FollowTransform;
            overlay.Follow = fauna;
            overlay.FrameRate = giant ? 8.5f : 12f;
            overlay.Phase = index * 1.31f;
            overlay.Tint = giant ? new Color(0.64f, 0.46f, 1f, 0.58f) : new Color(1f, 0.52f, 0.24f, 0.5f);
            overlay.Glow = giant ? 0.98f : 0.82f;
            _bulkSpriteOverlays.Add(overlay);
        }

        void CreateLatchSpriteOverlays()
        {
            if (!_latchRingSpriteMaterial)
                return;

            for (int ring = 0; ring < 2; ring++)
            {
                var overlay = CreateBulkSpriteOverlay($"Latch Ring Flare Sprite {ring}", _latchRingSpriteMaterial, Vector2.one * 6.8f);
                overlay.Anchor = BulkSpriteAnchorKind.LatchRing;
                overlay.RingIndex = ring;
                overlay.Ring01 = 0.5f;
                overlay.FrameRate = 18f;
                overlay.Phase = ring * 3f;
                overlay.Tint = new Color(0.78f, 1f, 0.48f, 0.74f);
                overlay.Glow = 1.08f;
                _bulkSpriteOverlays.Add(overlay);
            }
        }

        void CreateTetherSpriteOverlays()
        {
            if (!_tetherSpriteMaterial)
                return;

            for (int tether = 0; tether < _tethers.Count; tether++)
            {
                for (int i = 0; i < 2; i++)
                {
                    var overlay = CreateBulkSpriteOverlay($"Tether Crackle Sprite {tether}-{i}", _tetherSpriteMaterial, new Vector2(3.4f, 7.8f));
                    overlay.Anchor = BulkSpriteAnchorKind.Tether;
                    overlay.TetherIndex = tether;
                    overlay.Tether01 = (i + 0.5f) / 2f;
                    overlay.FrameRate = 20f + i * 5f;
                    overlay.Phase = tether * 0.8f + i * 2f;
                    overlay.Tint = i == 0 ? new Color(0.52f, 0.74f, 1f, 0.62f) : new Color(1f, 0.3f, 0.82f, 0.58f);
                    overlay.Glow = 0.98f;
                    _bulkSpriteOverlays.Add(overlay);
                }
            }
        }

        BulkSpriteOverlayRuntime CreateBulkSpriteOverlay(string name, Material material, Vector2 baseScale)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(_runtimeRoot.transform, false);
            quad.transform.localScale = new Vector3(baseScale.x, baseScale.y, 1f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Destroy(quad.GetComponent<Collider>());
            return new BulkSpriteOverlayRuntime
            {
                Transform = quad.transform,
                Renderer = renderer,
                Block = new MaterialPropertyBlock(),
                BaseScale = baseScale,
                Tint = Color.white,
                Alpha = 1f,
                Glow = 1f,
                FrameRate = 12f
            };
        }

        void AnimateBulkSpriteOverlays()
        {
            if (_bulkSpriteOverlays.Count == 0)
                return;

            for (int i = 0; i < _bulkSpriteOverlays.Count; i++)
            {
                BulkSpriteOverlayRuntime overlay = _bulkSpriteOverlays[i];
                if (overlay.Transform == null || overlay.Renderer == null)
                    continue;

                if (!UpdateBulkSpritePose(overlay))
                {
                    overlay.Transform.gameObject.SetActive(false);
                    continue;
                }

                overlay.Transform.gameObject.SetActive(true);
                ApplyBulkSpriteFrame(overlay);
            }
        }

        bool UpdateBulkSpritePose(BulkSpriteOverlayRuntime overlay)
        {
            return overlay.Anchor switch
            {
                BulkSpriteAnchorKind.Filament => UpdateFilamentSpritePose(overlay),
                BulkSpriteAnchorKind.LatchRing => UpdateRingSpritePose(overlay),
                BulkSpriteAnchorKind.Tether => UpdateTetherSpritePose(overlay),
                BulkSpriteAnchorKind.FollowTransform => UpdateFollowSpritePose(overlay),
                BulkSpriteAnchorKind.Root => UpdateRootSpritePose(overlay),
                _ => false
            };
        }

        bool UpdateFilamentSpritePose(BulkSpriteOverlayRuntime overlay)
        {
            if (overlay.Filament == null)
                return false;

            float distance = Mathf.Repeat(overlay.Distance - Time.time * Mathf.Max(minimumSpeed, _speed) * 1.65f, overlay.Filament.TravelLength);
            Vector3 center = AttachPoint(overlay.Filament, distance);
            Vector3 normal = (overlay.Filament.Up * Mathf.Cos(overlay.OrbitAngleRadians) + overlay.Filament.Side * Mathf.Sin(overlay.OrbitAngleRadians)).normalized;
            overlay.Transform.position = center + normal * 2.2f;
            overlay.Transform.rotation = Quaternion.LookRotation(normal, overlay.Filament.Direction);
            overlay.Transform.localScale = new Vector3(overlay.BaseScale.x, overlay.BaseScale.y * (1f + _waveformEnergy * 0.28f), 1f);
            overlay.Tint = ContrastingFilamentParticleColor(overlay.Filament.Index);
            return true;
        }

        bool UpdateRingSpritePose(BulkSpriteOverlayRuntime overlay)
        {
            if (overlay.RingIndex < 0 || overlay.RingIndex >= _latchRings.Count || !_latchRings[overlay.RingIndex])
                return false;

            LineRenderer ring = _latchRings[overlay.RingIndex];
            int index = Mathf.Clamp(Mathf.RoundToInt(overlay.Ring01 * (ring.positionCount - 1)), 0, ring.positionCount - 1);
            overlay.Transform.position = ring.GetPosition(index);
            FaceCamera(overlay.Transform);
            float pulse = 1f + BeatPulse() * 0.35f + (_latchState == LatchState.FrontLocked ? 0.32f : 0f);
            overlay.Transform.localScale = Vector3.one * overlay.BaseScale.x * pulse;
            return ring.gameObject.activeInHierarchy;
        }

        bool UpdateTetherSpritePose(BulkSpriteOverlayRuntime overlay)
        {
            if (overlay.TetherIndex < 0 || overlay.TetherIndex >= _tethers.Count || !_tethers[overlay.TetherIndex])
                return false;

            float t = Mathf.Repeat(overlay.Tether01 + Time.time * 1.8f, 1f);
            overlay.Transform.position = SampleLineRenderer(_tethers[overlay.TetherIndex], t);
            FaceCamera(overlay.Transform);
            overlay.Transform.localScale = new Vector3(overlay.BaseScale.x, overlay.BaseScale.y * (1f + BeatPulse() * 0.25f), 1f);
            return _tethers[overlay.TetherIndex].gameObject.activeInHierarchy;
        }

        bool UpdateFollowSpritePose(BulkSpriteOverlayRuntime overlay)
        {
            if (!overlay.Follow || !overlay.Follow.gameObject.activeInHierarchy)
                return false;

            overlay.Transform.position = overlay.Follow.position;
            FaceCamera(overlay.Transform);
            float pulse = 1f + BeatPulse() * 0.18f + Mathf.Sin(Time.time * 4f + overlay.Phase) * 0.08f;
            overlay.Transform.localScale = new Vector3(overlay.BaseScale.x, overlay.BaseScale.y, 1f) * pulse;
            return true;
        }

        bool UpdateRootSpritePose(BulkSpriteOverlayRuntime overlay)
        {
            if (overlay.Filament != null)
            {
                overlay.RootPosition = FilamentSurfacePoint(overlay.Filament, overlay.RootAxis01);
                overlay.RootOutward = overlay.RootAxis01 < 0.5f ? -overlay.Filament.Direction : overlay.Filament.Direction;
                overlay.RootUp = overlay.Filament.Up;
            }

            overlay.Transform.position = overlay.RootPosition + overlay.RootOutward * 0.35f;
            overlay.Transform.rotation = Quaternion.LookRotation(overlay.RootOutward, overlay.RootUp);
            float pulse = 1f + BeatPulse() * 0.22f + Mathf.Sin(Time.time * 2.4f + overlay.Phase) * 0.09f;
            overlay.Transform.localScale = new Vector3(overlay.BaseScale.x, overlay.BaseScale.y, 1f) * pulse;
            return true;
        }

        void ApplyBulkSpriteFrame(BulkSpriteOverlayRuntime overlay)
        {
            int frame = Mathf.FloorToInt(Time.time * overlay.FrameRate + overlay.Phase) % BulkSpriteFrameCount;
            overlay.Block.SetFloat("_Frame", frame);
            overlay.Block.SetColor("_TintColor", overlay.Tint);
            overlay.Block.SetFloat("_Alpha", overlay.Alpha);
            overlay.Block.SetFloat("_Glow", overlay.Glow + BeatPulse() * 0.14f);
            overlay.Renderer.SetPropertyBlock(overlay.Block);
        }

    }
}
