using System;
using Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CnControls
{
    [Flags]
    public enum ControlMovementDirection
    {
        Horizontal = 0x1,
        Vertical = 0x2,
        Both = Horizontal | Vertical
    }

    /// <summary>
    /// Simple joystick class
    /// Contains logic for creating a simple joystick
    /// </summary>
    public class SimpleJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
    {
        /// <summary>
        /// Current event camera reference.
        /// </summary>
        public Camera CurrentEventCamera { get; private set; }

        // ------- Inspector visible variables ---------------------------------------

        /// <summary>
        /// The range in non-scaled pixels for which we can drag the joystick around
        /// </summary>
        public float MovementRange = 50f;

        /// <summary>
        /// The name of the horizontal axis for this joystick to update
        /// </summary>
        public string HorizontalAxisName = "Horizontal";

        /// <summary>
        /// The name of the vertical axis for this joystick to update
        /// </summary>
        public string VerticalAxisName = "Vertical";

        /// <summary>
        /// Should the joystick be hidden when the user releases the finger?
        /// [Space(15f)] attribute is needed only for the editor, it creates some spacing in the inspector
        /// </summary>
        [Space(15f)]
        [Tooltip("Should the joystick be hidden on release?")]
        public bool HideOnRelease;

        /// <summary>
        /// Should the joystick be moved along with the finger
        /// </summary>
        [Tooltip("Should the Base image move along with the finger without any constraints?")]
        public bool MoveBase = true;

        /// <summary>
        /// Should the joystick be moved along with the finger
        /// </summary>
        [Tooltip("Should the joystick snap to finger? If it's FALSE, the MoveBase checkbox logic will be ommited")]
        public bool SnapsToFinger = true;

        /// <summary>
        /// Joystick movement direction
        /// Specifies the axis along which it can move
        /// </summary>
        [Tooltip("Constraints on the joystick movement axis")]
        public ControlMovementDirection JoystickMoveAxis = ControlMovementDirection.Both;

        /// <summary>
        /// Image of the joystick base
        /// </summary>
        [Tooltip("Image of the joystick base")]
        public Image JoystickBase;

        /// <summary>
        /// Image of the stick itself
        /// </summary>
        [Tooltip("Image of the stick itself")]
        public Image Stick;

        /// <summary>
        /// Rect Transform of the touch zone
        /// </summary>
        [Tooltip("Touch Zone transform")]
        public RectTransform TouchZone;

        public Vector2 NormalizedBasePosition { get; private set; }
        public event Action OnDragStart;
        public event Action OnDragEnd;
        public event Action<Vector3> OnDragMove;

        // ---------------------------------------------------------------------------

        private Vector2 _initialStickPosition;
        private Vector2 _intermediateStickPosition;
        private Vector2 _initialBasePosition;
        private RectTransform _baseTransform;
        private RectTransform _stickTransform;

        private bool _pointerDown;
        private Vector2 _pointerPosition;
        private float _oneOverMovementRange;

        protected VirtualAxis HorizintalAxis;
        protected VirtualAxis VerticalAxis;

        private void Awake()
        {
            CurrentEventCamera = Camera.main;

            _stickTransform = Stick.GetComponent<RectTransform>();
            _baseTransform = JoystickBase.GetComponent<RectTransform>();

            _initialStickPosition = _stickTransform.anchoredPosition;
            _intermediateStickPosition = _initialStickPosition;
            _initialBasePosition = _baseTransform.anchoredPosition;

            _stickTransform.anchoredPosition = _initialStickPosition;
            _baseTransform.anchoredPosition = _initialBasePosition;

            _oneOverMovementRange = 1f / MovementRange;

            if (HideOnRelease)
            {
                Hide(true);
            }
        }

        private void OnEnable()
        {
            // When we enable, we get our virtual axis

            HorizintalAxis = HorizintalAxis ?? new VirtualAxis(HorizontalAxisName);
            VerticalAxis = VerticalAxis ?? new VirtualAxis(VerticalAxisName);

            // And register them in our input system
            CnInputManager.RegisterVirtualAxis(HorizintalAxis);
            CnInputManager.RegisterVirtualAxis(VerticalAxis);
        }

        private void OnDisable()
        {
            // When we disable, we just unregister our axis
            // It also happens before the game object is Destroyed
            CnInputManager.UnregisterVirtualAxis(HorizintalAxis);
            CnInputManager.UnregisterVirtualAxis(VerticalAxis);
        }

        private void Update()
        {
            if (_pointerDown && (Input.GetMouseButtonUp(0) || Input.touchCount <= 0))
            {
                _pointerDown = false;
                ProcessPointerUp();
                return;
            }

            if (Input.touchCount <= 0)
            {
                return;
            }

            var touchPosition = Input.GetTouch(0).position;

            if (Input.GetMouseButtonDown(0))
            {
                _pointerDown = true;
                _pointerPosition = touchPosition;
                ProcessPointerDown(new PointerEventData(EventSystem.current)
                {
                    position = touchPosition,
                    pointerId = 0
                }, v2: true);
                return;
            }

            if (_pointerDown && Input.GetMouseButton(0) && Vector2.Distance(_pointerPosition, touchPosition) > 0.1f)
            {
                _pointerPosition = touchPosition;
                ProcessPointerDrag(new PointerEventData(EventSystem.current)
                {
                    position = touchPosition,
                    pointerId = 0
                }, v2: true);
            }
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            ProcessPointerDrag(eventData, v2: false);
        }

        private void ProcessPointerDrag(PointerEventData eventData, bool v2)
        {
            // We get the local position of the joystick
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _stickTransform,
                eventData.position,
                CurrentEventCamera,
                out Vector3 worldJoystickPosition
            );

            // Then we change it's actual position so it snaps to the user's finger
            _stickTransform.position = worldJoystickPosition;
            // We then query it's anchored position. It's calculated internally and quite tricky to do from scratch here in C#
            var stickAnchoredPosition = _stickTransform.anchoredPosition;

            // Some bitwise logic for constraining the joystick along one of the axis
            // If the "Both" option was selected, non of these two checks will yield "true"
            if ((JoystickMoveAxis & ControlMovementDirection.Horizontal) == 0)
            {
                stickAnchoredPosition.x = _intermediateStickPosition.x;
            }
            if ((JoystickMoveAxis & ControlMovementDirection.Vertical) == 0)
            {
                stickAnchoredPosition.y = _intermediateStickPosition.y;
            }

            _stickTransform.anchoredPosition = stickAnchoredPosition;

            // Find current difference between the previous central point of the joystick and it's current position
            Vector2 difference = new Vector2(stickAnchoredPosition.x, stickAnchoredPosition.y) - _intermediateStickPosition;

            // Normalisation stuff
            var diffMagnitude = difference.magnitude;
            var normalizedDifference = difference / diffMagnitude;

            // If the joystick is being dragged outside of it's range
            if (diffMagnitude > MovementRange)
            {
                if (MoveBase && SnapsToFinger)
                {
                    // We move the base so it maps the new joystick center position
                    var baseMovementDifference = difference.magnitude - MovementRange;
                    var addition = normalizedDifference * baseMovementDifference;
                    _baseTransform.anchoredPosition += addition;
                    _intermediateStickPosition += addition;
                }
                else
                {
                    _stickTransform.anchoredPosition = _intermediateStickPosition + normalizedDifference * MovementRange;
                }
            }

            // We should now calculate axis values based on final position and not on "virtual" one
            var finalStickAnchoredPosition = _stickTransform.anchoredPosition;
            // Sanity recalculation
            Vector2 finalDifference = new Vector2(finalStickAnchoredPosition.x, finalStickAnchoredPosition.y) - _intermediateStickPosition;
            // We don't need any values that are greater than 1 or less than -1
            var horizontalValue = Mathf.Clamp(finalDifference.x * _oneOverMovementRange, -1f, 1f);
            var verticalValue = Mathf.Clamp(finalDifference.y * _oneOverMovementRange, -1f, 1f);

            // Finally, we update our virtual axis
            HorizintalAxis.Value = horizontalValue;
            VerticalAxis.Value = verticalValue;

            // Update the normalized position values
            NormalizedBasePosition = new Vector2(
                _baseTransform.anchoredPosition.x.Remap(0f, TouchZone.rect.width, 0f, 1f),
                _baseTransform.anchoredPosition.y.Remap(0f, TouchZone.rect.height, 0f, 1f)
            );

            OnDragMove?.Invoke(finalDifference);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ProcessPointerUp();
        }

        private void ProcessPointerUp()
        {
            // When we lift our finger, we reset everything to the initial state
            _baseTransform.anchoredPosition = _initialBasePosition;
            _stickTransform.anchoredPosition = _initialStickPosition;
            _intermediateStickPosition = _initialStickPosition;

            HorizintalAxis.Value = VerticalAxis.Value = 0f;

            // We also hide it if we specified that behaviour
            if (HideOnRelease)
            {
                Hide(true);
            }

            OnDragEnd?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ProcessPointerDown(eventData, v2: false);
        }

        private void ProcessPointerDown(PointerEventData eventData, bool v2)
        {
            // When we press, we first want to snap the joystick to the user's finger
            if (SnapsToFinger)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(_stickTransform, eventData.position, CurrentEventCamera, out var localStickPosition);
                RectTransformUtility.ScreenPointToWorldPointInRectangle(_baseTransform, eventData.position, CurrentEventCamera, out var localBasePosition);

                _baseTransform.position = localBasePosition;
                _stickTransform.position = localStickPosition;
                _intermediateStickPosition = _stickTransform.anchoredPosition;

                NormalizedBasePosition = new Vector2(
                    _baseTransform.anchoredPosition.x.Remap(0f, TouchZone.rect.width, 0f, 1f),
                    _baseTransform.anchoredPosition.y.Remap(0f, TouchZone.rect.height, 0f, 1f)
                );
            }
            else
            {
                OnDrag(eventData);
            }
            // We also want to show it if we specified that behaviour
            if (HideOnRelease)
            {
                Hide(false);
            }

            OnDragStart?.Invoke();
        }

        /// <summary>
        /// Simple "Hide" behaviour
        /// </summary>
        /// <param name="isHidden">Whether the joystick should be hidden</param>
        private void Hide(bool isHidden)
        {
            JoystickBase.gameObject.SetActive(!isHidden);
            Stick.gameObject.SetActive(!isHidden);
        }

        public void OnServiceInitialize() { }


    }
}
