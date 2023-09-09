using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LayoutGroupAccordion : MonoBehaviour
{
    [SerializeField] float Duration = 1f;
    [SerializeField] float CollapsedSpacing = -26f;
    [SerializeField] float ExpandedSpacing = 12f;
    [SerializeField] VerticalLayoutGroup layoutGroup;

    public void Collapse()
    {
        layoutGroup.spacing = CollapsedSpacing;
    }

    public void Expand()
    {
        StartCoroutine(ExpandCoroutine());
    }

    IEnumerator ExpandCoroutine()
    {
        var elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;

            layoutGroup.spacing = Mathf.Lerp(CollapsedSpacing, ExpandedSpacing, elapsed / Duration);
            yield return null;
        }
    }
}
