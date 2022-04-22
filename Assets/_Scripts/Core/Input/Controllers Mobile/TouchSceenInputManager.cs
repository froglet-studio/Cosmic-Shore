using UnityEngine;
using UnityEngine.InputSystem;
using Amoebius.Utility.Singleton;
using UnityEngine.InputSystem.EnhancedTouch;

[DefaultExecutionOrder(-1)]
public class TouchSceenInputManager : SingletonPersistent<TouchSceenInputManager>
{
    #region Touch Events
    public delegate void StartTouch(Vector2 position, float time);
    public event StartTouch OnStartTouch;                                   // OnStartTouch Event
    public delegate void EndTouch(Vector2 position, float time);
    public event EndTouch OnEndTouch;                                       // OnEndTouch Event
    #endregion

    #region Varriables
    private InputActionsAsset touchControls;
    private Camera mainCamera;
    #endregion

    public override void Awake()
    {
        base.Awake();
        touchControls = new InputActionsAsset();
        mainCamera = Camera.main; 
    }

    private void OnEnable()
    {        
        touchControls.Enable();
        TouchSimulation.Enable(); //might be enabled already in Input Debugger
    }

    private void Start()
    {
        touchControls.Touch.PrimaryContact.started += ctx => StartPrimaryTouch(ctx);
        touchControls.Touch.PrimaryContact.canceled += ctx => EndPrimaryTouch(ctx);
    }

    private void StartPrimaryTouch(InputAction.CallbackContext ctx)         // Primary Touch starts
    {
        //Debug.Log("Ctx: " + ctx);
        if (OnStartTouch != null)
        {
            Debug.Log("Touch Started");
            OnStartTouch(Utils.ScreenToWorld2D(mainCamera, touchControls.Touch.PrimaryPosition.ReadValue<Vector2>()), (float)ctx.startTime);
        }
        
    }
    
    private void EndPrimaryTouch(InputAction.CallbackContext ctx)           // Primary Touch ends
    {
        if (OnEndTouch != null)
        {
            Debug.Log("Touch Ended");
            OnEndTouch(Utils.ScreenToWorld2D(mainCamera, touchControls.Touch.PrimaryPosition.ReadValue<Vector2>()), (float)ctx.time);
        }
    }

    //Returns current primary touch position for drawing
    private Vector2 PrimaryPosition() 
    {
        return Utils.ScreenToWorld2D(mainCamera, touchControls.Touch.PrimaryPosition.ReadValue<Vector2>());
    }

    private void OnDisable()
    {
        touchControls.Touch.PrimaryContact.started -= ctx => StartPrimaryTouch(ctx);
        touchControls.Touch.PrimaryContact.canceled -= ctx => EndPrimaryTouch(ctx);
        touchControls.Disable();
        TouchSimulation.Disable();
    }


}