using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Content;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

// harness <out.json> <budget> <seed> (<configAsset.asset>)+
//
// Each config's FloraPrefab is instantiated in Amoebius's content runtime, its Variant applied and its element resolved
// exactly as LifeForm.Initialize does, then grown through the SHIPPED Flora.TryPreviewGrowth - so the prisms measured
// are the prisms the game's own growth rule lays (Docs/ECOSYSTEM.md 65, Tools/Build/measure_flora_heart_clearance.py).
// A BranchingFlora preview reports its limbs as well as its leaves (its silhouette is its branches); limbs are exempt
// from every prism rule, so only the entries at the plant's leaf size are written.
static class Harness
{
    static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    static int Main(string[] args)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        if (!Directory.Exists(Path.Combine(root, "Assets"))) root = Directory.GetCurrentDirectory();
        using var loop = new GameLoop("flora_heart_harness");
        var rt = new ContentRuntime(root, new[] { typeof(Fauna).Assembly });
        int budget = int.Parse(args[1]), seed = int.Parse(args[2]);
        var lifeForm = typeof(LifeForm);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var leafSizeProp = typeof(Flora).GetProperty("LeafSize", flags);
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var cfgPath in args.Skip(3))
        {
            var full = Path.IsPathRooted(cfgPath) ? cfgPath : Path.Combine(root, cfgPath);
            var cfg = rt.Assets.Load<FloraConfigurationSO>(new ObjRef(11400000, rt.Db.GuidOf(full), 2));
            if (cfg == null || cfg.FloraPrefab == null) { Console.Error.WriteLine($"skip {cfgPath}: no config or prefab"); continue; }

            var go = CosmicShore.Engine.Object.Instantiate(cfg.FloraPrefab.gameObject);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var flora = go.GetComponent<Flora>();
            if (cfg.Variant is { Enabled: true }) flora.ApplyVariantTuning(cfg.Variant);
            var crystal = go.GetComponentInChildren<Crystal>(true);
            if (crystal)
            {
                var props = crystal.crystalProperties;
                props.Element = cfg.Element;
                crystal.crystalProperties = props;
                lifeForm.GetField("crystal", flags).SetValue(flora, crystal);
            }
            lifeForm.GetMethod("OnElementResolved", flags).Invoke(flora, null);

            var poses = new List<SpawnPoint>();
            bool ok = flora.TryPreviewGrowth(budget, seed, poses);
            if (flora is BranchingFlora)
            {
                var leaf = (Vector3)leafSizeProp.GetValue(flora);
                if (leaf == Vector3.zero) leaf = Vector3.one;
                poses = poses.Where(p => (p.Scale - leaf).sqrMagnitude < 1e-6f).ToList();
            }
            var heart = crystal ? go.transform.InverseTransformPoint(crystal.transform.position) : Vector3.zero;

            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(Path.GetFileNameWithoutExtension(cfgPath)).Append("\":{\"cls\":\"").Append(flora.GetType().Name)
              .Append("\",\"element\":\"").Append(flora.Element).Append("\",\"preview\":").Append(ok ? "true" : "false")
              .Append(",\"heart\":[").Append(F(heart.x)).Append(',').Append(F(heart.y)).Append(',').Append(F(heart.z))
              .Append("],\"prisms\":[");
            for (int i = 0; i < poses.Count; i++)
            {
                var p = poses[i];
                var q = p.Rotation;
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(string.Join(",", new[] { p.Position.x, p.Position.y, p.Position.z, q.x, q.y, q.z, q.w,
                    p.Scale.x, p.Scale.y, p.Scale.z }.Select(F))).Append(']');
            }
            sb.Append("]}");
            CosmicShore.Engine.Object.DestroyImmediate(go);
        }
        File.WriteAllText(args[0], sb.Append('}').ToString());
        return 0;
    }
}
