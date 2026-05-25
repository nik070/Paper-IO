using System;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
using System.Collections;

namespace Core
{
     public enum FollowTargetUpdateMode : int
    {
        LateUpdate = 0,
        FixedUpdate = 1
    }

    [ExecuteInEditMode]
    public class FollowTarget : MonoBehaviour
    {
        [Serializable]
        public class EditorReferences
        {
            public Transform target;
            public Vector3 offset;
            public Vector3 offsetRotation;
        }

        private bool IsDampingEnabled => useCameraDamping;

        private readonly float _velocitySmoothingRate = 11.16f;

        private float _currentAngle;
        private float _lerpTime = -1f;
        private Vector3 _lastTargetPosition;
        private Vector3 _lastTargetVelocity;
        private Vector3 _filteredTargetVelocity;
        private float _lastSpeed;
        private float _filteredSpeed;
        private Vector3 _springOffset;
        private Vector3 _springOffsetVelocity;
        private Vector3 _pendingImpulse;
        private bool _hasTargetHistory;
        private Sequence _rotationSequence;
        private bool _isAnimating;

        private Coroutine _focusRoutine;

        public event Action CameraMovedCallback;

        [SerializeField]
        public EditorReferences references =
            new EditorReferences();

        public FollowTargetUpdateMode updateMode =
            FollowTargetUpdateMode.LateUpdate;

        public float lerpSpeed = 1f;
        public float moveSpeed = -1f;

        public bool lockX;
        public bool lockY;

        public bool useRangeX;

        public Vector2 rangeX = Vector2.zero;

        public bool lerping;

        public bool followRotation;

        public bool smoothRotation;

        public float minSmoothAngle = 10;

        [Header("Camera Damping")]
        public bool useCameraDamping;

        public float decelerationThreshold = 0.75f;

        public float decelerationSensitivity = 0.2f;

        public float springReturnTime = 0.25f;

        public float maxSpringOffset = 2f;

        public Transform Target
        {
            get => references.target;
            set
            {
                if (lerping)
                {
                    _lerpTime = 0;
                }

                references.target = value;

                ResetDampingState();
            }
        }

        private void FixedUpdate()
        {
            if (updateMode ==
                FollowTargetUpdateMode.FixedUpdate)
            {
                UpdateInternal();
            }
            else if (IsDampingEnabled &&
                     references.target != null &&
                     !_isAnimating)
            {
                SampleDampingVelocity(
                    references.target.position +
                    references.offset,
                    Time.fixedDeltaTime);
            }
        }

        private void LateUpdate()
        {
            if (updateMode ==
                FollowTargetUpdateMode.LateUpdate)
            {
                UpdateInternal();
            }
        }

        private void OnEnable()
        {
            ResetDampingState();
        }

        // =====================================================
        // TEMP FOCUS
        // =====================================================

        public void FocusTemporary(
            Transform tempTarget,
            float focusTime = 0.5f,
            float moveDuration = 0.5f)
        {
            if (_focusRoutine != null)
            {
                StopCoroutine(_focusRoutine);
            }

            _focusRoutine =
                StartCoroutine(
                    FocusRoutine(
                        tempTarget,
                        focusTime,
                        moveDuration));
        }

