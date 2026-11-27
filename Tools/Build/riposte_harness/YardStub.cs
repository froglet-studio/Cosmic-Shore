// The two constants and the one function RiposteYard reads from SpawnableJackstraw, so the placement rules compile
// without Unity. author_riposte_assets.py --check holds the constants to the shipped file.
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class SpawnableJackstraw
    {
        public const float Segment = 10f, Section = 3f;

        public static Vector3 Fibonacci(int k, int n)
        {
            float y = 1f - 2f * (k + 0.5f) / n;
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = 2.39996323f * k;
            return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s);
        }
    }
}
