using System;
using System.Linq;
using System.Reflection;
using CosmicShore.Game;

public static class ElementalFloatBinder
{
    static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly System.Collections.Generic.Dictionary<Type, FieldInfo[]> Cache = new();

    public static void BindAndClone(object target, IShip ship, string prefix)
    {
        if (target == null || ship == null) return;
        var t = target.GetType();

        if (!Cache.TryGetValue(t, out var fields))
        {
            fields = t.GetFields(BF).Where(f => f.FieldType == typeof(ElementalFloat)).ToArray();
            Cache[t] = fields;
        }

        foreach (var f in fields)
        {
            var original = (ElementalFloat) f.GetValue(target);
            if (original == null) continue;

            var clone = new ElementalFloat(original.Value);
            typeof(ElementalFloat).GetProperty("Name")?.SetValue(clone, $"{prefix}.{f.Name}");
            typeof(ElementalFloat).GetProperty("Ship")?.SetValue(clone, ship);
            f.SetValue(target, clone);
        }
    }
}