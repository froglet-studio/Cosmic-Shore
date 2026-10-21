using System;
using System.Globalization;
using System.Text;
using CosmicShore.Gameplay;

// Prints the C# mask's values on the grid the Node side evaluates the lab's twin on (cases.json).
// One line per case: "<kind> <values...>", invariant culture, round-trip floats.
public static class Driver
{
    public static int Main(string[] args)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        foreach (var line in System.IO.File.ReadAllLines(args[0]))
        {
            var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f.Length == 0) continue;
            float F(int i) => float.Parse(f[i], inv);
            switch (f[0])
            {
                case "shape":
                {
                    // shape style facets inner feather roundness fill rimStrength rimWidth aspect
                    var s = new SilenceVignetteMask.Shape
                    {
                        Style = int.Parse(f[1], inv), Facets = int.Parse(f[2], inv), Inner = F(3), Feather = F(4),
                        Roundness = F(5), Fill = F(6), RimStrength = F(7), RimWidth = F(8),
                    };
                    float aspect = F(9);
                    sb.Append("shape");
                    for (int y = 0; y <= 16; y++)
                    for (int x = 0; x <= 16; x++)
                    {
                        float u = x / 16f, v = y / 16f;
                        sb.Append(' ').Append(SilenceVignetteMask.Distance(u, v, aspect, s).ToString("R", inv));
                        sb.Append(' ').Append(SilenceVignetteMask.Alpha(u, v, aspect, s).ToString("R", inv));
                    }
                    sb.AppendLine();
                    break;
                }
                case "breathe":
                    sb.Append("breathe ").AppendLine(SilenceVignetteMask.Breathe(F(1), F(2), F(3)).ToString("R", inv));
                    break;
                case "opacity":
                    sb.Append("opacity ").AppendLine(SilenceVignetteMask.Opacity(F(1), F(2), F(3), F(4), F(5)).ToString("R", inv));
                    break;
            }
        }
        Console.Write(sb.ToString());
        return 0;
    }
}
