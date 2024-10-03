using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

[RequireComponent(typeof(PlayerInput))]
public class InputManager : MonoBehaviour
{
    [SerializeField] private SoUniversalInputEvents inputEvents;
    [SerializeField] private SoGameStateEvents gameStateEvents;

    private PlayerInput _playerInput;
    private InputAction _mouseMovement;
    private InputAction _leftClick;
    private InputAction _rightClick;


    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        
        _mouseMovement = _playerInput.actions["MouseMove"];
        _leftClick = _playerInput.actions["LeftClick"];
        _rightClick = _playerInput.actions["rightClick"];
    }

    private void OnEnable()
    {
        _mouseMovement.performed += HandleMouseMove;
        _leftClick.performed += HandleMouseLeftClick;
        _leftClick.canceled += HandleMouseLeftClick;
        _rightClick.performed += HandleMouseRightClick;
    }

    private void OnDisable()
    {
        _mouseMovement.performed -= HandleMouseMove;
        _leftClick.performed -= HandleMouseLeftClick;
        _leftClick.canceled -= HandleMouseLeftClick;
        _rightClick.performed -= HandleMouseRightClick;
    }

    private void HandleMouseMove(InputAction.CallbackContext context)
    {
        if(_rightClick.IsPressed())
        {
            inputEvents.RaiseLookAround(context.ReadValue<Vector2>());
            
            if (gameStateEvents.CurrentPlayerState != PlayerStateEnum.LookAround)
            {
                if (gameStateEvents.CurrentPlayerState == PlayerStateEnum.CardPicked)
                {
                    gameStateEvents.RaiseOnRevertPlayerState();
                }
                gameStateEvents.RaiseOnPlayerStateChange(PlayerStateEnum.LookAround);
            }
        }
        else
        {
            inputEvents.RaiseMouseMove();

            if (gameStateEvents.CurrentPlayerState != PlayerStateEnum.LookAround) return;

            gameStateEvents.RaiseOnRevertPlayerState();
        }
    }

    private void HandleMouseLeftClick(InputAction.CallbackContext context)
    {
        if(context.interaction is HoldInteraction)
        {
            if (context.performed)
            {
                Debug.Log("Mouse Left");
                inputEvents.RaiseCardPick(true);
            }
            else if (context.canceled)
            {
                inputEvents.RaiseCardPick(false);
            }
        }
    }

    private void HandleMouseRightClick(InputAction.CallbackContext context) => inputEvents.RaiseCameraReset(!context.control.IsPressed());
}
