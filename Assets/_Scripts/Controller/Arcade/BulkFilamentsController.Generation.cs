using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

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
            _lightningMaterial = MakeMaterial("Bulk Lightning", new Color(0.75f, 1f, 1f, 1f));
            _mirrorWallMaterial = MakeMirrorWallMaterial();
            _gateMaterial = MakeMaterial("Bulk Pulse Gate", new Color(0.25f, 0.82f, 1f, 0.9f));
            _shardMaterial = MakeMaterial("Bulk Speed Shards", new Color(1f, 0.56f, 1f, 0.92f));
            _glyphMaterial = MakeGlyphMaterial();
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
            Shader shader = Shader.Find("CosmicShore/BulkEnergyUnlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = materialName };
            SetMaterialColor(material, color);
            SetMaterialFloat(material, "_Alpha", color.a);
            return material;
        }

        Material MakeGlyphMaterial()
        {
            Shader shader = Shader.Find("CosmicShore/BulkGlyphSprite")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "Bulk Dark Animated Glyphs" };
            SetMaterialColor(material, new Color(0.015f, 0.035f, 0.052f, 0.88f));
            if (material.HasProperty("_AccentColor"))
                material.SetColor("_AccentColor", new Color(0.04f, 0.95f, 1f, 0.72f));
            if (material.HasProperty("_DarkColor"))
                material.SetColor("_DarkColor", new Color(0f, 0.004f, 0.012f, 0.96f));
            SetMaterialFloat(material, "_Alpha", 0.84f);
            return material;
        }

        Material MakeMirrorWallMaterial()
        {
            Shader shader = Shader.Find("CosmicShore/BulkVoronoiMirror")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "Bulk Voronoi Mirror Wall" };
            SetMaterialColor(material, new Color(0.025f, 0.13f, 0.24f, 0.82f));
            if (material.HasProperty("_LineColor"))
                material.SetColor("_LineColor", new Color(0.02f, 0.92f, 1f, 1f));
            SetMaterialFloat(material, "_Alpha", 0.82f);
            SetMaterialFloat(material, "_MirrorStrength", 0.48f);
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

        static void SetMaterialFloat(Material material, string property, float value)
        {
            if (material && material.HasProperty(property))
                material.SetFloat(property, value);
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

        void CreateMirrorWall()
        {
            if (!_runtimeRoot)
                return;

            float length = _targetTransfers * (filamentRisePerTransfer + tubeRadius * 0.095f) + tubeRadius * 1.5f;
            var wall = new GameObject("Bulk Voronoi Hall Of Mirrors");
            wall.transform.SetParent(_runtimeRoot.transform, false);
            var filter = wall.AddComponent<MeshFilter>();
            var renderer = wall.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _mirrorWallMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            filter.sharedMesh = BuildMirrorWallMesh(length);
        }

        void CreateMirrorReflectionProbe()
        {
            if (!_runtimeRoot)
                return;

            float length = _targetTransfers * (filamentRisePerTransfer + tubeRadius * 0.095f) + tubeRadius * 1.5f;
            var probeObject = new GameObject("Bulk Realtime Mirror Probe");
            probeObject.transform.SetParent(_runtimeRoot.transform, false);
            probeObject.transform.position = Vector3.up * (length * 0.42f);
            _mirrorReflectionProbe = probeObject.AddComponent<ReflectionProbe>();
            _mirrorReflectionProbe.mode = ReflectionProbeMode.Realtime;
            _mirrorReflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _mirrorReflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            _mirrorReflectionProbe.resolution = 64;
            _mirrorReflectionProbe.intensity = 0.62f;
            _mirrorReflectionProbe.size = new Vector3(tubeRadius * 2.55f, length * 1.08f, tubeRadius * 2.55f);
            _mirrorReflectionProbe.nearClipPlane = 0.25f;
            _mirrorReflectionProbe.farClipPlane = tubeRadius * 3.4f;
            _mirrorReflectionProbe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            _mirrorReflectionProbe.backgroundColor = new Color(0.005f, 0.002f, 0.014f, 1f);
        }

        Mesh BuildMirrorWallMesh(float length)
        {
            int rings = Mathf.Max(3, mirrorWallRingCount);
            int segments = Mathf.Max(8, mirrorWallSegments);
            float radius = tubeRadius * 1.075f;
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rings * segments * 6];

            for (int yIndex = 0; yIndex <= rings; yIndex++)
            {
                float y01 = yIndex / (float)rings;
                float y = Mathf.Lerp(-tubeRadius * 0.65f, length, y01);
                for (int xIndex = 0; xIndex <= segments; xIndex++)
                {
                    float x01 = xIndex / (float)segments;
                    float angle = x01 * Mathf.PI * 2f;
                    float facet = Mathf.PerlinNoise(xIndex * 0.17f, yIndex * 0.23f) - 0.5f;
                    float localRadius = radius + facet * tubeRadius * 0.028f;
                    int index = yIndex * (segments + 1) + xIndex;
                    Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices[index] = radial * localRadius + Vector3.up * y;
                    normals[index] = (-radial + Vector3.up * facet * 0.15f).normalized;
                    uv[index] = new Vector2(x01 * 8f, y01 * 18f);
                }
            }

            int ti = 0;
            for (int yIndex = 0; yIndex < rings; yIndex++)
            {
                for (int xIndex = 0; xIndex < segments; xIndex++)
                {
                    int a = yIndex * (segments + 1) + xIndex;
                    int b = a + 1;
                    int c = a + segments + 1;
                    int d = c + 1;
                    triangles[ti++] = a;
                    triangles[ti++] = c;
                    triangles[ti++] = b;
                    triangles[ti++] = b;
                    triangles[ti++] = c;
                    triangles[ti++] = d;
                }
            }

            var mesh = new Mesh { name = "Bulk_Voronoi_Mirror_Wall" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
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
                ConfigureFilamentMotion(filament, random);
                filament.Beam = MakeFilamentBeam(filament);
                CreateFilamentWaveform(filament);

                CreateRootFlares(filament);
                CreateCrystals(filament);
                CreateHazards(filament);
                CreateFilamentGlyphs(filament, random);
                _filaments.Add(filament);
                previous = filament;
                routeStart += travelLength;
            }
        }

        LineRenderer MakeFilamentBeam(FilamentRuntime filament)
        {
            float width = Mathf.Max(0.72f, tubeRadius * 0.0032f);
            var beam = MakeLine($"Filament {filament.Index:00}", 41, width, _whiteEnergyMaterial);
            UpdateFilamentBeam(filament);
            return beam;
        }

        void UpdateFilamentBeam(FilamentRuntime filament)
        {
            if (filament?.Beam == null)
                return;

            for (int i = 0; i < filament.Beam.positionCount; i++)
            {
                float axis01 = i / (float)(filament.Beam.positionCount - 1);
                filament.Beam.SetPosition(i, FilamentSurfacePoint(filament, axis01));
            }
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
                crystal.transform.localScale = Vector3.one * (1.4f * speedDiamondScaleMultiplier);
                crystal.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
                var filter = crystal.GetComponent<MeshFilter>();
                if (filter)
                    filter.sharedMesh = OctahedronMeshGenerator.Generate(Vector3.one * 0.5f);
                Renderer renderer = crystal.GetComponent<Renderer>();
                Material crystalMaterial = new(_crystalMaterial) { name = $"Bulk Crystal Hue {filament.Index:00}-{i:00}" };
                float hue = Mathf.Repeat(0.11f + filament.Index * 0.137f + i * 0.291f, 1f);
                SetMaterialColor(crystalMaterial, Color.HSVToRGB(hue, 0.9f, 1f));
                renderer.sharedMaterial = crystalMaterial;
                Destroy(crystal.GetComponent<Collider>());
                CreateCrystalGlyphs(crystal.transform, hue, filament.Index, i);

                filament.Crystals.Add(new CrystalRuntime
                {
                    GameObject = crystal,
                    Position = position,
                    Distance = distance,
                    OrbitAngleRadians = angle,
                    HueOffset = hue,
                    Renderer = renderer
                });
            }
        }

        void CreateCrystalGlyphs(Transform crystal, float hue, int filamentIndex, int crystalIndex)
        {
            if (!_glyphMaterial)
                return;

            Vector3[] normals =
            {
                new(0.72f, 0.48f, 0.5f),
                new(-0.62f, 0.54f, 0.57f),
                new(0.16f, -0.72f, 0.68f)
            };

            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 normal = normals[i].normalized;
                GameObject glyph = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glyph.name = $"Crystal Dark Glyph {filamentIndex:00}-{crystalIndex:00}-{i:00}";
                glyph.transform.SetParent(crystal, false);
                glyph.transform.localPosition = normal * 0.53f;
                glyph.transform.localRotation = Quaternion.LookRotation(normal, Vector3.up);
                glyph.transform.localScale = new Vector3(0.34f, 0.16f, 1f) * (1f + i * 0.18f);
                var renderer = glyph.GetComponent<Renderer>();
                renderer.sharedMaterial = _glyphMaterial;
                if (renderer.material.HasProperty("_Phase"))
                    renderer.material.SetFloat("_Phase", hue * 11f + filamentIndex * 0.31f + i);
                Destroy(glyph.GetComponent<Collider>());
            }
        }

        void CreateFilamentGlyphs(FilamentRuntime filament, System.Random random)
        {
            if (!_glyphMaterial)
                return;

            int count = Mathf.Clamp(4 + Intensity, 4, 8);
            for (int i = 0; i < count; i++)
            {
                float distance = Mathf.Lerp(12f, filament.TravelLength - 12f, (i + 0.5f) / count);
                float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                var glyph = CreateGlyphSprite($"Filament Glyph {filament.Index:00}-{i:00}", new Vector2(RandomRange(random, 6f, 12f), RandomRange(random, 2.2f, 4.2f)));
                _glyphSprites.Add(new GlyphSpriteRuntime
                {
                    Transform = glyph.transform,
                    Anchor = GlyphAnchorKind.Filament,
                    Filament = filament,
                    Distance = distance,
                    OrbitAngleRadians = angle,
                    BaseScale = new Vector2(glyph.transform.localScale.x, glyph.transform.localScale.y),
                    Phase = RandomRange(random, 0f, 9f)
                });
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
                _hazardRuntimes.Add(new HazardRuntime
                {
                    GameObject = hazard,
                    Filament = filament,
                    Distance = distance,
                    OrbitAngleRadians = angle,
                    SpinDegreesPerSecond = RandomRange(new System.Random(filament.Index * 9281 + i * 71), 55f, 135f)
                });
            }
        }

        void CreatePulseGates()
        {
            if (_filaments.Count == 0 || _targetTransfers <= 0)
                return;

            int gateCount = Mathf.Max(1, Mathf.FloorToInt(1f / Mathf.Max(0.05f, pulseGateRouteInterval)));
            for (int i = 1; i <= gateCount; i++)
            {
                float route01 = Mathf.Clamp01(i * pulseGateRouteInterval);
                int filamentIndex = Mathf.Clamp(Mathf.RoundToInt(route01 * (_targetTransfers - 1)), 1, _filaments.Count - 2);
                FilamentRuntime filament = _filaments[filamentIndex];
                float distance = Mathf.Lerp(filament.TravelLength * 0.24f, filament.TransferDistance * 0.78f, (i * 0.37f) % 1f);
                var ring = MakeLine($"Pulse Gate {i:00} Ring", 65, Mathf.Max(0.18f, tubeRadius * 0.0013f), _gateMaterial);
                var core = MakeLine($"Pulse Gate {i:00} Core", 33, Mathf.Max(0.1f, tubeRadius * 0.00075f), _activeFilamentMaterial);
                _pulseGates.Add(new PulseGateRuntime
                {
                    Filament = filament,
                    Distance = distance,
                    Ring = ring,
                    Core = core
                });
            }
        }

        void CreateLatchRig()
        {
            for (int i = 0; i < 4; i++)
                _tethers.Add(MakeLine($"Latch Tether {i}", 6, 0.13f, _activeFilamentMaterial));

            _latchRings.Add(MakeLine("Front Latch Ring", 49, 0.18f, _activeFilamentMaterial));
            _latchRings.Add(MakeLine("Rear Latch Ring", 49, 0.18f, _activeFilamentMaterial));
            CreateLatchRingGlyphs();
        }

        void CreateLatchRingGlyphs()
        {
            if (!_glyphMaterial)
                return;

            for (int ring = 0; ring < 2; ring++)
            {
                for (int i = 0; i < 7; i++)
                {
                    var glyph = CreateGlyphSprite($"Latch Ring Glyph {ring}-{i}", new Vector2(1.55f, 0.44f));
                    _glyphSprites.Add(new GlyphSpriteRuntime
                    {
                        Transform = glyph.transform,
                        Anchor = GlyphAnchorKind.LatchRing,
                        RingIndex = ring,
                        Ring01 = (i + 0.5f) / 7f,
                        BaseScale = new Vector2(1.55f, 0.44f),
                        Phase = ring * 1.7f + i * 0.43f
                    });
                }
            }
        }

        GameObject CreateGlyphSprite(string glyphName, Vector2 scale)
        {
            var glyph = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glyph.name = glyphName;
            glyph.transform.SetParent(_runtimeRoot.transform, false);
            glyph.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = glyph.GetComponent<Renderer>();
            renderer.sharedMaterial = _glyphMaterial;
            Destroy(glyph.GetComponent<Collider>());
            return glyph;
        }

        void CreateNaniteSwarm()
        {
            for (int i = 0; i < 26; i++)
            {
                var nanite = new GameObject($"Filament Nanite {i:00}");
                nanite.name = $"Filament Nanite {i:00}";
                nanite.transform.SetParent(_runtimeRoot.transform, false);

                float scale = Random.Range(1.45f, 2.85f);
                CreateNanitePart(nanite.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * scale, _naniteMaterial);
                CreateNanitePart(nanite.transform, PrimitiveType.Cube, new Vector3(0f, 0f, -0.75f * scale), new Vector3(0.22f, 0.22f, 1.25f) * scale, _activeFilamentMaterial);
                CreateNanitePart(nanite.transform, PrimitiveType.Cube, new Vector3(0.55f * scale, 0f, 0.12f * scale), new Vector3(0.16f, 0.62f, 0.9f) * scale, _hazardMaterial);
                CreateNanitePart(nanite.transform, PrimitiveType.Cube, new Vector3(-0.55f * scale, 0f, 0.12f * scale), new Vector3(0.16f, 0.62f, 0.9f) * scale, _hazardMaterial);
                CreateNanitePart(nanite.transform, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.12f * scale), Vector3.one * scale * 1.7f, _glyphMaterial ? _glyphMaterial : _naniteMaterial);
                _nanites.Add(nanite);
                _naniteRespawnTimers.Add(0f);
            }

            _naniteWakeLine = MakeLine("Bulk Nanite Chase Warning Trail", 18, 0.44f, _naniteMaterial);
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
