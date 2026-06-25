using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        void OnGUI()
        {
            if (_missionFinaleActive)
            {
                DrawMissionFinaleHud();
                return;
            }

            if (!_isRunning || _turnFinished)
                return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.18f, 1f, 0.45f, 0.88f) }
            };

            float delta = CurrentFilament.TransferDistance - _distanceOnFilament;
            float latchWindow = CurrentLatchWindow();
            string latchState = CurrentLatchHudState(delta, latchWindow);
            string fakeMath =
                $"BULK NAV  INT psi({Intensity})  t={_elapsedTime:000.0}\n" +
                $"chain {_successfulTransfers:00}/{_targetTransfers:00}  latch={latchState}  eps={latchWindow:0.0}\n" +
                $"lambda transfer d={delta:00.0}  omega={_speed:00.0}  RT front / LT rear\n" +
                $"chi crystals={_crystalsCollected:00}  eta swarm={Mathf.Max(0f, PlayerRouteDistance - _naniteRouteDistance):000.0}\n" +
                $"proj: rho^2 + theta' -> latch ring convergence";

            GUI.Label(new Rect(24, 22, 540, 120), fakeMath, style);
        }

        void DrawMissionFinaleHud()
        {
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_missionFinaleTimer / 1.2f));
            float flicker = 0.82f + Mathf.Sin(Time.time * 22f) * 0.08f + _missionFinaleHudPulse * 0.1f;
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 38,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.15f, 1f, 0.45f, alpha * flicker) }
            };
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.12f, 0.92f, 0.38f, alpha * 0.94f) }
            };

            float x = Mathf.Max(28f, Screen.width * 0.065f);
            float y = Mathf.Max(36f, Screen.height * 0.1f);
            GUI.Label(new Rect(x, y, 720, 58), "MISSION ACCOMPLISHED", titleStyle);
            GUI.Label(
                new Rect(x, y + 62f, 760, 180),
                "ARK Route Secured\n" +
                "Pathfinder Rank Increment +1\n" +
                "Temporal Field: Normalized\n" +
                "Rendezvous with Ark at Checkpoint Gamma",
                bodyStyle);
        }

        void AnimateWormhole()
        {
            if (_tubeRings.Count == 0)
                return;

            float beat = Mathf.Max(BeatPulse(), _waveformEnergy * 0.72f);
            float finale = FinaleIntensity01;
            float time = Time.time;
            SetMaterialFloat(_tubeMaterial, "_Pulse", beat + finale * 0.65f);
            SetMaterialFloat(_activeFilamentMaterial, "_Pulse", beat * 1.35f + finale);
            SetMaterialFloat(_nextFilamentMaterial, "_Pulse", beat);
            SetMaterialFloat(_whiteEnergyMaterial, "_Pulse", beat * 0.75f);
            SetMaterialFloat(_crystalMaterial, "_Pulse", beat * 1.6f);
            SetMaterialFloat(_gateMaterial, "_Pulse", beat * 2.1f + finale);
            SetMaterialFloat(_glyphMaterial, "_Pulse", beat * 0.8f + finale * 0.45f);
            for (int i = 0; i < _tubeRings.Count; i++)
            {
                var ring = _tubeRings[i];
                float y = ring.GetPosition(0).y;
                for (int j = 0; j < ring.positionCount; j++)
                {
                    float a = j / (float)(ring.positionCount - 1) * Mathf.PI * 2f;
                    float undulation = Mathf.Sin(a * 6f + time * (1.8f + finale * 1.1f) + i * 0.27f) * tubeRadius * Mathf.Lerp(0.035f, 0.062f, finale);
                    float beatSurge = beat * Mathf.Sin(a * 3f + i) * tubeRadius * Mathf.Lerp(0.018f, 0.046f, finale);
                    float radius = tubeRadius + undulation + beatSurge;
                    ring.SetPosition(j, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
                }
            }
        }

        void AnimateMirrorWall()
        {
            if (!_mirrorWallMaterial)
                return;

            float pulse = Mathf.Max(BeatPulse(), _waveformEnergy * 0.65f);
            SetMaterialFloat(_mirrorWallMaterial, "_Pulse", pulse + FinaleIntensity01);
            SetMaterialFloat(_mirrorWallMaterial, "_Distortion", Mathf.Lerp(0.42f, 1.1f, FinaleIntensity01));
            RefreshMirrorProbeIfNeeded();
        }

        void RefreshMirrorProbeIfNeeded()
        {
            if (!_mirrorReflectionProbe || Time.time < _nextMirrorProbeRefreshTime)
                return;

            Vector3 probePosition = _vessel?.Transform.position ?? (_filaments.Count > 0 ? CurrentFilament.Center : Vector3.zero);
            _mirrorReflectionProbe.transform.position = probePosition;
            _mirrorReflectionProbe.RenderProbe();
            _nextMirrorProbeRefreshTime = Time.time + Mathf.Lerp(0.9f, 0.38f, FinaleIntensity01);
        }

        void AnimateFilamentColors()
        {
            if (_filaments.Count == 0)
                return;

            for (int i = 0; i < _filaments.Count; i++)
            {
                Material material = _whiteEnergyMaterial;
                if (i == _currentFilamentIndex)
                    material = _activeFilamentMaterial;
                else if (i == _currentFilamentIndex + 1)
                    material = UpdateNextFilamentMaterial();

                if (_filaments[i].Beam)
                    _filaments[i].Beam.material = material;
            }
        }

        Material UpdateNextFilamentMaterial()
        {
            float transitionSpan = Mathf.Max(CurrentFilament.TransferDistance * 0.45f, CurrentLatchWindow() * 5f);
            float closeness = 1f - Mathf.Clamp01(Mathf.Abs(_distanceOnFilament - CurrentFilament.TransferDistance) / transitionSpan);
            closeness = Mathf.SmoothStep(0f, 1f, closeness);
            float hue = Mathf.Lerp(0f, 0.34f, closeness);
            Color ramp = Color.HSVToRGB(hue, Mathf.Lerp(1f, 0.65f, closeness), 1f);
            Color color = closeness <= 0.02f ? new Color(0.85f, 1f, 1f, 1f) : Color.Lerp(Color.white, ramp, Mathf.Clamp01(closeness * 1.35f));
            SetMaterialColor(_nextFilamentMaterial, color);
            return _nextFilamentMaterial;
        }

        void AnimateNanites()
        {
            if (_nanites.Count == 0 || _filaments.Count == 0)
                return;

            float visualRoute = Mathf.Max(0f, _naniteRouteDistance);
            if (_isRunning)
            {
                float desiredTail = Mathf.Max(8f, naniteVisualTailDistance);
                visualRoute = Mathf.Clamp(
                    _naniteRouteDistance,
                    PlayerRouteDistance - naniteCatchBuffer * 1.18f,
                    PlayerRouteDistance - desiredTail);
            }

            FilamentRuntime filament = FilamentAtRouteDistance(Mathf.Max(0f, visualRoute), out float localDistance);
            if (filament == null)
                return;

            Vector3 swarmCenter = PositionOnFilament(filament, localDistance, Time.time * 0.74f, orbitRadius + 7f);
            for (int i = 0; i < _nanites.Count; i++)
            {
                if (i < _naniteRespawnTimers.Count && _naniteRespawnTimers[i] > 0f)
                {
                    _naniteRespawnTimers[i] = Mathf.Max(0f, _naniteRespawnTimers[i] - Time.deltaTime);
                    if (_nanites[i].activeSelf)
                        _nanites[i].SetActive(false);
                    continue;
                }

                if (!_nanites[i].activeSelf)
                    _nanites[i].SetActive(true);

                float angle = (i / (float)_nanites.Count) * Mathf.PI * 2f + Time.time * (1.35f + i * 0.018f);
                float swarmDistance = localDistance - i * 0.42f + Mathf.Sin(Time.time * 1.7f + i) * 1.8f;
                float radius = orbitRadius + 5.2f + Mathf.Sin(Time.time * 2.1f + i) * 3.5f;
                Vector3 position = PositionOnFilament(filament, swarmDistance, angle, radius);
                _nanites[i].transform.position = position;
                _nanites[i].transform.localScale = Vector3.one * (1f + BeatPulse() * 0.16f);
                _nanites[i].transform.Rotate(180f * Time.deltaTime, 230f * Time.deltaTime, 140f * Time.deltaTime);
                if (i == 0)
                    swarmCenter = position;
            }

            UpdateNaniteWake(swarmCenter);
        }

        void UpdateNaniteWake(Vector3 swarmCenter)
        {
            if (!_naniteWakeLine || _vessel == null)
                return;

            _naniteWakeLine.gameObject.SetActive(_isRunning || _missionFinaleActive);
            Vector3 vesselPosition = _vessel.Transform.position;
            for (int i = 0; i < _naniteWakeLine.positionCount; i++)
            {
                float t = i / (float)(_naniteWakeLine.positionCount - 1);
                Vector3 p = Vector3.Lerp(swarmCenter, vesselPosition, t);
                Vector3 jitter = Random.onUnitSphere * Mathf.Sin(Time.time * 8f + i) * (1f - t) * 1.6f;
                _naniteWakeLine.SetPosition(i, p + jitter);
            }

            float gap = Mathf.Max(0f, PlayerRouteDistance - _naniteRouteDistance);
            float danger = Mathf.Clamp01((naniteCatchBuffer * 1.5f - gap) / Mathf.Max(1f, naniteCatchBuffer));
            _naniteWakeLine.widthMultiplier = Mathf.Lerp(0.28f, 1.1f, danger + BeatPulse() * 0.2f);
        }

        void AnimatePulseGates()
        {
            if (_pulseGates.Count == 0)
                return;

            for (int i = 0; i < _pulseGates.Count; i++)
            {
                PulseGateRuntime gate = _pulseGates[i];
                if (gate.Filament == null)
                    continue;

                gate.PulseTimer = Mathf.Max(0f, gate.PulseTimer - Time.deltaTime);
                _pulseGates[i] = gate;
                Vector3 center = AttachPoint(gate.Filament, gate.Distance);
                float beat = BeatPulse();
                float triggerPulse = gate.PulseTimer > 0f ? Mathf.Sin((1f - gate.PulseTimer / 0.78f) * Mathf.PI) : 0f;
                float radius = orbitRadius * (1.36f + beat * 0.08f + triggerPulse * 0.45f);
                float width = Mathf.Max(0.18f, tubeRadius * 0.0013f) * (1f + triggerPulse * 2f);

                if (gate.Ring)
                {
                    gate.Ring.widthMultiplier = width;
                    UpdateRing(gate.Ring, center, gate.Filament.Direction, gate.Filament.Up, gate.Filament.Side, radius);
                    gate.Ring.gameObject.SetActive(!gate.Triggered || gate.PulseTimer > 0f);
                }

                if (gate.Core)
                {
                    gate.Core.widthMultiplier = width * 0.54f;
                    UpdateGateCore(gate.Core, center, gate.Filament, radius * 0.72f, beat + triggerPulse);
                    gate.Core.gameObject.SetActive(!gate.Triggered || gate.PulseTimer > 0f);
                }
            }
        }

        void UpdateGateCore(LineRenderer core, Vector3 center, FilamentRuntime filament, float radius, float pulse)
        {
            for (int i = 0; i < core.positionCount; i++)
            {
                float a = i / (float)(core.positionCount - 1) * Mathf.PI * 2f;
                float weave = Mathf.Sin(a * 5f + Time.time * 5f) * pulse * 0.18f;
                Vector3 p = center
                            + filament.Up * Mathf.Cos(a) * (radius * (0.72f + weave))
                            + filament.Side * Mathf.Sin(a) * (radius * (1.08f - weave));
                core.SetPosition(i, p);
            }
        }

        void AnimateGlyphSprites()
        {
            if (_glyphSprites.Count == 0)
                return;

            Vector3 cameraPosition = _mainCamera ? _mainCamera.transform.position : Vector3.zero;
            for (int i = 0; i < _glyphSprites.Count; i++)
            {
                GlyphSpriteRuntime glyph = _glyphSprites[i];
                if (glyph.Transform == null)
                    continue;

                if (glyph.Anchor == GlyphAnchorKind.Filament)
                {
                    if (glyph.Filament == null)
                        continue;

                    float drift = Mathf.Sin(Time.time * 0.33f + glyph.Phase) * 2.5f;
                    float distance = Mathf.Clamp(glyph.Distance + drift, 0f, glyph.Filament.TravelLength);
                    Vector3 attach = AttachPoint(glyph.Filament, distance);
                    float angle = glyph.OrbitAngleRadians + Time.time * 0.18f;
                    Vector3 normal = (glyph.Filament.Up * Mathf.Cos(angle) + glyph.Filament.Side * Mathf.Sin(angle)).normalized;
                    glyph.Transform.position = attach + normal * 1.65f;
                    glyph.Transform.rotation = Quaternion.LookRotation(normal, glyph.Filament.Direction);
                    float pulse = 1f + Mathf.Sin(Time.time * 2.2f + glyph.Phase) * 0.11f + BeatPulse() * 0.08f;
                    glyph.Transform.localScale = new Vector3(glyph.BaseScale.x, glyph.BaseScale.y * pulse, 1f);
                    continue;
                }

                if (glyph.Anchor == GlyphAnchorKind.LatchRing)
                {
                    if (glyph.RingIndex < 0 || glyph.RingIndex >= _latchRings.Count || !_latchRings[glyph.RingIndex])
                    {
                        glyph.Transform.gameObject.SetActive(false);
                        continue;
                    }

                    LineRenderer ring = _latchRings[glyph.RingIndex];
                    if (!ring.gameObject.activeInHierarchy)
                    {
                        glyph.Transform.gameObject.SetActive(false);
                        continue;
                    }

                    glyph.Transform.gameObject.SetActive(true);
                    int index = Mathf.Clamp(Mathf.RoundToInt(glyph.Ring01 * (ring.positionCount - 1)), 0, ring.positionCount - 1);
                    Vector3 position = ring.GetPosition(index);
                    Vector3 toCamera = cameraPosition - position;
                    if (toCamera.sqrMagnitude < 0.01f)
                        toCamera = Vector3.up;
                    glyph.Transform.position = position + toCamera.normalized * 0.05f;
                    glyph.Transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
                    float pulse = 1f + BeatPulse() * 0.2f + Mathf.Sin(Time.time * 5f + glyph.Phase) * 0.08f;
                    glyph.Transform.localScale = new Vector3(glyph.BaseScale.x * pulse, glyph.BaseScale.y, 1f);
                }
            }
        }

        void BurstTrailingNanites()
        {
            if (_nanites.Count == 0 || _vessel == null)
                return;

            Vector3 vesselPosition = _vessel.Transform.position;
            int popped = 0;
            for (int pass = 0; pass < _nanites.Count && popped < 3; pass++)
            {
                int index = (pass * 7 + _successfulTransfers) % _nanites.Count;
                if (index < _naniteRespawnTimers.Count && _naniteRespawnTimers[index] > 0f)
                    continue;

                GameObject nanite = _nanites[index];
                if (!nanite || !nanite.activeSelf)
                    continue;

                if (Vector3.Distance(nanite.transform.position, vesselPosition) > orbitRadius * 3.4f)
                    continue;

                SpawnNanitePop(nanite.transform.position);
                _naniteRespawnTimers[index] = Random.Range(0.48f, 0.9f);
                nanite.SetActive(false);
                popped++;
            }

            if (popped > 0)
                PlayNanitePopSound();
        }

        void UpdateLatchRig()
        {
            if (_latchRings.Count < 2 || _tethers.Count < 4 || _vessel == null || _filaments.Count == 0)
                return;

            var filament = CurrentFilament;
            Vector3 attach = AttachPoint(filament, _distanceOnFilament);
            Vector3 frontRing = attach + filament.Direction * 2.8f;
            Vector3 rearRing = attach - filament.Direction * 2.8f;

            if (TryGetNextLatchPose(out Vector3 nextFront, out Vector3 nextRear, out Vector3 nextAxis, out Vector3 nextUp, out Vector3 nextSide))
            {
                if (_frontRingShotTimer > 0f)
                    frontRing = Vector3.Lerp(frontRing, nextFront, 1f - _frontRingShotTimer / 0.35f);
                if (_rearRingShotTimer > 0f)
                    rearRing = Vector3.Lerp(rearRing, nextRear, 1f - _rearRingShotTimer / 0.35f);
                if (_latchState == LatchState.FrontLocked)
                    frontRing = nextFront;

                UpdateRing(_latchRings[0], frontRing, nextAxis, nextUp, nextSide, _latchState == LatchState.FrontLocked ? 1.72f : 1.45f);
                UpdateRing(_latchRings[1], rearRing, _rearRingShotTimer > 0f ? nextAxis : filament.Direction, _rearRingShotTimer > 0f ? nextUp : filament.Up, _rearRingShotTimer > 0f ? nextSide : filament.Side, 1.45f);
            }
            else
            {
                UpdateRing(_latchRings[0], frontRing, filament.Direction, filament.Up, filament.Side, 1.45f);
                UpdateRing(_latchRings[1], rearRing, filament.Direction, filament.Up, filament.Side, 1.45f);
            }

            Transform vesselTransform = _vessel.Transform;
            Vector3[] localEngines =
            {
                new(1.1f, 0.1f, 1.2f),
                new(-1.1f, 0.1f, 1.2f),
                new(1.1f, 0.1f, -1.2f),
                new(-1.1f, 0.1f, -1.2f),
            };

            for (int i = 0; i < _tethers.Count; i++)
            {
                Vector3 engine = vesselTransform.TransformPoint(localEngines[i]);
                Vector3 ringPoint = i < 2 ? frontRing : rearRing;
                ringPoint += (i % 2 == 0 ? filament.Side : -filament.Side) * 0.8f;
                UpdateTether(_tethers[i], engine, ringPoint, filament, i);
            }
        }

        void UpdateTether(LineRenderer tether, Vector3 engine, Vector3 ringPoint, FilamentRuntime filament, int index)
        {
            if (!tether)
                return;

            if (tether.positionCount < 6)
                tether.positionCount = 6;

            float transfer01 = _redirectTimer > 0f
                ? 1f - Mathf.Clamp01(_redirectTimer / RedirectDurationSeconds)
                : 1f;
            float stretch = _redirectTimer > 0f ? DampedTetherStretch(transfer01) : 0f;
            Vector3 span = ringPoint - engine;
            Vector3 sagAxis = Vector3.Cross(span.normalized, filament.Direction);
            if (sagAxis.sqrMagnitude < 0.01f)
                sagAxis = filament.Side;
            sagAxis.Normalize();

            float sideSign = index % 2 == 0 ? 1f : -1f;
            float length = span.magnitude;
            float bow = length * stretch * sideSign;

            for (int i = 0; i < tether.positionCount; i++)
            {
                float t = i / (float)(tether.positionCount - 1);
                float envelope = Mathf.Sin(t * Mathf.PI);
                Vector3 retract = -span.normalized * (length * Mathf.Max(0f, -stretch) * envelope * 0.22f);
                Vector3 curve = sagAxis * bow * envelope;
                tether.SetPosition(i, Vector3.Lerp(engine, ringPoint, t) + curve + retract);
            }
        }

        static float DampedTetherStretch(float t)
        {
            t = Mathf.Clamp01(t);
            float longPull = Mathf.Exp(-3.2f * t) * 0.34f;
            float undershoot = -0.11f * Mathf.Exp(-80f * (t - 0.58f) * (t - 0.58f));
            float settle = 0.025f * Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Exp(-5.5f * t);
            return longPull + undershoot + settle;
        }

        string CurrentLatchHudState(float delta, float latchWindow)
        {
            if (_latchState == LatchState.FrontLocked)
                return $"LT REAR {_frontLatchTimer:0.0}s";
            if (Mathf.Abs(delta) <= latchWindow)
                return "RT FRONT";
            return delta > 0f ? "approach" : "late";
        }

        bool TryGetNextLatchPose(out Vector3 front, out Vector3 rear, out Vector3 axis, out Vector3 up, out Vector3 side)
        {
            front = rear = axis = up = side = Vector3.zero;
            if (_currentFilamentIndex + 1 >= _filaments.Count)
                return false;

            FilamentRuntime next = _filaments[_currentFilamentIndex + 1];
            Vector3 attach = AttachPoint(next, 0f);
            axis = next.Direction;
            up = next.Up;
            side = next.Side;
            front = attach + next.Direction * 2.8f;
            rear = attach - next.Direction * 2.8f;
            return true;
        }

        void UpdateRing(LineRenderer ring, Vector3 center, Vector3 axis, Vector3 up, Vector3 side, float radius)
        {
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i / (float)(ring.positionCount - 1) * Mathf.PI * 2f;
                Vector3 p = center + (up * Mathf.Cos(a) + side * Mathf.Sin(a)) * radius + axis * Mathf.Sin(a * 2f + Time.time * 7f) * 0.12f;
                ring.SetPosition(i, p);
            }
        }

    }
}
