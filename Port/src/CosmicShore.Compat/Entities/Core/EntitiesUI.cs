using System;

// Unity.Entities.UI (Entities' Properties UI). Its only member the game uses, MinMaxAttribute,
// is an ENGINE type (CosmicShore.Engine.MinMaxAttribute) — redefining it here makes
// FloraConfigurationSO's `[MinMax]` ambiguous (CS0104). This file exists so the namespace the
// game imports resolves; it carries the one base type the original namespace defines.
namespace Unity.Entities.UI
{
    /// <summary>Base of the Properties-UI inspector attributes (metadata only).</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public abstract class InspectorAttribute : Attribute { }
}
