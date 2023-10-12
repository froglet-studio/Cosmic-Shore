using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class ThumbCursor : MonoBehaviour
{
    [SerializeField] bool Left;
    [SerializeField] Vector2 offset;
    [SerializeField] Sprite InactiveImage;
    [SerializeField] Sprite ActiveImage;
    [SerializeField] Player player;

    Image image;
    bool initialized;
    Vector2 leftTouch, rightTouch;

    void Start()
    {
        image = GetComponent<Image>();
        image.sprite = InactiveImage;
        StartCoroutine(InitializeCoroutine());
    }

    // Wait until the input controller is wired up then only show if there is no gamepad and the left one when flying with single stick controls 
    IEnumerator InitializeCoroutine()
    {
        yield return new WaitUntil(() => Player.ActivePlayer != null && Player.ActivePlayer.Ship != null && Player.ActivePlayer.Ship.InputController != null);

        if (!Player.ActivePlayer.Ship.ShipStatus.AutoPilotEnabled)
            gameObject.SetActive(Gamepad.current == null && (Left || !Player.ActivePlayer.Ship.ShipStatus.SingleStickControls));

        initialized = true;
    }

    void Update()
    {
        if (initialized && !Player.ActivePlayer.Ship.ShipStatus.AutoPilotEnabled)
        {
            if (Input.touches.Length == 0)
            {
                transform.position = Left ? Vector2.Lerp(transform.position, Player.ActivePlayer.Ship.InputController.LeftJoystickHome, .2f) : Vector2.Lerp(transform.position, Player.ActivePlayer.Ship.InputController.RightJoystickHome, .2f);
                image.sprite = InactiveImage;
            }
            else if (Left)
            {
                leftTouch = Player.ActivePlayer.Ship.InputController.LeftClampedPosition;
                transform.position = Vector2.Lerp(transform.position, leftTouch, .2f);
                image.sprite = ActiveImage;
            }
            else
            {
                rightTouch = Player.ActivePlayer.Ship.InputController.RightClampedPosition;
                transform.position = Vector2.Lerp(transform.position, rightTouch, .2f);
                image.sprite = ActiveImage;
            }
        }
    }
}