using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game.Environment;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Generates points along a 3D Hilbert curve using an L-System.
    /// </summary>
    public class HilbertCurveGenerator : SpawnableBase
    {
        [Header("Hilbert Curve")]
        [SerializeField] int iterations = 2;
        [SerializeField] float segmentLength = 1f;
        [SerializeField] float rotationAngle;
        [SerializeField] Vector3 origin;

        private static readonly string Axiom = "A";
        private static readonly Dictionary<char, string> Rules = new()
        {
            { 'A', "B-F+CFC+F-D&F^D-F+&&CFC+F+B//" },
            { 'B', "A&F^CFB^F^D^^-F-D^|F^B|FC^F^A//" },
            { 'C', "|D^|F^B-F+C^F^A&&FA&F^C+F+B^F^D//" },
            { 'D', "|CFB-F+B|FA&F^A&&FB-F+B|FC//" }
        };

        protected override SpawnPoint[] GeneratePoints()
        {
            string sequence = GenerateLSystem();
            return InterpretLSystem(sequence);
        }

        private string GenerateLSystem()
        {
            string result = Axiom;
            for (int i = 0; i < iterations; i++)
            {
                var sb = new System.Text.StringBuilder(result.Length * 4);
                foreach (char c in result)
                {
                    if (Rules.TryGetValue(c, out var replacement))
                        sb.Append(replacement);
                    else
                        sb.Append(c);
                }
                result = sb.ToString();
            }
            return result;
        }

        private SpawnPoint[] InterpretLSystem(string sequence)
        {
            var points = new List<SpawnPoint>();
            var position = Vector3.zero;
            var direction = Vector3.forward;
            var up = Vector3.up;
            var right = Vector3.right;

            Quaternion globalRot = Quaternion.Euler(0, 0, rotationAngle);

            foreach (char c in sequence)
            {
                switch (c)
                {
                    case 'F':
                        Quaternion rot;
                        if (SafeLookRotation.TryGet(direction, up, out rot, this))
                            points.Add(new SpawnPoint(globalRot * (position + origin), globalRot * rot));
                        else if (points.Count > 0)
                            points.Add(new SpawnPoint(globalRot * (position + origin), points[^1].Rotation));
                        else
                            points.Add(new SpawnPoint(globalRot * (position + origin), globalRot * Quaternion.identity));

                        position += direction * segmentLength;
                        break;
                    case '+':
                        RotateAroundAxis(ref direction, ref up, right, 90);
                        break;
                    case '-':
                        RotateAroundAxis(ref direction, ref up, right, -90);
                        break;
                    case '^':
                        RotateAroundAxis(ref direction, ref right, up, 90);
                        break;
                    case '&':
                        RotateAroundAxis(ref direction, ref right, up, -90);
                        break;
                    case '>':
                        RotateAroundAxis(ref up, ref right, direction, 90);
                        break;
                    case '<':
                        RotateAroundAxis(ref up, ref right, direction, -90);
                        break;
                    case '|':
                        direction = -direction;
                        right = -right;
                        break;
                    case '/':
                        RotateAroundAxis(ref up, ref right, direction, 180);
                        break;
                }
            }

            return points.ToArray();
        }

        private static void RotateAroundAxis(ref Vector3 v1, ref Vector3 v2, Vector3 axis, float angle)
        {
            var q = Quaternion.AngleAxis(angle, axis);
            v1 = q * v1;
            v2 = q * v2;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(iterations, segmentLength, rotationAngle, seed, origin);
        }
    }
}
