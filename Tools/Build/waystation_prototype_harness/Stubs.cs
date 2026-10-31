// The two outside members WaystationTower.cs reads, at their shipped values, so the pure file compiles without the
// rest of the game. Each is a CONSTANT or a one-line formula; `waystation_prototype_course.py --compile` asserts them
// against the real files, so a stub that drifts fails there.
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public abstract class RaceCourseSource
    {
        public const float DefaultInnerRadiusNucleusFactor = 1.22f;
    }

    public static class TigerMothDropMath
    {
        public static float Ease01(float x)
        {
            float t = Mathf.Clamp01(x);
            return t * t * (3f - 2f * t);
        }
    }
}
