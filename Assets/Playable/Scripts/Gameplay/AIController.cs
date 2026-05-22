using System.Collections.Generic;
using Core;
using Mechanics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    [RequireComponent(typeof(CharacterMotor), typeof(Character))]
    public class AIController : MonoBehaviour, IController
    {
        private const float TrailLookahead = 1.5f;
        private const int TrailHeadSkip = 4;
        private const float PursueRangeCowardly = 8f;
        private const float PursueRangeAggressive = 25f;

        private enum State
        {
            InsideBase,
            Exploring,
            Returning
        }

        private CharacterMotor _motor;
        private Character _character;

        // OPTIMIZATION: AI doesn't need to think 60 times a second.
        private readonly float _aiTickRate = 0.2f;
        private float _nextTickTime;
        private State _currentState = State.InsideBase;

        private Vector3 _exitPoint;
        private float _timeOutside;
        private Vector3 _currentMoveDir;

        // Dynamic thresholds mapped from riskTaker
        private float _maxTimeOutside;
        private float _maxDistOutside;

        [FormerlySerializedAs("riskTaker")] [Tooltip("0 = Cowardly (stays close), 1 = Aggressive (takes huge areas)")]
        public float _riskTaker = 0.5f;

        private void Update()
        {
            if (_motor != null && !_motor.IsEnabled)
            {
                return;
            }

            // Safety check: wait until the Game Manager initializes the territory
            if (_character._area == null || _character._area.CurrentTerritory == null)
            {
                return;
            }

            // Throttle AI calculations
            if (Time.time < _nextTickTime)
            {
                return;
            }

            _nextTickTime = Time.time + _aiTickRate;

            Think();
        }

        public void Init(CharacterSpawnConfig config)
        {
            _riskTaker = Mathf.Clamp01(config.RiskTaker);

            // Map the risk (0 to 1) to actual game values
            // High risk = can stay out for 7 seconds, up to 18 units away.
            _maxTimeOutside = Mathf.Lerp(1.5f, 7.0f, _riskTaker);
            _maxDistOutside = Mathf.Lerp(8.0f, 35.0f, _riskTaker);

            _motor = GetComponent<CharacterMotor>();
            _character = GetComponent<Character>();

            _currentMoveDir = GetRandomDirection();
            _motor.SetLastMovement(_currentMoveDir);
        }

        private void Think()
        {
            bool isInside = _character._area.IsPointInside(transform.position);

            if (isInside)
            {
                if (_currentState != State.InsideBase)
                {
                    // Just successfully returned and captured territory!
                    _currentState = State.InsideBase;
                    _timeOutside = 0f;
                    _currentMoveDir = GetRandomDirection(); // Head out in a new direction
                }
                else
                {
                    // Wandering safely inside base.
                    // 10% chance per tick to adjust direction so they don't get stuck in a corner.
                    if (Random.value < 0.1f)
                    {
                        _currentMoveDir = GetRandomDirection();
                    }
                }
            }
            else // OUTSIDE TERRITORY
            {
                if (_currentState == State.InsideBase)
                {
                    // Just stepped outside
                    _currentState = State.Exploring;
                    _exitPoint = transform.position;

                    // Human simulation: Players usually run parallel to their border right after exiting
                    Turn90Degrees();
                }

                _timeOutside += _aiTickRate;
                float distFromExit = Vector3.Distance(transform.position, _exitPoint);

                if (_currentState == State.Exploring)
                {
                    // Panic Condition: We've been out too long, or went too far
                    if (_timeOutside > _maxTimeOutside || distFromExit > _maxDistOutside)
                    {
                        _currentState = State.Returning;
                    }
                    else if (!TryPursuePlayerTrail())
                    {
                        // 15% chance per tick to make a 90-degree turn.
                        // This causes them to draw "Boxes" and "U-shapes" like real players.
                        if (Random.value < 0.15f)
                        {
                            Turn90Degrees();
                        }
                    }
                }

                if (_currentState == State.Returning)
                {
                    // Head straight for the mathematical center of their territory.
                    Vector3 safeTarget = GetTerritoryCenter();
                    _currentMoveDir = (safeTarget - transform.position).normalized;
                }
            }

            _motor.SetLastMovement(_currentMoveDir);
        }

        private void Turn90Degrees()
        {
            // Tiny bit of random noise so it isn't perfectly robotic
            float noise = Random.Range(-10f, 10f);
            Vector3 candidateLeft = Quaternion.Euler(0, 0, 90f + noise) * _currentMoveDir;
            Vector3 candidateRight = Quaternion.Euler(0, 0, -90f + noise) * _currentMoveDir;

            // Pick the rotation that keeps us furthest from our own recent trail, so the AI
            // doesn't visually walk back along the line it just drew.
            float distLeft = MinSqrDistanceFromTrailAlong(candidateLeft);
            float distRight = MinSqrDistanceFromTrailAlong(candidateRight);

            if (Mathf.Approximately(distLeft, distRight))
            {
                _currentMoveDir = Random.value > 0.5f ? candidateLeft : candidateRight;
            }
            else
            {
                _currentMoveDir = distLeft >= distRight ? candidateLeft : candidateRight;
            }
        }

        private float MinSqrDistanceFromTrailAlong(Vector3 dir)
        {
            List<Vector3> points = _character._trail._logicPoints;
            if (points == null || points.Count <= TrailHeadSkip)
            {
                return float.MaxValue;
            }

            Vector3 probe = transform.position + dir.normalized * TrailLookahead;
            float minSqr = float.MaxValue;

            // Skip the most recent points — they sit right under the AI, so every candidate
            // direction would otherwise score "close" against them.
            int endIndex = points.Count - TrailHeadSkip;
            for (int i = 0; i < endIndex; i++)
            {
                float sqr = (probe - points[i]).sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                }
            }

            return minSqr;
        }

        private Vector3 GetRandomDirection()
        {
            return new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
        }

        private bool TryPursuePlayerTrail()
        {
            // Only the AI pursues — and only when a live player exists with an active trail outside
            // their own zone. If the player is safe inside their territory, there's no trail to cut.
            if (CollisionManager.Instance == null
                || !CollisionManager.Instance.TryGetPlayer(out Character player)
                || player == null
                || player == _character
                || player._trail == null)
            {
                return false;
            }

            List<Vector3> playerTrail = player._trail._logicPoints;
            if (playerTrail == null || playerTrail.Count < 2)
            {
                return false;
            }

            Vector3 selfPos = transform.position;
            float minSqr = float.MaxValue;
            Vector3 closest = Vector3.zero;
            for (int i = 0; i < playerTrail.Count; i++)
            {
                float sqr = (playerTrail[i] - selfPos).sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    closest = playerTrail[i];
                }
            }

            // Range scales with the bot's risk profile so cowardly bots ignore distant prey while
            // aggressive bots will commit to a chase from across the arena.
            float chaseRange = Mathf.Lerp(PursueRangeCowardly, PursueRangeAggressive, _riskTaker);
            if (minSqr > chaseRange * chaseRange)
            {
                return false;
            }

            Vector3 toTrail = closest - selfPos;
            if (toTrail.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            _currentMoveDir = toTrail.normalized;
            return true;
        }

        private Vector3 GetTerritoryCenter()
        {
            // O(1) way to find the center using Clipper2's pre-calculated bounds.
            double centerX = (_character._area.TerritoryBounds.left + _character._area.TerritoryBounds.right) / 2.0;
            double centerY = (_character._area.TerritoryBounds.top + _character._area.TerritoryBounds.bottom) / 2.0;

            return new Vector3((float)(centerX / GeometryUtils.Scale), (float)(centerY / GeometryUtils.Scale), 0);
        }
    }
}
