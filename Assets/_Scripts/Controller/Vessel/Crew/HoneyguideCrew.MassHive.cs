using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Mass passive (Honeyguide Lab round 8, Garrett 2026-11-01): every <c>massHiveSeconds</c> a hive is planted
    /// somewhere in the cell, at least <c>hiveMinDistance</c> from the hull; flying within <c>hiveReach</c> of it opens hive
    /// mode there (the Ratel Raid Lab, <see cref="OnOmniCrystal"/>'s path). The hull's Mass level speeds the timer
    /// (<c>massHiveLevelGain</c> a level). The hive is marked in the world by a hexagonal frame the pilot flies to, and the
    /// strip's distance counts it as it counts an omnicrystal. It runs on the hull's machine; an AI crew plants none.
    /// </summary>
    public sealed partial class HoneyguideCrew
    {
        const int HiveMarkerSides = 6;
        float _massHiveT;
        bool _massHiveUp;
        Vector3 _massHiveAt;
        LineRenderer[] _hiveMarker;
        GameObject _hiveMarkerRoot;

        /// <summary>True while a Mass hive waits to be reached; <see cref="MassHiveAt"/> is where.</summary>
        internal bool MassHiveUp => _massHiveUp;
        internal Vector3 MassHiveAt => _massHiveAt;

        void ResetMassHive()
        {
            _massHiveT = _cfg ? _cfg.MassHiveSeconds : 0f;
            _massHiveUp = false;
            ShowHiveMarker(false);
        }

        void DestroyMassHive()
        {
            if (_hiveMarkerRoot) Destroy(_hiveMarkerRoot);
            _hiveMarkerRoot = null;
            _hiveMarker = null;
            _massHiveUp = false;
        }

        void StepMassHive(float dt)
        {
            if (_aiCrew || !_cfg.MassHive || !_cfg.HiveMode || _hive != null) { ShowHiveMarker(false); return; }
            var pos = _vessel.Transform.position;
            if (!_massHiveUp)
            {
                int level = _status?.ResourceSystem != null ? _status.ResourceSystem.GetLevel(Element.Mass) : 0;
                _massHiveT -= dt * (1f + _cfg.MassHiveLevelGain * level);
                if (_massHiveT <= 0f) PlantMassHive(pos);
                return;
            }
            if ((pos - _massHiveAt).sqrMagnitude < _cfg.HiveReach * _cfg.HiveReach)
            {
                _massHiveUp = false;
                _massHiveT = _cfg.MassHiveSeconds;
                ShowHiveMarker(false);
                OnOmniCrystal(_massHiveAt);
                return;
            }
            DrawHiveMarker();
        }

        // Somewhere in the hull's cell (the cytoplasm), at least hiveMinDistance away; the last try stands if none is.
        void PlantMassHive(Vector3 from)
        {
            var cell = CellAt(from);
            Vector3 centre = cell ? cell.transform.position : from;
            float r = cell ? cell.MembraneRadius * 0.8f : _cfg.HiveMinDistance * 2f;
            var at = centre;
            for (int k = 0; k < 40; k++)
            {
                at = centre + Random.insideUnitSphere * r;
                if ((at - from).sqrMagnitude >= _cfg.HiveMinDistance * _cfg.HiveMinDistance) break;
            }
            _massHiveAt = at;
            _massHiveUp = true;
        }

        // A hexagonal frame (two hexes and their struts), turning slowly: a 3D mark in the world, not a ring on the screen.
        void DrawHiveMarker()
        {
            if (!EnsureHiveMarker()) return;
            float s = _cfg.HiveMarkerSize, spin = Time.time * 0.6f;
            var rot = Quaternion.Euler(0f, spin * Mathf.Rad2Deg, 0f);
            for (int h = 0; h < 2; h++)
            {
                var line = _hiveMarker[h];
                float y = (h == 0 ? 0.5f : -0.5f) * s;
                line.positionCount = HiveMarkerSides;
                for (int i = 0; i < HiveMarkerSides; i++)
                {
                    float a = i * Mathf.PI * 2f / HiveMarkerSides;
                    line.SetPosition(i, _massHiveAt + rot * new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s));
                }
            }
            var struts = _hiveMarker[2];
            struts.positionCount = HiveMarkerSides * 3;
            for (int i = 0; i < HiveMarkerSides; i++)
            {
                float a = i * Mathf.PI * 2f / HiveMarkerSides;
                var top = _massHiveAt + rot * new Vector3(Mathf.Cos(a) * s, 0.5f * s, Mathf.Sin(a) * s);
                var bottom = _massHiveAt + rot * new Vector3(Mathf.Cos(a) * s, -0.5f * s, Mathf.Sin(a) * s);
                struts.SetPosition(i * 3, top);
                struts.SetPosition(i * 3 + 1, bottom);
                struts.SetPosition(i * 3 + 2, top);
            }
            ShowHiveMarker(true);
        }

        bool EnsureHiveMarker()
        {
            if (_hiveMarker != null) return true;
            var material = _cfg.PreviewMaterial ? _cfg.PreviewMaterial : PreviewFallbackMaterial();
            if (!material) return false;   // headless: no graphics, no marker
            _hiveMarkerRoot = new GameObject("HoneyguideMassHive");
            _hiveMarker = new LineRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject(i < 2 ? "Hex" + i : "Struts");
                go.transform.SetParent(_hiveMarkerRoot.transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;
                line.loop = i < 2;
                line.numCapVertices = 2;
                line.widthMultiplier = _cfg.PreviewWidth * 1.5f;
                line.startColor = line.endColor = _cfg.HiveMarkerColour;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.positionCount = 0;
                _hiveMarker[i] = line;
            }
            return true;
        }

        void ShowHiveMarker(bool on)
        {
            if (_hiveMarker == null) return;
            foreach (var l in _hiveMarker) if (l && l.enabled != on) l.enabled = on;
        }
    }
}
