using System;

namespace CosmicShore.Engine
{
    // Inspector/serialization marker attributes. Ported code keeps its annotations
    // verbatim; the engine's asset serializer and (future) inspector tooling read them.

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class HeaderAttribute : Attribute
    {
        public readonly string header;
        public HeaderAttribute(string header) { this.header = header; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public readonly string tooltip;
        public TooltipAttribute(string tooltip) { this.tooltip = tooltip; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    {
        public readonly float min;
        public readonly float max;
        public RangeAttribute(float min, float max) { this.min = min; this.max = max; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinAttribute : Attribute
    {
        public readonly float min;
        public MinAttribute(float min) { this.min = min; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TextAreaAttribute : Attribute
    {
        public readonly int minLines;
        public readonly int maxLines;
        public TextAreaAttribute() { minLines = 3; maxLines = 3; }
        public TextAreaAttribute(int minLines, int maxLines) { this.minLines = minLines; this.maxLines = maxLines; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName;
        public string fileName;
        public int order;
    }

    /// <summary>
    /// Orders lifecycle callbacks across behaviour types (lower runs earlier).
    /// Same contract as the original engine attribute (e.g. AppManager at -100).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrderAttribute : Attribute
    {
        public readonly int order;
        public DefaultExecutionOrderAttribute(int order) { this.order = order; }
    }

    /// <summary>Editor add-component menu path (inert until editor tooling exists).</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddComponentMenuAttribute : Attribute
    {
        public readonly string menuName;
        public AddComponentMenuAttribute(string menuName) { this.menuName = menuName; }
    }

    /// <summary>
    /// Previous serialized name of a field — read by the asset pipeline (content phase)
    /// to migrate data written under the old name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class FormerlySerializedAsAttribute : Attribute
    {
        public readonly string oldName;
        public FormerlySerializedAsAttribute(string oldName) { this.oldName = oldName; }
    }

    /// <summary>Keeps the annotated member through code stripping (inert marker for now).</summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    public sealed class PreserveAttribute : Attribute { }
}
