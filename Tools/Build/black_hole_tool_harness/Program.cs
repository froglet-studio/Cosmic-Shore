// Runs the SHIPPED BlackHoleConfigSO.cs + BlackHoleToolModel.cs (Docs/BLACK_HOLE.md §6.1) under
// plain .NET and asserts what the Black Hole tool rests on:
//
//   1. COVERAGE — the tool reaches EVERY serialized field of the config: the model's field list is
//      exactly the [SerializeField]s in the source, none of an unsupported kind.
//   2. BOUNDS — every number has a bound the tool can enforce ([Range] → a slider, or a [Min]).
//   3. SPAWN SECTION — the five spawn fields exist, in the tool's order, under the Spawn header.
//   4. ASSET ⇄ SO ⇄ TOOL — every key in Resources/BlackHoleConfig.asset is a field the tool edits,
//      and every field the tool edits is in the asset; the asset loads through the model.
//   5. SIZE — the config's size-aware helpers: size 0 derives r_s from strength, a set size wins,
//      the floor holds, influence and warp reach follow the size.
//   6. CLAMPING — writes through the model land inside the field's own bounds (and whole for ints).
//   7. LABELS and the console switch parse.
//   8. NEGATIVE CONTROLS — a config with an unsupported field fails (1), and an asset with a
//      renamed key fails (4): both checks are seen to fire.
//
//   run.sh <BlackHoleConfig.asset> <BlackHoleConfigSO.cs>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

static class BlackHoleToolHarness
{
    static int failures;

    static void Check(bool ok, string what)
    {
        if (!ok) { failures++; Console.WriteLine("  FAIL " + what); }
    }