        private IEnumerator FocusRoutine(
            Transform tempTarget,
            float focusTime,
            float moveDuration)
        {
            if (tempTarget == null)
                yield break;

            _isAnimating = true;

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            Vector3 targetPos =
                tempTarget.position +
                references.offset;

            Quaternion targetRot =
                Quaternion.Euler(
                    tempTarget.rotation.eulerAngles +
                    references.offsetRotation);

            float time = 0f;

            // MOVE TO TARGET
            while (time < moveDuration)
            {
                time += Time.deltaTime;

                float t = time / moveDuration;

                transform.position =
                    Vector3.Lerp(
                        startPos,
                        targetPos,
                        t);

                if (followRotation)
                {
                    transform.rotation =
                        Quaternion.Slerp(
                            startRot,
                            targetRot,
                            t);
                }

                yield return null;
            }

            // WAIT
            yield return new WaitForSeconds(focusTime);

            // RETURN TO PLAYER
            float returnSmoothTime = 0.18f;

            Vector3 velocity = Vector3.zero;

            while (true)
            {
                if (references.target == null)
                    break;

                Vector3 livePlayerPos =
                    references.target.position +
                    references.offset;

                Quaternion livePlayerRot =
                    Quaternion.Euler(
                        references.target.rotation.eulerAngles +
                        references.offsetRotation);

                transform.position =
                    Vector3.SmoothDamp(
                        transform.position,
                        livePlayerPos,
                        ref velocity,
                        returnSmoothTime);

                if (followRotation)
                {
                    transform.rotation =
                        Quaternion.Slerp(
                            transform.rotation,
                            livePlayerRot,
                            Time.deltaTime * 8f);
                }

                if (Vector3.Distance(
                        transform.position,
                        livePlayerPos) < 0.01f)
                {
                    break;
                }

                yield return null;
            }

            // PRELOAD HISTORY
            Vector3 currentTargetPos =
                references.target.position +
                references.offset;

            _lastTargetPosition =
                currentTargetPos;

            _lastTargetVelocity =
                Vector3.zero;

            _filteredTargetVelocity =
                Vector3.zero;

            _lastSpeed = 0f;

            _filteredSpeed = 0f;

            _hasTargetHistory = true;

            _isAnimating = false;
        }

        // =====================================================

        public void AnimateTo(
            float duration,
            float animationTimePassed,
            Ease ease = Ease.Unset)
        {
            if (references.target != null)
            {
                _isAnimating = true;

                transform.DOKill(true);

                Vector3 newPos =
                    references.target.position +
                    references.offset;

                TweenerCore<Vector3,
                    Vector3,
                    VectorOptions> transformTween =
                    transform.DOMove(newPos, duration)
                        .SetEase(ease)
                        .OnComplete(() =>
                        {
                            _isAnimating = false;
                        });

                ApplyStartOffset(
                    transformTween,
                    animationTimePassed);

                if (followRotation)
                {
                    Vector3 newRot =
                        references.target.rotation.eulerAngles +
                        references.offsetRotation;

                    TweenerCore<Quaternion,
                        Vector3,
                        QuaternionOptions> rotationTween =
                        transform.DORotate(
                                newRot,
                                duration)
                            .SetEase(ease);

                    ApplyStartOffset(
                        rotationTween,
                        animationTimePassed);
                }
            }
        }

        public void SetInstantly()
        {
            if (references.target != null)
            {
                SetInstantly(
                    references.target.position,
                    references.target.rotation);
            }
        }

        public void SetInstantly(
            Vector3 targetPos,
            Quaternion targetRot)
        {
            ResetDampingState();

            transform.position =
                targetPos + references.offset;

            if (followRotation)
            {
                transform.rotation =
                    Quaternion.Euler(
                        targetRot.eulerAngles +
                        references.offsetRotation);
            }
        }

        private static void ApplyStartOffset(
            Tween tween,
            float duration)
        {
            if (duration > 0)
            {
                tween.Goto(duration, true);
            }
            else
            {
                tween.SetDelay(Mathf.Abs(duration));
            }
        }

        private void ApplyDampingSpring(float deltaTime)
        {
            if (!IsDampingEnabled)
            {
                _springOffset = Vector3.zero;
                _pendingImpulse = Vector3.zero;
                return;
            }

            _springOffset += _pendingImpulse;

            _pendingImpulse = Vector3.zero;

            _springOffset =
                Vector3.ClampMagnitude(
                    _springOffset,
                    maxSpringOffset);

            if (deltaTime > 0f)
            {
                _springOffset =
                    Vector3.SmoothDamp(
                        _springOffset,
                        Vector3.zero,
                        ref _springOffsetVelocity,
                        springReturnTime,
                        Mathf.Infinity,
                        deltaTime);
            }
        }

