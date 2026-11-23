using CosmicShore.Engine.UI;

namespace CosmicShore.Engine.InputSystem.UI
{
    /// <summary>What kind of pointer an <see cref="ExtendedPointerEventData"/> came from (original contract: UIPointerType).</summary>
    public enum UIPointerType
    {
        None = 0,
        MouseOrPen = 1,
        Touch = 2,
        Tracked = 3,
    }

    /// <summary>
    /// The Input System UI module's pointer payload (original contract: ExtendedPointerEventData): a
    /// <see cref="PointerEventData"/> that says which kind of pointer sent it, so game code can tell a finger from
    /// a mouse (HangarVesselStage.IsTouch). The engine's module still sends plain <see cref="PointerEventData"/>,
    /// so callers take their non-extended branch here.
    /// </summary>
    public class ExtendedPointerEventData : PointerEventData
    {
        public ExtendedPointerEventData(EventSystem eventSystem) : base(eventSystem) { }

        public UIPointerType pointerType = UIPointerType.None;
    }
}
