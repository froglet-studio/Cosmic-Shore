using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// A place for each game: the HyperSea sky every game scene authors (<c>HyperSeaSkybox.mat</c>), re-shaped per
    /// game so a still of Grizzly Time, Kelpwood or Scarab Rally says where it was taken (the footage critique's weak
    /// link 1, 2026-11-13: "every game is played on the same wallpaper").
    ///
    /// A place changes only the sky's NUMBERS - the band's tilt, where the core and Andromeda sit, how much nebula and
    /// in what mix of the sky's own three nebula colours, how the clouds are drawn out, how much of the far net shows
    /// and how near it hangs. It never sets a colour: every colour the game draws comes from the worn colour set
    /// (LOCKED, Garrett 2026-11-10), and a Color property named here is refused with a warning.
    ///
    /// <see cref="CosmicShore.Core.PlatformRenderApplier"/> draws a place's sky (a copy of the scene's own sky
    /// material with these values, made once per session) wherever the active scene is listed. A tier or quality
    /// preset that bakes the sky draws its bake instead, as before. Loaded from <c>Resources/HyperSeaSkyPlaces</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HyperSeaSkyPlaces",
        menuName = "ScriptableObjects/Environment/" + nameof(HyperSeaSkyPlacesSO))]
    public class HyperSeaSkyPlacesSO : ScriptableObject
    {
        /// <summary>Resources path of the shipped asset.</summary>
        public const string ResourcePath = "HyperSeaSkyPlaces";

        [Tooltip("One row per place. A scene no row lists draws the sky it authored.")]
        [SerializeField] Place[] places = Array.Empty<Place>();

        public IReadOnlyList<Place> Places => places;

        /// <summary>The place that lists <paramref name="sceneName"/>, or null.</summary>
        public Place For(string sceneName)
        {
            if (places == null || string.IsNullOrEmpty(sceneName)) return null;
            foreach (var place in places)
                if (place != null && place.Lists(sceneName)) return place;
            return null;
        }

        /// <summary>
        /// The sky to draw in <paramref name="sceneName"/>: its place's copy of <paramref name="authored"/>, or null
        /// when no place lists the scene (draw <paramref name="authored"/>).
        /// </summary>
        public Material SkyFor(string sceneName, Material authored)
        {
            var place = For(sceneName);
            return place != null && authored ? place.SkyFrom(authored) : null;
        }

        [Serializable]
        public class Place
        {
            [Tooltip("What the place is, in a word or two (it names the sky copy).")]
            [SerializeField] string name = "Place";

            [Tooltip("The scenes (by name, e.g. MinigameKelpwood) that are played in this place.")]
            [SerializeField] string[] scenes = Array.Empty<string>();

            [Tooltip("Float properties of the sky shader, by name, and the value this place gives each.")]
            [SerializeField] FloatValue[] floats = Array.Empty<FloatValue>();

            [Tooltip("Vector properties of the sky shader (directions, the nebula mix and stretch), by name.")]
            [SerializeField] VectorValue[] vectors = Array.Empty<VectorValue>();

            [NonSerialized] Material _from;
            [NonSerialized] Material _sky;

            public string Name => name;
            public IReadOnlyList<string> Scenes => scenes;
            public IReadOnlyList<FloatValue> Floats => floats;
            public IReadOnlyList<VectorValue> Vectors => vectors;

            public bool Lists(string sceneName)
            {
                if (scenes == null) return false;
                foreach (var s in scenes)
                    if (string.Equals(s, sceneName, StringComparison.Ordinal)) return true;
                return false;
            }

            /// <summary>This place's copy of <paramref name="authored"/>, made once and kept for the session (a scene
            /// reload draws the same copy, so nothing is allocated per load). Never written to the project.</summary>
            public Material SkyFrom(Material authored)
            {
                if (_sky && _from == authored) return _sky;
                if (_sky) Destroy(_sky);
                _from = authored;
                _sky = new Material(authored) { name = $"{authored.name} ({name})", hideFlags = HideFlags.DontSave };
                Apply(_sky);
                return _sky;
            }

            /// <summary>Writes this place's values onto <paramref name="sky"/>. A property the shader does not have,
            /// or a colour, is skipped with a warning naming it.</summary>
            public void Apply(Material sky)
            {
                var shader = sky.shader;
                if (floats != null)
                    foreach (var f in floats)
                        if (Settable(shader, f.Property, false)) sky.SetFloat(f.Property, f.Value);
                if (vectors != null)
                    foreach (var v in vectors)
                        if (Settable(shader, v.Property, true)) sky.SetVector(v.Property, v.Value);
            }

            bool Settable(Shader shader, string property, bool vector)
            {
                int i = shader ? shader.FindPropertyIndex(property ?? string.Empty) : -1;
                if (i < 0)
                {
                    CSDebug.LogWarning($"[HyperSeaSkyPlaces] {name}: the sky has no property '{property}'.");
                    return false;
                }
                var type = shader.GetPropertyType(i);
                bool ok = vector ? type == ShaderPropertyType.Vector
                                 : type is ShaderPropertyType.Float or ShaderPropertyType.Range;
                if (!ok)
                    CSDebug.LogWarning($"[HyperSeaSkyPlaces] {name}: '{property}' is a {type} property; a place " +
                                       "sets only numbers and directions (colours come from the worn colour set).");
                return ok;
            }

            static void Destroy(Material m)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(m);
                else UnityEngine.Object.DestroyImmediate(m);
            }
        }

        [Serializable]
        public struct FloatValue
        {
            [Tooltip("The sky shader's float property, e.g. _NebulaStrength.")]
            [SerializeField] string property;
            [SerializeField] float value;

            public FloatValue(string property, float value) { this.property = property; this.value = value; }
            public string Property => property;
            public float Value => value;
        }

        [Serializable]
        public struct VectorValue
        {
            [Tooltip("The sky shader's vector property, e.g. _GalacticNormal.")]
            [SerializeField] string property;
            [SerializeField] Vector4 value;

            public VectorValue(string property, Vector4 value) { this.property = property; this.value = value; }
            public string Property => property;
            public Vector4 Value => value;
        }
    }
}
