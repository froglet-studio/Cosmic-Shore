using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisplayDiscManager : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> displayPositions;

    [SerializeField]
    private List<Transform> lookAtTransforms;

    public string selectedGameObject;

    [SerializeField]
    private Vector3 clockwise;
    [SerializeField]
    private Vector3 counterclockwise;

    [SerializeField]
    private float speed = 30f;

    //private int index = 0f;

    private void Start()
    {
        
    }
    // Update is called once per frame
    void Update()
    {
        //AutoRotateDisplayCarouselClockwise();
    }

    public void AutoRotateDisplayCarouselClockwise()
    {
        transform.Rotate(clockwise * Time.deltaTime * speed);
    }

    public void DisplayPosition(int index)
    {
        transform.LookAt(lookAtTransforms[index]);
        UpDateSelectedGameObject(displayPositions[index]);
    }

    private void UpDateSelectedGameObject(GameObject currentDisplayPosition)
    {
        selectedGameObject = currentDisplayPosition.name;
    }
}
