using System;

namespace CosmicShore.Engine
{
    // Inspector/serialization marker attributes. Ported code keeps its annotations
    // verbatim; the engine's asset serializer and (future) inspector tooling read them.

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class HeaderAttribute : PropertyAttribute
    {
        public readonly string header;
        public HeaderAttribute(string header) { this.header = header; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class TooltipAttribute : PropertyAttribute
    {
        public readonly string tooltip;
        public TooltipAttribute(string tooltip) { this.tooltip = tooltip; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : PropertyAttribute
    {
        public readonly float min;
        public readonly float max;
        public RangeAttribute(float min, float max) { this.min = min; this.max = max; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinAttribute : PropertyAttribute
    {
        public readonly float min;
        public MinAttribute(float min) { this.min = min; }
    }

    /// <summary>
    /// Inspector slider metadata mirroring Unity.Entities.UI's MinMaxAttribute
    /// (used by FloraConfigurationSO.SpawnProbability). Inert at runtime, like
    /// the rest of this file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinMaxAttribute : PropertyAttribute
    {
        public readonly float min;
        public readonly float max;
        public MinMaxAttribute(float min, float max) { this.min = min; this.max = max; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TextAreaAttribute : PropertyAttribute
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
        public readonly int componentOrder;
        public AddComponentMenuAttribute(string menuName) { this.menuName = menuName; }
        public AddComponentMenuAttribute(string menuName, int order) { this.menuName = menuName; componentOrder = order; }
    }

    /// <summary>HDR/alpha color picker hint for serialized Color fields (inert at runtime).</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ColorUsageAttribute : PropertyAttribute
    {
        public readonly bool showAlpha;
        public readonly bool hdr;
        public ColorUsageAttribute(bool showAlpha) { this.showAlpha = showAlpha; }
        public ColorUsageAttribute(bool showAlpha, bool hdr) { this.showAlpha = showAlpha; this.hdr = hdr; }
    }


    /// <summary>Declares component dependencies (enforced by editor tooling later; inert at runtime).</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponentAttribute : Attribute
    {
        public readonly Type m_Type0, m_Type1, m_Type2;
        public RequireComponentAttribute(Type requiredComponent) { m_Type0 = requiredComponent; }
        public RequireComponentAttribute(Type requiredComponent, Type requiredComponent2) { m_Type0 = requiredComponent; m_Type1 = requiredComponent2; }
        public RequireComponentAttribute(Type requiredComponent, Type requiredComponent2, Type requiredComponent3) { m_Type0 = requiredComponent; m_Type1 = requiredComponent2; m_Type2 = requiredComponent3; }
    }

    /// <summary>Forbids adding the same component twice to one GameObject (inert marker; editor tooling enforces it later).</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class DisallowMultipleComponent : Attribute { }

    /// <summary>Hides a serialized field from inspector tooling (inert marker).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HideInInspectorAttribute : Attribute { }


    /// <summary>Surfaces a method in the component's inspector context menu (inert marker; editor tooling reads it later).</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ContextMenuAttribute : Attribute
    {
        public readonly string menuItem;
        public ContextMenuAttribute(string itemName) { menuItem = itemName; }
    }

    /// <summary>Base class of inspector property decorators (Header, Range, Tooltip, Space …).</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public abstract class PropertyAttribute : Attribute
    {
        public int order { get; set; }
        protected PropertyAttribute() { }
        protected PropertyAttribute(bool applyToCollection) { this.applyToCollection = applyToCollection; }
        public bool applyToCollection { get; }
    }

    /// <summary>Vertical spacing in the inspector (inert at runtime).</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = true)]
    public class SpaceAttribute : PropertyAttribute
    {
        public readonly float height;
        public SpaceAttribute() { height = 8f; }
        public SpaceAttribute(float height) { this.height = height; }
    }

    /// <summary>Multi-line string field hint (inert).</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class MultilineAttribute : PropertyAttribute
    {
        public readonly int lines;
        public MultilineAttribute() { lines = 3; }
        public MultilineAttribute(int lines) { this.lines = lines; }
    }

    /// <summary>Delayed text/number field hint (inert).</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class DelayedAttribute : PropertyAttribute { }

    /// <summary>Inspector context-menu item on a field (inert).</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = true)]
    public sealed class ContextMenuItemAttribute : PropertyAttribute
    {
        public readonly string name, function;
        public ContextMenuItemAttribute(string name, string function) { this.name = name; this.function = function; }
    }

    /// <summary>Inspector header for grouped inspector-only data (inert).</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class InspectorNameAttribute : PropertyAttribute
    {
        public readonly string displayName;
        public InspectorNameAttribute(string displayName) { this.displayName = displayName; }
    }

    /// <summary>Makes a MonoBehaviour's lifecycle run in edit mode too. The port has no edit mode, so it is a marker.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ExecuteAlways : Attribute { }

    /// <summary>Legacy edit-mode execution marker.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ExecuteInEditMode : Attribute { }

    /// <summary>Help URL for a component (inert).</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class HelpURLAttribute : Attribute
    {
        public readonly string URL;
        public HelpURLAttribute(string url) { URL = url; }
    }

    /// <summary>Selection base marker (inert).</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class SelectionBaseAttribute : Attribute { }

    /// <summary>Unity serialization marker for polymorphic managed references.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class SerializeReference : Attribute { }

    /// <summary>Hides a type from the inspector's icon gizmo (inert).</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class IconAttribute : Attribute
    {
        public readonly string path;
        public IconAttribute(string path) { this.path = path; }
    }

    /// <summary>Receives callbacks around serialization; the port's loader calls OnAfterDeserialize after reading fields.</summary>
    public interface ISerializationCallbackReceiver
    {
        void OnBeforeSerialize();
        void OnAfterDeserialize();
    }
}

namespace CosmicShore.Engine.Serialization
{
    /// <summary>
    /// Previous serialized name of a field (original: UnityEngine.Serialization.FormerlySerializedAsAttribute)
    /// — read by the asset pipeline to migrate data written under the old name.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = true)]
    public sealed class FormerlySerializedAsAttribute : System.Attribute
    {
        public FormerlySerializedAsAttribute(string oldName) { this.oldName = oldName; }
        public string oldName { get; }
    }
}
