using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        void CreateMaterials()
        {
            _activeFilamentMaterial = MakeMaterial("Bulk Active Filament", new Color(0.1f, 1f, 0.25f, 1f));
            _nextFilamentMaterial = MakeMaterial("Bulk Next Filament", Color.white);
            _whiteEnergyMaterial = MakeMaterial("Bulk White Energy", new Color(0.85f, 1f, 1f, 1f));
            _tubeMaterial = MakeMaterial("Bulk Refractive Tube", new Color(0.28f, 0.72f, 1f, 0.42f));
            _crystalMaterial = MakeMaterial("Bulk Crystal", new Color(0.95f, 0.35f, 1f, 1f));
            _hazardMaterial = MakeMaterial("Bulk Flora/Fauna", new Color(1f, 0.38f, 0.22f, 0.9f));
            _naniteMaterial = MakeMaterial("Bulk Nanites", new Color(0.05f, 0.95f, 0.75f, 1f));
        }

        int ResolveTargetTransferCount()
        {
            int configuredCount = intensityOneTransfers + (Intensity - 1) * transfersAddedPerIntensity;
            AudioClip clip = Resources.Load<AudioClip>(MusicResourcePath);
            if (clip)
                configuredCount = Mathf.Max(configuredCount, Mathf.RoundToInt(clip.length / targetSecondsPerTransfer));

            return Mathf.Clamp(configuredCount, minMusicTransfers, maxMusicTransfers);
        }

        Material MakeMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = materialName };
            SetMaterialColor(material, color);
            return material;
        }

        static void SetMaterialColor(Material material, Color color)
        {
            if (!material)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.8f);
            }
        }

        void CreateWormhole()
        {
            float length = _targetTransfers * (filamentRisePerTransfer + tubeRadius * 0.095f) + tubeRadius * 1.5f;
            float lineWidth = Mathf.Max(0.32f, tubeRadius * 0.004f);
            for (int i = 0; i < tubeRingCount; i++)
            {
                float normalized = i / Mathf.Max(1f, tubeRingCount - 1f);
                float y = Mathf.Lerp(-tubeRadius * 0.65f, length, normalized);
                var ring = MakeLine($"Wormhole Reflective Cloud Ring {i:00}", tubeRingResolution + 1, lineWidth, _tubeMaterial);

                for (int j = 0; j <= tubeRingResolution; j++)
                {
                    float a = j / (float)tubeRingResolution * Mathf.PI * 2f;
                    float bump = Mathf.PerlinNoise(i * 0.37f, j * 0.11f) * tubeRadius * 0.035f;
                    float radius = tubeRadius + bump + Mathf.Sin(a * 5f + i * 0.41f) * tubeRadius * 0.018f;
                    ring.SetPosition(j, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
                }

                _tubeRings.Add(ring);
            }
        }

        void CreateFilaments()
        {
            int count = _targetTransfers + 1;
            var random = new System.Random(7103 + Intensity * 997);
            FilamentRuntime previous = null;
            float routeStart = 0f;
            for (int i = 0; i < count; i++)
            {
                float length = SampleFilamentLength(random);
                float travelLength = length * FilamentTravelRatio;
                Vector3 start = previous == null ? FirstFilamentStart(random) : NextFilamentStart(previous, random);
                Vector3 direction = FilamentDirectionFromStart(start, length, random);
                Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
                if (side.sqrMagnitude < 0.01f)
                    side = Vector3.right;

                var filament = new FilamentRuntime
                {
                    Index = i,
                    Center = start + direction * (travelLength * 0.5f),
                    Direction = direction,
                    Side = side,
                    Up = Vector3.Cross(direction, side).normalized,
                    Length = length,
                    TravelLength = travelLength,
                    TransferDistance = travelLength * TransferDistanceRatio,
                    RouteStartDistance = routeStart,
                };
                filament.Beam = MakeFilamentBeam(filament);

                CreateRootFlares(filament);
                CreateCrystals(filament);
                CreateHazards(filament);
                _filaments.Add(filament);
                previous = filament;
                routeStart += travelLength;
            }
        }

        LineRenderer MakeFilamentBeam(FilamentRuntime filament)
        {
            float width = Mathf.Max(0.72f, tubeRadius * 0.0032f);
            var beam = MakeLine($"Filament {filament.Index:00}", 9, width, _whiteEnergyMaterial);
            for (int i = 0; i < 9; i++)
            {
                float t = Mathf.Lerp(-0.5f, 0.5f, i / 8f);
                Vector3 ripple = Vector3.up * Mathf.Sin((filament.Index + i) * 1.7f) * tubeRadius * 0.002f;
                beam.SetPosition(i, filament.Center + filament.Direction * (t * filament.Length) + ripple);
            }
            return beam;
        }

        void CreateRootFlares(FilamentRuntime filament)
        {
            Vector3 left = filament.Center - filament.Direction * (filament.Length * 0.5f);
            Vector3 right = filament.Center + filament.Direction * (filament.Length * 0.5f);
            CreateRootFlare($"{filament.Index:00} Root A", left, -filament.Direction, filament.Side, filament.Up);
            CreateRootFlare($"{filament.Index:00} Root B", right, filament.Direction, filament.Side, filament.Up);
        }

        void CreateRootFlare(string flareName, Vector3 endpoint, Vector3 outward, Vector3 side, Vector3 up)
        {
            for (int i = -2; i <= 2; i++)
            {
                float flareWidth = Mathf.Max(0.22f, tubeRadius * 0.0015f);
                float flareLength = Mathf.Max(8f, tubeRadius * 0.035f);
                var line = MakeLine($"Filament {flareName} {i}", 4, flareWidth, _whiteEnergyMaterial);
                Vector3 spread = (outward * 5f + side * i * 2.2f + up * Mathf.Abs(i) * 1.2f).normalized;
                for (int p = 0; p < 4; p++)
                {
                    float t = p / 3f;
                    line.SetPosition(p, endpoint + spread * (t * flareLength) + up * (Mathf.Sin(t * Mathf.PI) * flareLength * 0.2f));
                }
            }
        }

        void CreateCrystals(FilamentRuntime filament)
        {
            int crystals = 2 + (filament.Index + Intensity) % 3;
            for (int i = 0; i < crystals; i++)
            {
                float distance = Mathf.Lerp(8f, filament.TravelLength - 10f, (i + 1f) / (crystals + 1f));
                float angle = (filament.Index * 51f + i * 137f) * Mathf.Deg2Rad;
                Vector3 position = PositionOnFilament(filament, distance, angle, orbitRadius + 1.2f);

                var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crystal.name = $"Bulk Crystal {filament.Index:00}-{i:00}";
                crystal.transform.SetParent(_runtimeRoot.transform, false);
                crystal.transform.position = position;
                crystal.transform.localScale = Vector3.one * 1.4f;
                crystal.GetComponent<Renderer>().sharedMaterial = _crystalMaterial;
                Destroy(crystal.GetComponent<Collider>());

                filament.Crystals.Add(new CrystalRuntime { GameObject = crystal, Position = position });
            }
        }

        void CreateHazards(FilamentRuntime filament)
        {
            if (filament.Index < 3)
                return;

            int hazards = Mathf.Clamp(Intensity - 1, 1, 3);
            for (int i = 0; i < hazards; i++)
            {
                float distance = Mathf.Lerp(12f, filament.TravelLength - 8f, (i + 0.5f) / hazards);
                float angle = (filament.Index * 83f + i * 91f) * Mathf.Deg2Rad;
                Vector3 position = PositionOnFilament(filament, distance, angle, orbitRadius + 2.8f);

                var hazard = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Capsule : PrimitiveType.Sphere);
                hazard.name = $"Bulk Ambient Lifeform {filament.Index:00}-{i:00}";
                hazard.transform.SetParent(_runtimeRoot.transform, false);
                hazard.transform.position = position;
                hazard.transform.localScale = new Vector3(1.5f, 4.2f, 1.5f);
                hazard.GetComponent<Renderer>().sharedMaterial = _hazardMaterial;
                Destroy(hazard.GetComponent<Collider>());
                _hazards.Add(hazard);
            }
        }

        void CreateLatchRig()
        {
            for (int i = 0; i < 4; i++)
                _tethers.Add(MakeLine($"Latch Tether {i}", 2, 0.13f, _activeFilamentMaterial));

            _latchRings.Add(MakeLine("Front Latch Ring", 49, 0.18f, _activeFilamentMaterial));
            _latchRings.Add(MakeLine("Rear Latch Ring", 49, 0.18f, _activeFilamentMaterial));
        }

        void CreateNaniteSwarm()
        {
            for (int i = 0; i < 18; i++)
            {
                var nanite = new GameObject($"Filament Nanite {i:00}");
                nanite.name = $"Filament Nanite {i:00}";
                nanite.transform.SetParent(_runtimeRoot.transform, false);

                float scale = Random.Range(0.55f, 1.2f);
                CreateNanitePart(nanite.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * scale, _naniteMaterial);
                CreateNanitePart(nanite.transform, PrimitiveType.Cube, new Vector3(0f, 0f, -0.75f * scale), new Vector3(0.22f, 0.22f, 1.25f) * scale, _activeFilamentMaterial);
                CreateNanitePart(nanite.transform, PrimitiveType.Cube, new Vector3(0.55f * scale, 0f, 0.12f * scale), new Vector3(0.16f, 0.62f, 0.9f) * scale, _hazardMaterial);
                CreateNanitePart(nanite.transform, PrimitiveType.Cube, new Vector3(-0.55f * scale, 0f, 0.12f * scale), new Vector3(0.16f, 0.62f, 0.9f) * scale, _hazardMaterial);
                _nanites.Add(nanite);
            }
        }

        void CreateNanitePart(Transform parent, PrimitiveType shape, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
        }

        LineRenderer MakeLine(string lineName, int positions, float width, Material material)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(_runtimeRoot.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = positions;
            line.widthMultiplier = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 6;
            line.material = material;
            return line;
        }
    }
}
