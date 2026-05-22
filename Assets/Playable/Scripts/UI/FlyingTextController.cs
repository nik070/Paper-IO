using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace UI
{
    public struct FlyingTextData
    {
        public GameplayFlyingTextType Type;
        public float Value;
        public Color Color;
        public Transform Origin;
        public float Delay;
    }

    public class FlyingTextController : SingletonBehaviour<FlyingTextController>
    {
        private const int PrewarmCount = 3;

        private const int LowClutterThreshold = 2;
        private const int HighClutterThreshold = 6;
        private const float HighClutterAcceleration = 0.1f;

        // Watchdog: if the processing coroutine appears alive but hasn't ticked in this
        // many seconds, assume it died silently (e.g. killed by GameObject deactivation
        // we missed, exception swallowed by Unity, etc.) and restart it. Animation
        // duration is 1.5s + interval 0.35s, so 5s leaves plenty of headroom.
        private const float WatchdogTimeoutSeconds = 5f;

        private readonly List<GameplayFlyingTextView> _pool = new List<GameplayFlyingTextView>();
        private readonly Queue<FlyingTextData> _queue = new Queue<FlyingTextData>();

        private Coroutine _processRoutine;
        private float _lastTickTime;
        private bool _forceStop;

        [SerializeField] private GameplayFlyingTextView _prefab;
        [SerializeField] private Transform _parent;
        [SerializeField] private Transform _origin;
        [SerializeField] private float _interval = 0.35f;

        public event Action<FlyingTextData> FlyingTextTriggered;

        protected override void Awake()
        {
            base.Awake();
            Prewarm();
        }

        public void ShowText(FlyingTextData data)
        {
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[FlyingText] ShowText skipped: controller not active in hierarchy");
                return;
            }

            if (data.Origin == null)
            {
                data.Origin = _origin;
            }

            _queue.Enqueue(data);

            EnsureProcessing();
        }

        public void ForceHideAll()
        {
            if (_processRoutine != null)
            {
                StopCoroutine(_processRoutine);
                _processRoutine = null;
            }

            _forceStop = true;
            _queue.Clear();

            foreach (GameplayFlyingTextView view in _pool)
            {
                if (view != null)
                {
                    view.Stop();
                }
            }
        }

        protected override void OnDestroy()
        {
            FlyingTextTriggered = null;
            base.OnDestroy();
        }

        private void OnEnable()
        {
            // Unity destroys running coroutines when the GameObject is deactivated.
            // On re-enable, restart processing if anything is still queued.
            _processRoutine = null;
            if (_queue.Count > 0)
            {
                EnsureProcessing();
            }
        }

        private void OnDisable()
        {
            // Routine was just killed by Unity; clear the ref so the next ShowText restarts it.
            if (_processRoutine != null)
            {
                Debug.LogWarning("[FlyingText] OnDisable: coroutine was killed by deactivation, clearing ref");
                _processRoutine = null;
            }
        }

        private void EnsureProcessing()
        {
            // Watchdog: if a coroutine is supposedly alive but hasn't ticked in a long time,
            // it died without telling us. Reset and restart.
            if (_processRoutine != null && _lastTickTime > 0f && Time.time - _lastTickTime > WatchdogTimeoutSeconds)
            {
                Debug.LogWarning($"[FlyingText] Watchdog: routine appeared alive but no tick for {Time.time - _lastTickTime:F1}s. Force-restarting");
                StopCoroutine(_processRoutine);
                _processRoutine = null;
            }

            if (_processRoutine == null)
            {
                _processRoutine = StartCoroutine(ProcessQueue());
            }
        }

        private void Prewarm()
        {
            if (_prefab == null || _parent == null)
            {
                return;
            }

            for (int i = 0; i < PrewarmCount; i++)
            {
                GameplayFlyingTextView view = Instantiate(_prefab, _parent);
                view.gameObject.SetActive(false);
                _pool.Add(view);
            }
        }

        private IEnumerator ProcessQueue()
        {
            _forceStop = false;
            _lastTickTime = Time.time;

            while (_queue.Count > 0)
            {
                _lastTickTime = Time.time;
                FlyingTextData data = _queue.Dequeue();

                if (data.Delay > 0f)
                {
                    yield return new WaitForSeconds(data.Delay);
                }

                if (_forceStop && _queue.Count == 0)
                {
                    break;
                }

                TryTriggerFlyingText(data);
                _lastTickTime = Time.time;

                float wait = _interval * QueueAcceleration(_queue.Count);
                yield return new WaitForSeconds(wait);
            }

            _processRoutine = null;
        }

        private void TryTriggerFlyingText(FlyingTextData data)
        {
            try
            {
                TriggerFlyingText(data);
            }
            catch (Exception e)
            {
                // Never let a single bad trigger kill the whole pipeline.
                Debug.LogError($"[FlyingText] TriggerFlyingText threw: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static float QueueAcceleration(int queueSize)
        {
            return queueSize switch
            {
                <= LowClutterThreshold => 1,
                >= HighClutterThreshold => HighClutterAcceleration,
                _ => 1 - (1 - HighClutterAcceleration) * (queueSize - LowClutterThreshold) / (HighClutterThreshold - LowClutterThreshold)
            };
        }

        private void TriggerFlyingText(FlyingTextData data)
        {
            GameplayFlyingTextView view = null;

            for (int i = 0; i < _pool.Count; i++)
            {
                GameplayFlyingTextView candidate = _pool[i];
                if (candidate != null && !candidate.IsRunning)
                {
                    view = candidate;
                    break;
                }
            }

            if (view == null)
            {
                if (_prefab == null || _parent == null)
                {
                    Debug.LogError("[FlyingText] Cannot instantiate: prefab or parent is null");
                    return;
                }
                view = Instantiate(_prefab, _parent);
                _pool.Add(view);
                Debug.Log($"[FlyingText] Pool grown to {_pool.Count}");
            }

            if (data.Origin == null)
            {
                Debug.LogWarning("[FlyingText] Origin null at trigger; skipping");
                view.Stop();
                FlyingTextTriggered?.Invoke(data);
                return;
            }

            view.SetupAndAnimate(data.Type, data.Value, data.Color, data.Origin.position);

            FlyingTextTriggered?.Invoke(data);
        }
    }
}
