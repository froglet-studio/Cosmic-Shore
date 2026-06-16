using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        void OnGUI()
        {
            if (!_isRunning || _turnFinished)
                return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.18f, 1f, 0.45f, 0.88f) }
            };

            float delta = CurrentFilament.TransferDistance - _distanceOnFilament;
            string fakeMath =
                $"BULK NAV  INT psi({Intensity})  t={_elapsedTime:000.0}\n" +
                $"lambda transfer d={delta:00.0}  omega={_speed:00.0}  eps={CurrentLatchWindow():0.0}\n" +
                $"chi crystals={_crystalsCollected:00}  eta swarm={Mathf.Max(0f, PlayerRouteDistance - _naniteRouteDistance):000.0}\n" +
                $"proj: rho^2 + theta' -> latch ring convergence";

            GUI.Label(new Rect(24, 22, 540, 120), fakeMath, style);
        }

        void AnimateWormhole()
        {
            if (_tubeRings.Count == 0)
                return;

            float beat = BeatPulse();
            float time = Time.time;
            for (int i = 0; i < _tubeRings.Count; i++)
            {
                var ring = _tubeRings[i];
                float y = ring.GetPosition(0).y;
                for (int j = 0; j < ring.positionCount; j++)
                {
                    float a = j / (float)(ring.positionCount - 1) * Mathf.PI * 2f;
                    float undulation = Mathf.Sin(a * 6f + time * 1.8f + i * 0.27f) * tubeRadius * 0.035f;
                    float beatSurge = beat * Mathf.Sin(a * 3f + i) * tubeRadius * 0.018f;
                    float radius = tubeRadius + undulation + beatSurge;
                    ring.SetPosition(j, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
                }
            }
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

            FilamentRuntime filament = FilamentAtRouteDistance(Mathf.Max(0f, _naniteRouteDistance), out float localDistance);
            if (filament == null)
                return;

            for (int i = 0; i < _nanites.Count; i++)
            {
                float angle = (i / (float)_nanites.Count) * Mathf.PI * 2f + Time.time * (0.7f + i * 0.01f);
                float swarmDistance = localDistance - i * 0.8f;
                _nanites[i].transform.position = PositionOnFilament(filament, swarmDistance, angle, orbitRadius + 8f + Mathf.Sin(Time.time + i) * 2f);
                _nanites[i].transform.Rotate(90f * Time.deltaTime, 130f * Time.deltaTime, 70f * Time.deltaTime);
            }
        }

        void UpdateLatchRig()
        {
            if (_latchRings.Count < 2 || _tethers.Count < 4 || _vessel == null || _filaments.Count == 0)
                return;

            var filament = CurrentFilament;
            Vector3 attach = AttachPoint(filament, _distanceOnFilament);
            Vector3 frontRing = attach + filament.Direction * 2.8f;
            Vector3 rearRing = attach - filament.Direction * 2.8f;
            UpdateRing(_latchRings[0], frontRing, filament.Direction, filament.Up, filament.Side, 1.45f);
            UpdateRing(_latchRings[1], rearRing, filament.Direction, filament.Up, filament.Side, 1.45f);

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
                _tethers[i].SetPosition(0, engine);
                _tethers[i].SetPosition(1, ringPoint);
            }
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

        void StartMusic()
        {
            if (!_runtimeRoot)
                return;

            _musicSource = _runtimeRoot.GetComponent<AudioSource>();
            if (!_musicSource)
                _musicSource = _runtimeRoot.AddComponent<AudioSource>();

            if (!_musicSource.clip)
                _musicSource.clip = Resources.Load<AudioClip>(MusicResourcePath);

            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.pitch = 1f;
            _musicSource.volume = 0.8f;

            if (_musicSource.clip)
                _musicSource.Play();
            else
                CSDebug.LogWarning($"[BulkFilaments] Missing music resource at Resources/{MusicResourcePath}.");
        }

        float BeatPulse()
        {
            float sourceTime = _musicSource && _musicSource.isPlaying ? _musicSource.time : Time.time;
            float beat = sourceTime * (musicBpm / 60f);
            float phase = beat - Mathf.Floor(beat);
            return Mathf.Pow(1f - phase, 5f);
        }
    }
}
