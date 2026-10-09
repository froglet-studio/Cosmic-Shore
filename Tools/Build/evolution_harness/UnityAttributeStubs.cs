// Harness-only stubs. EvolutionSettings.cs is plain C# apart from UnityEngine's INSPECTOR attributes (Tooltip,
// Header, Min, Range), which exist so a designer gets a documented field. Outside Unity these four empty attributes
// let the same file compile against bare netstandard2.1 - so the harness and the Unity profile gate run the SHIPPED
// core, not a copy of it.
namespace UnityEngine
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class TooltipAttribute : System.Attribute { public TooltipAttribute(string tooltip) { } }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class HeaderAttribute : System.Attribute { public HeaderAttribute(string header) { } }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class MinAttribute : System.Attribute { public MinAttribute(float min) { } }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class RangeAttribute : System.Attribute { public RangeAttribute(float min, float max) { } }
}
