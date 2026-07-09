namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Canvas hit-tester (original contract: the raycaster the event system queries to
    /// find the Graphic under a pointer). Data surface for now — the actual raycast
    /// (walking the canvas's graphics, honoring <see cref="Graphic.raycastTarget"/> and
    /// sort order) goes REAL in Arc D with EventSystem/PointerEventData; until then the
    /// component carries the authored flags so canvas prefabs transcribe verbatim.
    /// </summary>
    public class GraphicRaycaster : MonoBehaviour
    {
        public enum BlockingObjects { None = 0, TwoD = 1, ThreeD = 2, All = 3 }

        [SerializeField] bool m_IgnoreReversedGraphics = true;
        [SerializeField] BlockingObjects m_BlockingObjects = BlockingObjects.None;

        public bool ignoreReversedGraphics { get => m_IgnoreReversedGraphics; set => m_IgnoreReversedGraphics = value; }
        public BlockingObjects blockingObjects { get => m_BlockingObjects; set => m_BlockingObjects = value; }
    }
}