        private void ResetDampingState()
        {
            _lastTargetPosition = Vector3.zero;
            _lastTargetVelocity = Vector3.zero;
            _filteredTargetVelocity = Vector3.zero;
            _lastSpeed = 0f;
            _filteredSpeed = 0f;
            _springOffset = Vector3.zero;
            _springOffsetVelocity = Vector3.zero;
            _pendingImpulse = Vector3.zero;
            _hasTargetHistory = false;
        }

        private void SampleDampingVelocity(
            Vector3 targetPosition,
            float deltaTime)
        {
            if (!IsDampingEnabled ||
                deltaTime <= 0f)
            {
                return;
            }

            if (!_hasTargetHistory)
            {
                _lastTargetPosition =
                    targetPosition;

                _lastTargetVelocity =
                    Vector3.zero;

                _filteredTargetVelocity =
                    Vector3.zero;

                _lastSpeed = 0f;

                _filteredSpeed = 0f;

                _hasTargetHistory = true;

                return;
            }

            Vector3 displacement =
                targetPosition -
                _lastTargetPosition;

            if (displacement.sqrMagnitude >
                maxSpringOffset *
                maxSpringOffset)
            {
                _lastTargetPosition =
                    targetPosition;

                _lastTargetVelocity =
                    Vector3.zero;

                _filteredTargetVelocity =
                    Vector3.zero;

                _lastSpeed = 0f;

                _filteredSpeed = 0f;

                _springOffset = Vector3.zero;

                _springOffsetVelocity =
                    Vector3.zero;

                _pendingImpulse =
                    Vector3.zero;

                return;
            }

            Vector3 targetVelocity =
                displacement / deltaTime;

            float alpha =
                1f -
                Mathf.Exp(
                    -_velocitySmoothingRate *
                    deltaTime);

            _filteredTargetVelocity =
                Vector3.Lerp(
                    _filteredTargetVelocity,
                    targetVelocity,
                    alpha);

            float targetSpeed =
                targetVelocity.magnitude;

            _filteredSpeed =
                Mathf.Lerp(
                    _filteredSpeed,
                    targetSpeed,
                    alpha);

            float currentSpeed =
                _filteredSpeed;

            float previousSpeed =
                _lastSpeed;

            float speedDelta =
                currentSpeed -
                previousSpeed;

            if (speedDelta <
                -decelerationThreshold &&
                previousSpeed >
                Mathf.Epsilon)
            {
                _pendingImpulse +=
                    _lastTargetVelocity.normalized *
                    (-speedDelta *
                     decelerationSensitivity);
            }

            _lastTargetPosition =
                targetPosition;

            _lastTargetVelocity =
                _filteredTargetVelocity;

            _lastSpeed =
                currentSpeed;
        }

        private void UpdateInternal()
        {
            if (references.target != null &&
                !_isAnimating)
            {
                float deltaTime =
                    updateMode ==
                    FollowTargetUpdateMode.FixedUpdate
                        ? Time.fixedDeltaTime
                        : Time.deltaTime;

                Vector3 targetPosition =
                    references.target.position +
                    references.offset;

                if (updateMode ==
                    FollowTargetUpdateMode.FixedUpdate)
                {
                    SampleDampingVelocity(
                        targetPosition,
                        deltaTime);
                }

                ApplyDampingSpring(deltaTime);

                if (_lerpTime > -1)
                {
                    _lerpTime +=
                        lerpSpeed *
                        deltaTime;

                    transform.position =
                        Vector3.Lerp(
                            transform.position,
                            targetPosition,
                            _lerpTime);

                    if (_lerpTime > 1)
                    {
                        _lerpTime = -1;
                    }
                }
                else if (moveSpeed > -1)
                {
                    transform.position =
                        Vector3.Lerp(
                            transform.position,
                            targetPosition,
                            moveSpeed *
                            deltaTime);
                }
                else
                {
                    if (followRotation)
                    {
                        transform.rotation =
                            Quaternion.Euler(
                                references.target.rotation.eulerAngles +
                                references.offsetRotation);
                    }

                    transform.position =
                        Vector3.Lerp(
                            transform.position,
                            targetPosition + _springOffset,
                            12f * deltaTime);
                }

                CameraMovedCallback?.Invoke();
            }
        }
    }
}
