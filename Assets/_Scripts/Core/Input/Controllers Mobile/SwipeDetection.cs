using System;
using UnityEngine;
using TMPro;

public class SwipeDetection : MonoBehaviour
{
    #region Variables
    private TouchSceenInputManager inputManager;

    public TextMeshProUGUI directionalText;

    private Vector2 startPosition;
    private Vector2 endPosition;

    //floats
    private float startTime;
    private float endTime;
    [SerializeField]
    private float maxTime = 1f;
    [SerializeField]
    private float minDistance = 0.1f;
    [SerializeField, Range(0f, 1f)]
    private float directionThreshold = 0.9f;

    public Color color = Color.red;
    #endregion

    private void Awake()
    {
        inputManager = TouchSceenInputManager.Instance; //GetComponent<TouchSceenInputManager>();
    }

    #region Event Registration
    private void OnEnable()
    {
        inputManager.OnStartTouch += StartSwipe;
        inputManager.OnEndTouch += EndSwipe;
    }

    private void OnDisable()
    {
        inputManager.OnStartTouch -= StartSwipe;
        inputManager.OnEndTouch -= EndSwipe;
    }
    #endregion

    private void StartSwipe(Vector2 position, float time)                           // get Start Swipe position
    {
        Debug.Log("Started Swipe");
        startPosition = position;
        startTime = time;
    }

    private void EndSwipe(Vector2 position, float time)                             //  get End Swipe position
    {
        Debug.Log("Ended Swipe");
        endPosition = position;
        endTime = time;
        DetectSwipe();
    }                           

    private void DetectSwipe()                                                      // Confirm swipe and draw it
    {
        if(Vector3.Distance(startPosition, endPosition) >= minDistance && (endTime - startTime) <= maxTime)
        {
            Debug.Log("Swipe Detected");
            Debug.DrawLine(startPosition, endPosition, color, 5f, false);
            Vector3 direction = endPosition - startPosition;
            Vector2 direction2D = new Vector2(direction.x, direction.y).normalized;
            SwipeDirection(direction2D);
        }
    }                                                                                                       

    private void SwipeDirection(Vector2 direction2D)                                //Confirm swipes dirrection
    {
        if(Vector2.Dot(Vector2.up, direction2D) > directionThreshold)
        {
            Debug.Log("Swiped Up!");
            directionalText.text = "Up!";

        }
        if (Vector2.Dot(Vector2.down, direction2D) > directionThreshold)
        {
            Debug.Log("Swiped Down!");
            directionalText.text = "Down!";
        }
        if (Vector2.Dot(Vector2.left, direction2D) > directionThreshold)
        {
            Debug.Log("Swiped Left!");
            directionalText.text = "Left!";
        }
        if (Vector2.Dot(Vector2.right, direction2D) > directionThreshold)
        {
            Debug.Log("Swiped Right!");
            directionalText.text = "Right!";
        }
    }
}
