using UnityEngine;
using System.Collections.Generic;

public class HilbertCurveLSystemPositioning : MonoBehaviour
{
    [SerializeField] private int iterations = 2;
    [SerializeField] private float segmentLength = 1f;
    //[SerializeField] private float wallHeight = 1f;
    //[SerializeField] private float wallThickness = 0.1f;

    private string axiom = "A";
    private Dictionary<char, string> rules = new Dictionary<char, string>
    {
        {'A', "B-F+CFC+F-D&F^D-F+&&CFC+F+B//"},
        {'B', "A&F^CFB^F^D^^-F-D^|F^B|FC^F^A//"},
        {'C', "|D^|F^B-F+C^F^A&&FA&F^C+F+B^F^D//"},
        {'D', "|CFB-F+B|FA&F^A&&FB-F+B|FC//"}
    };

    private List<Vector3> positions = new List<Vector3>();
    private List<Quaternion> rotations = new List<Quaternion>();

    public void GenerateHilbertCurve()
    {
        string sequence = GenerateLSystem();
        InterpretLSystem(sequence);
    }

    private string GenerateLSystem()
    {
        string result = axiom;
        for (int i = 0; i < iterations; i++)
        {
            string newResult = "";
            foreach (char c in result)
            {
                if (rules.ContainsKey(c))
                    newResult += rules[c];
                else
                    newResult += c;
            }
            result = newResult;
        }
        return result;
    }

    private void InterpretLSystem(string sequence)
    {
        Vector3 position = Vector3.zero;
        Vector3 direction = Vector3.forward;
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.right;

        positions.Clear();
        rotations.Clear();

        foreach (char c in sequence)
        {
            switch (c)
            {
                case 'F':
                    positions.Add(position);
                    rotations.Add(Quaternion.LookRotation(direction, up));
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
        Debug.Log("Positions: " + positions.Count);
    }

    private void RotateAroundAxis(ref Vector3 v1, ref Vector3 v2, Vector3 axis, float angle)
    {
        v1 = Quaternion.AngleAxis(angle, axis) * v1;
        v2 = Quaternion.AngleAxis(angle, axis) * v2;
    }

    public List<Vector3> GetPositions() => positions;
    public List<Quaternion> GetRotations() => rotations;
}