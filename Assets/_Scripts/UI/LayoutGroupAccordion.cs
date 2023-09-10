using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LayoutGroupAccordion : MenuAnimator
{
    [SerializeField] float CollapsedSpacing = -26f;
    [SerializeField] float ExpandedSpacing = 12f;
    [SerializeField] VerticalLayoutGroup layoutGroup;

    public void Collapse()
    {
        layoutGroup.spacing = CollapsedSpacing;
    }

    public override void Animate()
    {
        Debug.Log("Animating: " + gameObject.name);
        Collapse();
        base.Animate();
    }

    protected override IEnumerator AnimateCoroutine()
    {
        // Was getting nested graphic rebuild loop errors under certain conditions. with this, we will just wait one frame to begin the animation
        yield return new WaitForEndOfFrame();

        var elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;

            layoutGroup.spacing = Mathf.Lerp(CollapsedSpacing, ExpandedSpacing, Ease(easingType, elapsed / Duration));
            yield return null;
        }
    }
}