using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonPlayerController : MonoBehaviour
{
    private InputActionsAsset controller;
    private InputAction move;

    private Rigidbody RB;

    [SerializeField]
    private Camera playerCamera;

    //floats
    [SerializeField]
    private float maxSpeed = 5f;
    [SerializeField]
    private float jumpModifier = 5f;
    [SerializeField]
    private float runModifier = 5f;
    [SerializeField]
    private float walkModifier= 3f;
    [SerializeField]
    private bool isWalking = true;
    private Vector3 moveDirection = Vector3.zero;

    private void Awake()
    {
        controller = new InputActionsAsset();
        RB = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        //Subsribe to Player Events
        controller.Player.Jump.started += DoJump;
        controller.Player.Use.started += DoUse;

        move = controller.Player.Move; //Get ref to Move for use in Update
        controller.Player.Enable();  //Set Input Map to Player
    }

    void FixedUpdate()
    {
        // Horizontal Movement
        /*if (isWalking)
        {
            Walk();
        }
        else
        {
            Run();
        }*/
        moveDirection += move.ReadValue<Vector2>().x * GetCameraRight(playerCamera) * runModifier;
        moveDirection += move.ReadValue<Vector2>().y * GetCameraForward(playerCamera) * runModifier;
        RB.AddForce(moveDirection, ForceMode.Impulse);
        moveDirection = Vector3.zero;

        // Controls falling to prevent floaty feeling jumps
        if(RB.velocity.y < 0)
        {
            RB.velocity += Vector3.down * Physics.gravity.y * Time.deltaTime;
        }

        // Controls Max horizontal speed
        Vector3 horizontalVelocity = RB.velocity;
        horizontalVelocity.y = 0;
        if(horizontalVelocity.sqrMagnitude < maxSpeed * maxSpeed)
        {
            RB.velocity = horizontalVelocity.normalized * maxSpeed + Vector3.up * RB.velocity.y;
        }

        // Controls Rotation of Player
        PlayerLookAtMoveDirrection();

        
    }

    private void Walk()
    {
        Debug.Log("Walking");
        moveDirection += move.ReadValue<Vector2>().x * GetCameraRight(playerCamera) * walkModifier;
        moveDirection += move.ReadValue<Vector2>().y * GetCameraForward(playerCamera) * walkModifier;
    }

    private void Run()
    {
        Debug.Log("Runing");
        moveDirection += move.ReadValue<Vector2>().x * GetCameraRight(playerCamera) * runModifier;
        moveDirection += move.ReadValue<Vector2>().y * GetCameraForward(playerCamera) * runModifier;
    }

    private void PlayerLookAtMoveDirrection()
    {
        Vector3 dirrection = RB.velocity;
        dirrection.y = 0;

        if(move.ReadValue<Vector2>().sqrMagnitude > 0.1f && dirrection.sqrMagnitude > 0.1f)
        {
            this.RB.rotation = Quaternion.LookRotation(dirrection, Vector3.up);
        }
        else { RB.angularVelocity = Vector3.zero; }
    }

    private Vector3 GetCameraForward(Camera playerCamera)
    {
        Vector3 forward = playerCamera.transform.position;
        forward.y = 0;
        return forward.normalized;
    }

    private Vector3 GetCameraRight(Camera playerCamera)
    {
        Vector3 right = playerCamera.transform.position;
        right.x = 0;
        return right.normalized;
    }

    private void OnDisable()
    {
        //Unsubsribe to Player Events
        controller.Player.Jump.started -= DoJump;
        controller.Player.Use.started -= DoUse;

        controller.Player.Disable();
    }

    private void DoJump(InputAction.CallbackContext context)
    {
        if (IsGrounded())
        {
            moveDirection = Vector3.up * jumpModifier;
            Debug.Log("Jumped");
        }        
    }

    private void DoUse(InputAction.CallbackContext context)
    {
        //TODO figure out what is going to be used and call Use() on it
        Debug.Log("Used");
    }

    private bool IsGrounded()
    {
        Ray ray = new Ray(this.transform.position + Vector3.up* 0.25f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 0.3f))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    
}
