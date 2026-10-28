using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The galactic map, in the world (Docs/Voyages/SLICE.md §1.4): a small 3D chart of the HyperSea floating beside the
    /// Ark - every location a marker at its chart position, every lane a line, the Ark's location large, the job's
    /// destination pulsing. A world fact shown in the world, in 3D (the LOCKED rule in SHADERS_AND_PERFORMANCE.md), not a
    /// screen overlay. It follows the Ark rather than riding under it, so the hull's own transform never scales it.
    /// </summary>
    public sealed class VoyageChart : MonoBehaviour
    {
        const float MarkerFraction = 0.035f;
        const float HereScale = 1.9f;
        const float PulseHz = 1.2f;

        readonly Dictionary<string, Transform> _markers = new();
        Transform _follow;
        Vector3 _offset;
        string _here, _destination;
        float _markerSize;

        /// <summary>Build the chart of <paramref name="content"/>, following <paramref name="follow"/> at
        /// <paramref name="def"/>'s offset.</summary>
        public static VoyageChart Create(Transform parent, VoyageContent content, VoyageToyDefinitionSO def,
            ToyContext context)
        {
            var go = new GameObject("VoyageChart");
            go.transform.SetParent(parent, false);
            var chart = go.AddComponent<VoyageChart>();
            chart.Build(content, def, context);
            return chart;
        }

        void Build(VoyageContent content, VoyageToyDefinitionSO def, ToyContext context)
        {
            _offset = def.ChartOffset;
            if (content?.Locations == null || content.Locations.Count == 0) return;

            // Fit the authored chart coordinates into the chart's size, centred.
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var l in content.Locations)
            {
                if (l == null) continue;
                var p = new Vector3(l.ChartX, l.ChartY, l.ChartZ);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 centre = (min + max) * 0.5f;
            float extent = Mathf.Max(0.001f, Mathf.Max(max.x - min.x, Mathf.Max(max.y - min.y, max.z - min.z)));
            float scale = def.ChartSize / extent;
            _markerSize = def.ChartSize * MarkerFraction;

            foreach (var l in content.Locations)
            {
                if (l == null || string.IsNullOrEmpty(l.Id)) continue;
                var color = l.Experience switch
                {
                    VoyageExperience.Harbour => ToyFactory.DomainAccentColor(context, l.Home),
                    VoyageExperience.VaultField => def.VaultColor,
                    VoyageExperience.Core => def.CoreColor,
                    _ => def.PortColor,
                };
                var marker = ToyFactory.AddSphereBody(transform, _markerSize, color);
                marker.name = l.Id;
                marker.transform.localPosition = (new Vector3(l.ChartX, l.ChartY, l.ChartZ) - centre) * scale;
                _markers[l.Id] = marker.transform;
            }

            foreach (var lane in content.Lanes ?? new List<VoyageLane>())
            {
                if (lane == null || !_markers.TryGetValue(lane.From, out var a) || !_markers.TryGetValue(lane.To, out var b))
                    continue;
                var line = ToyFactory.CreateLine($"{lane.From}-{lane.To}", transform, _markerSize * 0.25f, worldSpace: false);
                line.positionCount = 2;
                line.SetPosition(0, a.localPosition);
                line.SetPosition(1, b.localPosition);
                line.startColor = line.endColor = def.LaneColor;
            }
        }

        /// <summary>Follow <paramref name="target"/> (the Ark), or hold still where the chart is when null.</summary>
        public void Follow(Transform target) => _follow = target;

        /// <summary>Mark where the Ark is and where the job goes (null: no job).</summary>
        public void Show(string here, string destination)
        {
            _here = here;
            _destination = destination;
            foreach (var kv in _markers) kv.Value.localScale = Vector3.one * (_markerSize * 2f * (kv.Key == _here ? HereScale : 1f));
        }

        void LateUpdate()
        {
            if (_follow)
            {
                var rot = Quaternion.LookRotation(_follow.forward, Vector3.up);
                transform.SetPositionAndRotation(_follow.position + rot * _offset, rot);
            }
            if (!string.IsNullOrEmpty(_destination) && _markers.TryGetValue(_destination, out var dest))
            {
                float pulse = 1f + 0.5f * (0.5f + 0.5f * Mathf.Sin(Time.time * PulseHz * 2f * Mathf.PI));
                dest.localScale = Vector3.one * (_markerSize * 2f * pulse);
            }
        }
    }
}