    // The serialized MonoBehaviour body of a .asset: key → raw value (top-level keys only).
    static Dictionary<string, string> AssetKeys(string text)
    {
        var keys = new Dictionary<string, string>();
        bool body = false;
        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith("  m_EditorClassIdentifier:")) { body = true; continue; }
            if (!body) continue;
            var m = Regex.Match(line, @"^  ([A-Za-z_][A-Za-z0-9_]*): ?(.*)$");
            if (m.Success) keys[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        }
        return keys;
    }

    // Loads an asset's values into a config instance THROUGH the model, the way the tool writes.
    // Returns the keys the model does not know.
    static List<string> Load(BlackHoleConfigSO config, List<BlackHoleToolField> fields, Dictionary<string, string> keys)
    {
        var unknown = new List<string>();
        foreach (var kv in keys)
        {
            var f = fields.FirstOrDefault(x => x.Name == kv.Key);
            if (f == null) { unknown.Add(kv.Key); continue; }
            switch (f.Kind)
            {
                case BlackHoleToolFieldKind.Float:
                case BlackHoleToolFieldKind.Int:
                    BlackHoleToolModel.TryParse(kv.Value, out float v);
                    f.SetNumber(config, v);
                    break;
                case BlackHoleToolFieldKind.Bool:
                    f.SetBool(config, kv.Value == "1");
                    break;
                case BlackHoleToolFieldKind.Vector3:
                {
                    var m = Regex.Match(kv.Value, @"x: *([-\d.eE]+), *y: *([-\d.eE]+), *z: *([-\d.eE]+)");
                    BlackHoleToolModel.TryParse(m.Groups[1].Value, out float x);
                    BlackHoleToolModel.TryParse(m.Groups[2].Value, out float y);
                    BlackHoleToolModel.TryParse(m.Groups[3].Value, out float z);
                    f.SetVector(config, new Vector3(x, y, z));
                    break;
                }
            }
        }
        return unknown;
    }

    // Negative control for (1): a config shape with a field the tool cannot draw.
    sealed class ConfigWithAString : ScriptableObject
    {
        [SerializeField] float fine = 1f;
        [SerializeField] string label = "x";
        public float Fine => fine;
        public string Label => label;
    }

    static int Main(string[] args)
    {
        if (args.Length != 2) { Console.WriteLine("usage: <BlackHoleConfig.asset> <BlackHoleConfigSO.cs>"); return 2; }
        string assetText = File.ReadAllText(args[0]);
        string source = File.ReadAllText(args[1]);
        var fields = BlackHoleToolModel.EditableFields(typeof(BlackHoleConfigSO));

        // 1. coverage
        int sourceSerialized = Regex.Matches(source, @"\[SerializeField\]").Count;
        var unsupported = fields.Where(f => f.Kind == BlackHoleToolFieldKind.Unsupported).Select(f => f.Name).ToList();
        Check(fields.Count == sourceSerialized, $"the tool lists {fields.Count} fields, the source declares {sourceSerialized} [SerializeField]s");
        Check(unsupported.Count == 0, "fields the tool cannot draw: " + string.Join(", ", unsupported));
        Console.WriteLine($"1. coverage: {fields.Count} config fields, all drawable " +
                          $"({fields.Count(f => f.Kind == BlackHoleToolFieldKind.Float)} float, {fields.Count(f => f.Kind == BlackHoleToolFieldKind.Int)} int, " +
                          $"{fields.Count(f => f.Kind == BlackHoleToolFieldKind.Bool)} bool, {fields.Count(f => f.Kind == BlackHoleToolFieldKind.Vector3)} Vector3)");

        // 2. bounds
        var unbounded = fields.Where(f => (f.Kind == BlackHoleToolFieldKind.Float || f.Kind == BlackHoleToolFieldKind.Int) &&
                                          float.IsNegativeInfinity(f.Min) && !f.HasRange).Select(f => f.Name).ToList();
        Check(unbounded.Count == 0, "numbers with no [Range] or [Min] the tool could enforce: " + string.Join(", ", unbounded));
        Console.WriteLine($"2. bounds: {fields.Count(f => f.HasRange)} sliders ([Range]), the rest floored by [Min]; none unbounded");

        // 3. spawn section
        var spawnIdx = BlackHoleToolModel.SpawnFieldNames.Select(n => fields.FindIndex(f => f.Name == n)).ToArray();
        Check(spawnIdx.All(i => i >= 0), "a spawn field the tool shows is missing from the config");
        Check(spawnIdx.Zip(spawnIdx.Skip(1), (a, b) => a < b).All(x => x), "the spawn fields are not in the tool's order in the config");
        Check(spawnIdx[0] >= 0 && (fields[spawnIdx[0]].Header ?? "").StartsWith("Spawn"), "spawnStrength does not open the Spawn header");
        var size = fields.FirstOrDefault(f => f.Name == "spawnHorizonRadius");
        Check(size != null && size.HasRange && size.Min == 0f && size.Max >= 100f, "spawn size is not a 0..≥100 slider");
        Console.WriteLine($"3. spawn section: {string.Join(", ", BlackHoleToolModel.SpawnFieldNames)} under \"{(spawnIdx[0] >= 0 ? fields[spawnIdx[0]].Header : "?")}\"");

        // 4. asset ⇄ SO ⇄ tool
        var keys = AssetKeys(assetText);
        var config = (BlackHoleConfigSO)RuntimeHelpers.GetUninitializedObject(typeof(BlackHoleConfigSO));
        var unknownKeys = Load(config, fields, keys);
        var missingKeys = fields.Where(f => !keys.ContainsKey(f.Name)).Select(f => f.Name).ToList();
        Check(unknownKeys.Count == 0, "asset keys the tool does not know (stale field?): " + string.Join(", ", unknownKeys));
        Check(missingKeys.Count == 0, "config fields missing from the asset: " + string.Join(", ", missingKeys));
        Check(config.IsSane, "the shipped asset, loaded through the tool's model, is not sane");
        Console.WriteLine($"4. asset ⇄ SO ⇄ tool: {keys.Count} asset keys, all known; loaded through the model and sane " +
                          $"(spawn strength {config.SpawnStrength}, size {config.SpawnHorizonRadius}, position ({config.SpawnPosition.x}, {config.SpawnPosition.y}, {config.SpawnPosition.z}))");

        // 5. size
        float derived = config.HorizonRadius(10f, 0f);
        Check(Math.Abs(derived - config.HorizonRadius(10f)) < 1e-6f, "size 0 does not derive r_s from strength");
        Check(Math.Abs(config.HorizonRadius(10f, 35f) - 35f) < 1e-6f, "a set size does not win over the strength");
        Check(Math.Abs(config.HorizonRadius(10f, 0.001f) - config.MinHorizonRadius) < 1e-6f, "a set size below the floor is not floored");
        Check(config.InfluenceRadius(10f, 35f) >= 1.5f * 35f - 1e-3f, "the influence radius is inside 1.5 horizons of a set size");
        Check(config.InfluenceRadius(10f, 400f) >= 600f - 1e-3f, "a size past the influence cap shrinks the influence inside 1.5 horizons");
        Check(Math.Abs(config.WarpReachForHorizon(35f) - 35f * (config.WarpReachMultiplier - 1f)) < 1e-3f, "the warp reach does not follow the set size");
        Check(Math.Abs(config.WarpReach(10f) - config.WarpReachForHorizon(config.HorizonRadius(10f))) < 1e-4f, "WarpReach(strength) and WarpReachForHorizon disagree");
        Console.WriteLine($"5. size: strength 10 → r_s {derived:F1} (from strength), set 35 → {config.HorizonRadius(10f, 35f):F1}; " +
                          $"influence {config.InfluenceRadius(10f, 35f):F0} u, warp reach {config.WarpReachForHorizon(35f):F0} u");

        // 6. clamping through the model
        Field(fields, "spawnStrength").SetNumber(config, 500f);
        Check(config.SpawnStrength == 100f, $"spawnStrength 500 stored as {config.SpawnStrength}, expected the [Range] max 100");
        Field(fields, "maxBlackHoles").SetNumber(config, 7.6f);
        Check(config.MaxBlackHoles == 4, $"maxBlackHoles 7.6 stored as {config.MaxBlackHoles}, expected 4");
        Field(fields, "maxSubsteps").SetNumber(config, 3.4f);
        Check(config.MaxSubsteps == 3, $"maxSubsteps 3.4 stored as {config.MaxSubsteps}, expected whole 3");
        Field(fields, "maxBodies").SetNumber(config, -5f);
        Check(config.MaxBodies == 0, $"maxBodies -5 stored as {config.MaxBodies}, expected the [Min] 0");
        Field(fields, "spawnHorizonRadius").SetNumber(config, float.NaN);
        Check(config.SpawnHorizonRadius == 0f, $"spawn size NaN stored as {config.SpawnHorizonRadius}, expected the minimum 0");
        Console.WriteLine("6. clamping: writes land inside [Range]/[Min], ints whole, NaN → the minimum");

        // 7. labels + switch
        Check(BlackHoleToolModel.Nicify("spawnHorizonRadius") == "Spawn Horizon Radius", "Nicify(spawnHorizonRadius) = " + BlackHoleToolModel.Nicify("spawnHorizonRadius"));
        Check(BlackHoleToolModel.Nicify("gmPerStrength") == "Gm Per Strength", "Nicify(gmPerStrength) = " + BlackHoleToolModel.Nicify("gmPerStrength"));
        Check(BlackHoleToolModel.ParseSwitch("on", out bool r1) == true && r1, "'on' does not open");
        Check(BlackHoleToolModel.ParseSwitch("OFF", out bool r2) == false && r2, "'OFF' does not close");
        Check(BlackHoleToolModel.ParseSwitch(null, out bool r3) == null && r3, "no word is not a toggle");
        BlackHoleToolModel.ParseSwitch("banana", out bool r4);
        Check(!r4, "'banana' is accepted as a switch");
        Console.WriteLine("7. labels and the on/off switch parse");

        // 8. negative controls
        var bad = BlackHoleToolModel.EditableFields(typeof(ConfigWithAString));
        bool fired1 = bad.Any(f => f.Kind == BlackHoleToolFieldKind.Unsupported && f.Name == "label");
        var mutated = AssetKeys(assetText.Replace("  spawnStrength:", "  spawnStrenght:"));
        var cfg2 = (BlackHoleConfigSO)RuntimeHelpers.GetUninitializedObject(typeof(BlackHoleConfigSO));
        bool fired2 = Load(cfg2, fields, mutated).Contains("spawnStrenght");
        Console.WriteLine($"8. negative controls: unsupported field {(fired1 ? "FIRED" : "DID NOT FIRE")}, renamed asset key {(fired2 ? "FIRED" : "DID NOT FIRE")}");
        Check(fired1 && fired2, "a negative control did not fire");

        Console.WriteLine(failures == 0 ? "\nblack hole tool harness: OK" : $"\nblack hole tool harness: {failures} FAILED");
        return failures == 0 ? 0 : 1;
    }

    static BlackHoleToolField Field(List<BlackHoleToolField> fields, string name) =>
        fields.First(f => f.Name == name);
}
