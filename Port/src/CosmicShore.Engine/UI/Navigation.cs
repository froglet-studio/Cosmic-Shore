namespace CosmicShore.Engine.UI
{
    /// <summary>Which way a navigation move points (original contract: MoveDirection).</summary>
    public enum MoveDirection { Left = 0, Up = 1, Right = 2, Down = 3, None = 4 }

    /// <summary>Navigation-move payload (original contract: AxisEventData).</summary>
    public class AxisEventData : BaseEventData
    {
        public AxisEventData(EventSystem eventSystem) : base(eventSystem) { }

        public Vector2 moveVector;
        public MoveDirection moveDir = MoveDirection.None;
    }

    /// <summary>Receives navigation moves (gamepad dpad/stick, arrow keys).</summary>
    public interface IMoveHandler : IEventSystemHandler { void OnMove(AxisEventData eventData); }

    /// <summary>
    /// Per-selectable navigation wiring (original contract: the Navigation struct).
    /// Automatic finds the nearest selectable in the move direction by rect position;
    /// Explicit follows the authored selectOn* references; None opts out.
    /// </summary>
    public struct Navigation
    {
        public enum Mode
        {
            None = 0,
            Horizontal = 1,
            Vertical = 2,
            Automatic = 3,
            Explicit = 4,
        }

        public Mode mode;
        public Selectable selectOnUp;
        public Selectable selectOnDown;
        public Selectable selectOnLeft;
        public Selectable selectOnRight;

        public static Navigation defaultNavigation => new() { mode = Mode.Automatic };
    }
}
