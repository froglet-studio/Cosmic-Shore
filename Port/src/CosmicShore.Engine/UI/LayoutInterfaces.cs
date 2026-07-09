namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// A component that reports layout sizes to its parent layout controller
    /// (original contract). Sizes are consumed per axis in two passes: horizontal
    /// inputs are calculated (bottom-up) and applied (top-down) before vertical —
    /// see <see cref="LayoutRebuilder.ForceRebuildLayoutImmediate"/>.
    /// </summary>
    public interface ILayoutElement
    {
        /// <summary>Compute the horizontal inputs below. Called bottom-up, before any width is read.</summary>
        void CalculateLayoutInputHorizontal();

        /// <summary>Compute the vertical inputs below. Called after the horizontal pass has set widths.</summary>
        void CalculateLayoutInputVertical();

        float minWidth { get; }
        float preferredWidth { get; }
        float flexibleWidth { get; }
        float minHeight { get; }
        float preferredHeight { get; }
        float flexibleHeight { get; }

        /// <summary>Higher-priority elements override lower ones when several sit on one GameObject.</summary>
        int layoutPriority { get; }
    }

    /// <summary>A component that sets layout state (its own or its children's) during the control passes.</summary>
    public interface ILayoutController
    {
        void SetLayoutHorizontal();
        void SetLayoutVertical();
    }

    /// <summary>A controller that lays out its CHILDREN (layout groups).</summary>
    public interface ILayoutGroup : ILayoutController { }

    /// <summary>A controller that sizes ITSELF (fitters) — applied before sibling group controllers.</summary>
    public interface ILayoutSelfController : ILayoutController { }

    /// <summary>Opts a child out of its parent group's layout (original contract).</summary>
    public interface ILayoutIgnorer
    {
        bool ignoreLayout { get; }
    }
}
