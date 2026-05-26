using CnControls;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    [RequireComponent(typeof(CharacterMotor))]
    public class PlayerController : MonoBehaviour, IController
    {
        [FormerlySerializedAs("turnThreshold")]
        [Space]
        [Header("Input Settings")]
        [SerializeField]
        private float _turnThreshold = 10f;

        private Vector3 _lastMovement;
        private CharacterMotor _characterMotor;
        private Vector3 _mouseStartPos;

        public void Init(CharacterSpawnConfig config)
        {
            _characterMotor = GetComponent<CharacterMotor>();
        }

        private void Update()
        {
            // Sample input every frame so we never miss a touch/mouse event.
            // FixedUpdate can skip frames when the frame-rate exceeds the physics rate,
            // causing swallowed inputs and unresponsive-feeling controls.
            UpdateMovementVector();
        }

        private Vector3 _lastSwipeDir = Vector3.zero;

        private void UpdateMovementVector()
        {
            // 1. Try the virtual joystick axes first (SimpleJoystick feeds these).
            Vector3 movementVector = new Vector3(
                CnInputManager.GetAxis("Horizontal"),
                CnInputManager.GetAxis("Vertical"), 0f);

            // Apply a deadzone to the virtual joystick so it's not hyper-sensitive to tiny touches
            if (movementVector.magnitude < 0.15f)
            {
                movementVector = Vector3.zero;
            }

            // 2. Fall back to direct mouse/touch swipe if the joystick gives nothing.
            if (movementVector.sqrMagnitude < 0.00001f)
            {
                movementVector = GetSwipeInput();
            }

            // Normalize so direction is unit-length regardless of source.
            if (movementVector.sqrMagnitude > 0.00001f)
            {
                movementVector = movementVector.normalized;
            }

            _lastMovement = movementVector;
            _characterMotor.SetLastMovement(_lastMovement);
        }

        /// <summary>
        /// Reads raw mouse / touch drag and converts it into a movement direction using a floating joystick approach.
        /// </summary>
        private Vector3 GetSwipeInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _mouseStartPos = Input.mousePosition;
                _lastSwipeDir = Vector3.zero;
            }
            else if (Input.GetMouseButton(0))
            {
                Vector3 mousePos = Input.mousePosition;
                float distance = Vector3.Distance(_mouseStartPos, mousePos);
                
                // If they drag far enough, start registering movement
                if (distance > _turnThreshold)
                {
                    Vector3 curDir2D = (mousePos - _mouseStartPos).normalized;
                    
                    // Smoothly interpolate the swipe direction so rapid finger wiggles don't cause instant snaps
                    if (_lastSwipeDir == Vector3.zero) 
                        _lastSwipeDir = new Vector3(curDir2D.x, curDir2D.y, 0f);
                    else 
                        _lastSwipeDir = Vector3.Slerp(_lastSwipeDir, new Vector3(curDir2D.x, curDir2D.y, 0f), Time.deltaTime * 15f);
                    
                    // Floating joystick effect: pull the base towards the finger if dragged too far.
                    // Using 10% of the screen height (or min 150px) gives a wide, natural thumb-stick radius.
                    // Previously this was too small, making tiny finger movements cause drastic twitchy turns.
                    float maxDragRadius = Mathf.Max(150f, Screen.height * 0.1f); 
                    if (distance > maxDragRadius)
                    {
                        // Pull the center along so it's never further than maxDragRadius from the finger
                        _mouseStartPos += (mousePos - _mouseStartPos).normalized * (distance - maxDragRadius);
                    }
                }

                // If finger is currently dragged away from the (floating) center, keep moving
                if (Vector3.Distance(_mouseStartPos, mousePos) > _turnThreshold * 0.5f)
                {
                    return _lastSwipeDir;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _lastSwipeDir = Vector3.zero;
            }

            return Vector3.zero;
        }
    }
}
