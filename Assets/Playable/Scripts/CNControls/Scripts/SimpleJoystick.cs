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

        /// <summary>
        /// Dead zone as a fraction of MovementRange (0–1). Inputs within this
        /// radius are snapped to zero so tiny accidental touches don't move the player.
        /// </summary>
        [Space(10f)]
        [Header("Feel Tuning")]
        [Tooltip("Fraction of stick travel that is ignored (0-1). Default 0.1 = 10%.")]
        [Range(0f, 0.5f)]
        public float DeadZone = 0.1f;

        /// <summary>
        /// How fast the output axis values interpolate toward the target.
        /// Higher = snappier, lower = floatier.
        /// </summary>
        [Tooltip("Axis smoothing speed. 10 = responsive, 5 = floaty, 20 = near-instant.")]
        public float SmoothSpeed = 10f;

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

        private float _oneOverMovementRange;

        // Pointer tracking — ensures we only respond to the finger that started the drag
        private int _activePointerId = -1;
        private bool _isDragging;

        // Smoothed output values (written to virtual axes in LateUpdate)
        private float _rawHorizontal;
        private float _rawVertical;
        private float _smoothedHorizontal;
        private float _smoothedVertical;

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

        /// <summary>
        /// Smoothly interpolate output axis values every frame so the character
        /// steers into direction changes rather than snapping.
        /// </summary>
        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            float lerpFactor = 1f - Mathf.Exp(-SmoothSpeed * dt);

            _smoothedHorizontal = Mathf.Lerp(_smoothedHorizontal, _rawHorizontal, lerpFactor);
            _smoothedVertical = Mathf.Lerp(_smoothedVertical, _rawVertical, lerpFactor);

            // Snap to zero when close enough to avoid lingering micro-values
            if (Mathf.Abs(_smoothedHorizontal) < 0.001f) _smoothedHorizontal = 0f;
            if (Mathf.Abs(_smoothedVertical) < 0.001f) _smoothedVertical = 0f;

            HorizintalAxis.Value = _smoothedHorizontal;
            VerticalAxis.Value = _smoothedVertical;
        }

        // ===================== EventSystem Callbacks =====================

        public void OnPointerDown(PointerEventData eventData)
        {
            // Ignore if we're already tracking a finger
            if (_isDragging)
                return;

            _activePointerId = eventData.pointerId;
            _isDragging = true;

            // When we press, we first want to snap the joystick to the user's finger
            if (SnapsToFinger)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _stickTransform.parent as RectTransform,
                    eventData.position,
                    CurrentEventCamera,
                    out Vector2 localPoint
                );

                _baseTransform.anchoredPosition = localPoint;
                _stickTransform.anchoredPosition = localPoint;
                _intermediateStickPosition = localPoint;

                NormalizedBasePosition = new Vector2(
                    _baseTransform.anchoredPosition.x.Remap(0f, TouchZone.rect.width, 0f, 1f),
                    _baseTransform.anchoredPosition.y.Remap(0f, TouchZone.rect.height, 0f, 1f)
                );
            }
            else
            {
                // Process as an immediate drag so the stick jumps to the finger
                ProcessDrag(eventData);
            }

            // We also want to show it if we specified that behaviour
            if (HideOnRelease)
            {
                Hide(false);
            }

            OnDragStart?.Invoke();
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            // Only respond to the finger that started the drag
            if (eventData.pointerId != _activePointerId)
                return;

            ProcessDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Only respond to the finger that started the drag
            if (eventData.pointerId != _activePointerId)
                return;

            _isDragging = false;
            _activePointerId = -1;

            // When we lift our finger, we reset everything to the initial state
            _baseTransform.anchoredPosition = _initialBasePosition;
            _stickTransform.anchoredPosition = _initialStickPosition;
            _intermediateStickPosition = _initialStickPosition;

            // Set raw targets to zero — LateUpdate will smoothly ramp down
            _rawHorizontal = 0f;
            _rawVertical = 0f;

            // We also hide it if we specified that behaviour
            if (HideOnRelease)
            {
                Hide(true);
            }

            OnDragEnd?.Invoke();
        }

        // ===================== Core Drag Processing =====================

        private void ProcessDrag(PointerEventData eventData)
        {
            // Convert screen position to local position in the parent RectTransform
            RectTransform parentRect = _stickTransform.parent as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                CurrentEventCamera,
                out Vector2 localPoint
            );

            // Set stick to the local point
            Vector2 stickPosition = localPoint;

            // Apply axis constraints
            if ((JoystickMoveAxis & ControlMovementDirection.Horizontal) == 0)
            {
                stickPosition.x = _intermediateStickPosition.x;
            }
            if ((JoystickMoveAxis & ControlMovementDirection.Vertical) == 0)
            {
                stickPosition.y = _intermediateStickPosition.y;
            }

            _stickTransform.anchoredPosition = stickPosition;

            // Compute offset from the joystick center
            Vector2 difference = stickPosition - _intermediateStickPosition;
            float diffMagnitude = difference.magnitude;

            // If the joystick is being dragged outside of its range
            if (diffMagnitude > MovementRange)
            {
                Vector2 normalizedDiff = difference / diffMagnitude;

                if (MoveBase && SnapsToFinger)
                {
                    // Move the base so it follows the finger
                    float overshoot = diffMagnitude - MovementRange;
                    Vector2 baseShift = normalizedDiff * overshoot;
                    _baseTransform.anchoredPosition += baseShift;
                    _intermediateStickPosition += baseShift;
                }
                else
                {
                    // Clamp the stick to the edge of the movement range
                    _stickTransform.anchoredPosition = _intermediateStickPosition + normalizedDiff * MovementRange;
                }
            }

            // Recalculate final difference after any clamping / base movement
            Vector2 finalPosition = _stickTransform.anchoredPosition;
            Vector2 finalDifference = finalPosition - _intermediateStickPosition;
            float finalMagnitude = finalDifference.magnitude;

            // Compute normalized axis values (-1 to 1)
            float horizontalValue = Mathf.Clamp(finalDifference.x * _oneOverMovementRange, -1f, 1f);
            float verticalValue = Mathf.Clamp(finalDifference.y * _oneOverMovementRange, -1f, 1f);

            // Apply dead zone with rescaling so the usable range remains 0→1
            float magnitude = new Vector2(horizontalValue, verticalValue).magnitude;
            if (magnitude < DeadZone)
            {
                horizontalValue = 0f;
                verticalValue = 0f;
            }
            else if (DeadZone > 0f)
            {
                // Rescale so the edge of the dead zone maps to 0 and full deflection maps to 1
                float rescaled = (magnitude - DeadZone) / (1f - DeadZone);
                float scale = rescaled / magnitude;
                horizontalValue *= scale;
                verticalValue *= scale;
            }

            // Write to raw targets — LateUpdate will smooth these before writing to virtual axes
            _rawHorizontal = horizontalValue;
            _rawVertical = verticalValue;

            // Update the normalized position values
            NormalizedBasePosition = new Vector2(
                _baseTransform.anchoredPosition.x.Remap(0f, TouchZone.rect.width, 0f, 1f),
                _baseTransform.anchoredPosition.y.Remap(0f, TouchZone.rect.height, 0f, 1f)
            );

            OnDragMove?.Invoke(finalDifference);
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
