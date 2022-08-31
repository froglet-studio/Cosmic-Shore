using UnityEngine;

public class TutorialInput : MonoBehaviour
{
    [SerializeField] bool Left;
    [SerializeField] Vector2 offset;

    void Update()
    {
        if (Input.touches.Length == 2)
        {
            Vector2 leftTouch, rightTouch;

            if (Input.touches[0].position.x <= Input.touches[1].position.x)
            {
                leftTouch = Input.touches[0].position;
                rightTouch = Input.touches[1].position;
            }
            else
            {
                leftTouch = Input.touches[1].position;
                rightTouch = Input.touches[0].position;
            }
            if (Left)
                transform.position = Vector2.Lerp(transform.position,leftTouch + offset,.2f);
            else
                transform.position = Vector2.Lerp(transform.position, rightTouch + offset, .2f);
        }
    }
}
