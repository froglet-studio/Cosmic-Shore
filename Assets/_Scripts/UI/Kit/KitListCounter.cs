using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Counts the rows a list content holds and keeps the list's chrome in step with it: a count
    /// label, the parts that only make sense while the list has rows (a section header, its divider)
    /// and the line shown while it has none. Driven by <c>OnTransformChildrenChanged</c>, so it
    /// follows whoever spawns and destroys the rows without that code knowing it exists - the party
    /// panel's lists are filled by <see cref="FriendsListPanel"/>, which never had a count.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit List Counter")]
    public class KitListCounter : MonoBehaviour
    {
        [Tooltip("Shows the number of rows. Optional.")]
        [SerializeField] private KitText countText;
        [Tooltip("Shown only while the list has at least one row (its header, its divider).")]
        [SerializeField] private CanvasGroup[] shownWhileFilled = { };
        [Tooltip("Shown only while the list is empty (the empty-state line).")]
        [SerializeField] private CanvasGroup shownWhileEmpty;

        public int Count { get; private set; } = -1;

        private void OnEnable() => Recount();

        private void OnTransformChildrenChanged() => Recount();

        public void Recount()
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).gameObject.activeSelf) count++;

            if (count == Count) return;
            Count = count;

            if (countText) countText.SetText(count.ToString());
            foreach (var group in shownWhileFilled)
                KitVisibility.SetShown(group, count > 0);
            KitVisibility.SetShown(shownWhileEmpty, count == 0);
        }
    }
}
