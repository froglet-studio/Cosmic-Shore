// Prints what the SHIPPED FloraElementalForm actually resolves, as JSON on stdout, so the
// measurement tool compares the GAME against its independent model rather than comparing
// two models to each other. stdout is a DATA channel - nothing else may write to it.
using System;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

static class Driver
{
    static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

    static int Main(string[] args)
    {
        // Every leaf the tool wants shaped, as "x,y,z" triples after the mode.
        //   table                      - the four elements' scalars
        //   shape x,y,z [x,y,z ...]    - each leaf through every element
        if (args.Length == 0) { Console.Error.WriteLine("usage: table | shape x,y,z ..."); return 2; }

        var elements = new[] { Element.Charge, Element.Mass, Element.Space, Element.Time, Element.None };

        if (args[0] == "table")
        {
            Console.Write("{");
            for (int i = 0; i < elements.Length; i++)
            {
                var e = elements[i];
                if (i > 0) Console.Write(",");
                Console.Write($"\"{e}\":{{\"volume\":{F(FloraElementalForm.LeafVolumeScale(e))}," +
                              $"\"anisotropy\":{F(FloraElementalForm.LeafAnisotropy(e))}," +
                              $"\"reach\":{F(FloraElementalForm.ReachScale(e))}," +
                              $"\"growPeriod10\":{F(FloraElementalForm.ScaleGrowPeriod(10f, e))}," +
                              $"\"reproRate\":{F(FloraReproductionRules.ReproductionRateFor(e))}}}");
            }
            Console.WriteLine("}");
            return 0;
        }

        if (args[0] == "shape")
        {
            Console.Write("[");
            for (int a = 1; a < args.Length; a++)
            {
                var p = args[a].Split(',');
                var leaf = new Vector3(
                    float.Parse(p[0], CultureInfo.InvariantCulture),
                    float.Parse(p[1], CultureInfo.InvariantCulture),
                    float.Parse(p[2], CultureInfo.InvariantCulture));
                if (a > 1) Console.Write(",");
                Console.Write("{\"authored\":[" + F(leaf.x) + "," + F(leaf.y) + "," + F(leaf.z) + "]");
                foreach (var e in elements)
                {
                    var s = FloraElementalForm.ShapeLeaf(leaf, e);
                    Console.Write($",\"{e}\":[{F(s.x)},{F(s.y)},{F(s.z)}]");
                }
                Console.Write("}");
            }
            Console.WriteLine("]");
            return 0;
        }

        Console.Error.WriteLine("unknown mode: " + args[0]);
        return 2;
    }
}
