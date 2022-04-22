using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Use to move a GameObject in the forward direction
/// </summary>

namespace StarWriter.Movement
{
    public class Mover : MonoBehaviour
    {
        [SerializeField]
        private float speed = 5f;


        // Update is called once per frame
        void Update()
        {
            transform.position += speed * Time.deltaTime * transform.forward;
        }
    }
}

