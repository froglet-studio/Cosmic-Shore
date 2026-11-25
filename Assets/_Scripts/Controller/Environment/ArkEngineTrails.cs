using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark's engine streams (the round Ark's Trail 3, Shombith 2026-11-25: "every engine leaves its own straight
    /// stream"): one trail per bell the model's rig table names (<see cref="ArkRigTable.trails"/>), riding the bell's bone so
    /// it follows the bell as it vectors, in the domain's trail colours from the worn colour set
    /// (<see cref="ApplyColours"/>). The one thing on the Ark that glows. A stream stays dark until it has its colours, and
    /// goes out when the hull is lost.
    /// </summary>
    public sealed class ArkEngineTrails : MonoBehaviour
    {
        readonly List<TrailRenderer> _trails = new();
        Ark _ark;
        float _alpha = 1f;
        bool _coloured;

        /// <summary>The streams laid (one per rig-table trail whose bone the model has).</summary>
        public int Count => _trails.Count;

        /// <summary>
        /// Lay the streams <paramref name="table"/> names on <paramref name="bones"/>. Null when the definition has no
        /// stream material or the table names no trails (ArkReverse).
        /// </summary>
        public static ArkEngineTrails Attach(Ark ark, ArkModelDefinitionSO def, ArkRigTable table,
                                             IReadOnlyDictionary<string, Transform> bones, float scale)
        {
            if (!ark || !def || !def.EngineTrailMaterial || table?.trails == null || table.trails.Length == 0) return null;
            var host = ark.gameObject.AddComponent<ArkEngineTrails>();
            host._ark = ark;
            host._alpha = def.EngineTrailAlpha;
            var arkT = ark.transform;
            float unit = scale * Mathf.Max(1e-4f, arkT.lossyScale.x);
            var widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, def.EngineTrailEndWidth));
            foreach (var trail in table.trails)
            {
                if (trail == null || !bones.TryGetValue(trail.bone ?? string.Empty, out var bone) || !bone)
                {
                    CSDebug.LogWarning($"[Ark] An engine trail rides bone '{trail?.bone}', which the model lacks - " +
                                       "rebuild the model and its table together.", def);
                    continue;
                }
                var go = new GameObject("EngineTrail " + trail.bone);
                go.transform.SetParent(bone, false);
                go.transform.position = arkT.TransformPoint(trail.Position * scale);
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = def.EngineTrailMaterial;
                tr.time = def.EngineTrailSeconds;
                tr.widthMultiplier = trail.width * unit;
                tr.widthCurve = widthCurve;
                tr.minVertexDistance = 2f * unit;
                tr.numCapVertices = 2;
                tr.alignment = LineAlignment.View;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.receiveShadows = false;
                tr.emitting = false;
                host._trails.Add(tr);
            }
            return host;
        }

        /// <summary>
        /// The streams' colours, from the worn colour set's domain block: the trail's core where it leaves the bell,
        /// fading through its highlight to nothing at its end.
        /// </summary>
        public void ApplyColours(Color core, Color highlight)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(core, 0f), new GradientColorKey(highlight, 1f) },
                new[] { new GradientAlphaKey(_alpha, 0f), new GradientAlphaKey(_alpha * 0.5f, 0.45f), new GradientAlphaKey(0f, 1f) });
            foreach (var tr in _trails)
                if (tr) tr.colorGradient = gradient;
            _coloured = true;
        }

        void LateUpdate()
        {
            bool on = _coloured && _ark && !_ark.IsHullLost;
            for (int i = 0; i < _trails.Count; i++)
            {
                var tr = _trails[i];
                if (tr && tr.emitting != on) tr.emitting = on;
            }
        }
    }
}
