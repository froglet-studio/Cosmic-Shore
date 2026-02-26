using UnityEngine;
using System.Collections.Generic;
using System.Text;
using CosmicShore.Game.Ship;
using CosmicShore.Game.Environment;
using UnityEngine.Serialization;
using System.Linq;

namespace CosmicShore.Game.Environment
{
    public class SpawnableLSystem : SpawnableBase
    {
        public enum LSystemPreset
        {
            Custom,
            BasicTree,
            Tree3D,
            HilbertCurve3D,
            KochSnowflake3D,
            SphericalSpiral,
            FractalCoral,
            CrystalStructure
        }

        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
        [SerializeField] LSystemPreset preset = LSystemPreset.BasicTree;
        [SerializeField] float baseLength = 5f;
        [SerializeField] float baseWidth = 1f;
        [SerializeField] float widthScaleReduction = 0.8f;
        [SerializeField] float lengthScaleReduction = 0.9f;

        // Custom rule fields (only used when preset is Custom)
        [SerializeField] string customAxiom = "F";
        [SerializeField] int customIterations = 4;
        [SerializeField] float customAngle = 25f;
        [SerializeField] List<Rule> customRules = new List<Rule>();

        [System.Serializable]
        public struct Rule
        {
            public char input;
            public string output;
        }

        private string axiom;
        private int iterations;
        private float angle;
        private Dictionary<char, string> rulesDictionary = new Dictionary<char, string>();

        private struct TransformInfo
        {
            public Vector3 position;
            public Quaternion rotation;
            public float width;
            public float length;
        }

        private void SetupPreset()
        {
            rulesDictionary.Clear();
            switch (preset)
            {
                case LSystemPreset.BasicTree:
                    axiom = "F";
                    iterations = 4;
                    angle = 25f;
                    rulesDictionary['F'] = "F[+F]F[-F]F";
                    break;
                case LSystemPreset.Tree3D:
                    axiom = "A";
                    iterations = 5;
                    angle = 25f;
                    rulesDictionary['A'] = "F[&FL!A]/////[&FR!A]///////F!A";
                    rulesDictionary['F'] = "S //// F";
                    rulesDictionary['S'] = "F L";
                    rulesDictionary['L'] = "['''^^{-f+f+f-|-f+f+f}]";
                    break;
                case LSystemPreset.HilbertCurve3D:
                    axiom = "X";
                    iterations = 3;
                    angle = 90f;
                    rulesDictionary['X'] = "^<XF^<XFX-F^>>XFX&F+>>XFX-F>X->";
                    break;
                case LSystemPreset.KochSnowflake3D:
                    axiom = "F++F++F";
                    iterations = 4;
                    angle = 60f;
                    rulesDictionary['F'] = "F-F++F-F^F-F++F-F";
                    break;
                case LSystemPreset.SphericalSpiral:
                    axiom = "F";
                    iterations = 200;
                    angle = 15f;
                    rulesDictionary['F'] = "F>!G";
                    rulesDictionary['G'] = "F";
                    break;
                case LSystemPreset.FractalCoral:
                    axiom = "F";
                    iterations = 4;
                    angle = 22.5f;
                    rulesDictionary['F'] = "FF+[+F-F-F]-[-F+F+F]^F&F";
                    break;
                case LSystemPreset.CrystalStructure:
                    axiom = "F";
                    iterations = 3;
                    angle = 60f;
                    rulesDictionary['F'] = "F[-F][+F][&F][^F]F";
                    break;
                case LSystemPreset.Custom:
                    axiom = customAxiom;
                    iterations = customIterations;
                    angle = customAngle;
                    foreach (var rule in customRules)
                    {
                        rulesDictionary[rule.input] = rule.output;
                    }
                    break;
            }
        }

        protected override SpawnTrailData[] GenerateTrailData()
        {
            SetupPreset();
            string sequence = GenerateSequence();
            return BuildTrailData(sequence);
        }

        private string GenerateSequence()
        {
            string result = axiom;
            for (int i = 0; i < iterations; i++)
            {
                StringBuilder sb = new StringBuilder();
                foreach (char c in result)
                {
                    if (rulesDictionary.TryGetValue(c, out string replacement))
                    {
                        sb.Append(replacement);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                result = sb.ToString();
            }
            return result;
        }

        private SpawnTrailData[] BuildTrailData(string sequence)
        {
            var trailDataList = new List<SpawnTrailData>();
            var transformStack = new Stack<TransformInfo>();

            Vector3 currentPosition = Vector3.zero;
            Quaternion currentRotation = Quaternion.identity;
            float currentWidth = baseWidth;
            float currentLength = baseLength;

            var currentPoints = new List<SpawnPoint>();

            foreach (char c in sequence)
            {
                switch (c)
                {
                    case 'F':
                    case 'G':
                        Vector3 newPosition = currentPosition + currentRotation * Vector3.forward * currentLength;
                        // Original: CreateBlock(currentPosition, newPosition, ...) with flip=true
                        // flip=true means forward = position - lookPosition = currentPosition - newPosition
                        var rotation = SpawnPoint.LookRotation(newPosition, currentPosition, Vector3.up);
                        currentPoints.Add(new SpawnPoint(currentPosition, rotation,
                            new Vector3(currentWidth, currentWidth, currentLength)));
                        currentPosition = newPosition;
                        break;
                    case '+':
                        currentRotation *= Quaternion.Euler(0, angle, 0);
                        break;
                    case '-':
                        currentRotation *= Quaternion.Euler(0, -angle, 0);
                        break;
                    case '&':
                        currentRotation *= Quaternion.Euler(angle, 0, 0);
                        break;
                    case '^':
                        currentRotation *= Quaternion.Euler(-angle, 0, 0);
                        break;
                    case '\\':
                        currentRotation *= Quaternion.Euler(0, 0, angle);
                        break;
                    case '/':
                        currentRotation *= Quaternion.Euler(0, 0, -angle);
                        break;
                    case '|':
                        currentRotation *= Quaternion.Euler(180, 0, 0);
                        break;
                    case '[':
                        transformStack.Push(new TransformInfo
                        {
                            position = currentPosition,
                            rotation = currentRotation,
                            width = currentWidth,
                            length = currentLength
                        });
                        // Flush current trail and start a new one
                        if (currentPoints.Count > 0)
                            trailDataList.Add(new SpawnTrailData(currentPoints.ToArray(), false, domain));
                        currentPoints = new List<SpawnPoint>();
                        break;
                    case ']':
                        if (transformStack.Count > 0)
                        {
                            var info = transformStack.Pop();
                            currentPosition = info.position;
                            currentRotation = info.rotation;
                            currentWidth = info.width;
                            currentLength = info.length;
                            // Flush current trail and start a new one
                            if (currentPoints.Count > 0)
                                trailDataList.Add(new SpawnTrailData(currentPoints.ToArray(), false, domain));
                            currentPoints = new List<SpawnPoint>();
                        }
                        break;
                    case '>':
                        currentWidth *= widthScaleReduction;
                        break;
                    case '<':
                        currentWidth /= widthScaleReduction;
                        break;
                    case '!':
                        currentLength *= lengthScaleReduction;
                        break;
                }
            }

            // Flush any remaining points
            if (currentPoints.Count > 0)
                trailDataList.Add(new SpawnTrailData(currentPoints.ToArray(), false, domain));

            return trailDataList.ToArray();
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(preset, baseLength, baseWidth, widthScaleReduction,
                lengthScaleReduction, seed, customAxiom, customIterations);
        }
    }
}
