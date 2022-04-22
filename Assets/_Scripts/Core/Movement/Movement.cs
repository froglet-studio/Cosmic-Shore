using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [SerializeField]
    public float speed = 10.0f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += transform.forward * Time.deltaTime * speed;
        float vert = Input.GetAxis("Vertical");
        float horz = Input.GetAxis("Horizontal");
        transform.Rotate(3.0f * Input.GetAxis("Vertical"), 0.0f, -0.1f * Input.GetAxis("Horizontal"));
        //Debug.Log(vert + " " + horz);

    }
}
