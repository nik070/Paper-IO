using System.Collections.Generic;
using Clipper2Lib;
using Core;
using Mechanics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    [RequireComponent(typeof(CharacterMotor), typeof(Character))]
    public class AIController : MonoBehaviour, IController
    {
        // -----------------------------------------------------------------------------------------
        // Tuning constants
        // -----------------------------------------------------------------------------------------

        // AI tick rate. Faster than the old 0.2s so the bot can actually react before it ploughs
        // through its own trail at full speed.
        private const float AiTickRate = 0.1f;

        // Skip this many of the most recently dropped trail points when checking self-collision —
        // those points sit right under the bot and any forward direction would falsely "hit" them.
        private const int TrailHeadSkip = 4;

        // How many candidate directions to evaluate per tick (every 360/N degrees).
        private const int DirectionCandidates = 24;

        // Look-ahead duration: predict where we'll be N seconds in the future when scoring a
        // candidate direction. Scaled by motor speed inside the scorer.
        private const float TrailLookAheadSeconds = 0.6f;

        // Below this distance from any existing trail segment, a candidate direction starts losing
        // score linearly. Roughly the bot's visual radius + a small buffer.
        private const float TrailDangerDistance = 0.8f;

        // Magnitude of the trail-proximity penalty per unit-of-closeness. Strong enough to clearly
        // outrank "alignment with goal" but not so strong it overrides a hard wall-crossing veto.
        private const float TrailProximityWeight = 60f;

        // Score returned for a candidate direction whose look-ahead segment would actually CROSS
        // an existing trail segment. Effectively a hard veto — only chosen if every other
        // candidate is also vetoed (degenerate edge case).
        private const float TrailCrossPenalty = 10000f;

        // How close to the arena boundary the bot is allowed to plan a path before the candidate
        // starts losing score. Keeps bots from cornering themselves against the wall.
        private const float WallSafetyMargin = 1.5f;
        private const float WallProximityWeight = 80f;

        // Player-trail pursuit range, scaled by risk profile.
        private const float PursueRangeCowardly = 8f;
        private const float PursueRangeAggressive = 25f;

        // Score weights for the picker.
        private const float AlignmentWeight = 2.0f;
        private const float ContinuityWeight = 0.6f;
        private const float ReverseDotThreshold = -0.7f;

        private enum State
        {
            InsideBase,
            Exploring,
            Returning
        }

        // -----------------------------------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------------------------------

        private CharacterMotor _motor;
        private Character _character;

        private float _nextTickTime;
        private State _currentState = State.InsideBase;

        private Vector3 _exitPoint;
        private float _timeOutside;
        private Vector3 _currentMoveDir;

        // Dynamic thresholds mapped from _riskTaker
        private float _maxTimeOutside;
        private float _maxDistOutside;

        [FormerlySerializedAs("riskTaker")]
        [Tooltip("0 = Cowardly (stays close), 1 = Aggressive (takes huge areas)")]
        public float _riskTaker = 0.5f;

        // -----------------------------------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------------------------------

        private void Update()
        {
            if (_motor == null || !_motor.IsEnabled)
            {
                return;
            }

            if (_character._area == null || _character._area.CurrentTerritory == null)
            {
                return;
            }

            if (Time.time < _nextTickTime)
            {
                return;
            }

            _nextTickTime = Time.time + AiTickRate;
            Think();
        }

        public void Init(CharacterSpawnConfig config)
        {
            _riskTaker = Mathf.Clamp01(config.RiskTaker);
            _maxTimeOutside = Mathf.Lerp(1.5f, 7.0f, _riskTaker);
            _maxDistOutside = Mathf.Lerp(8.0f, 35.0f, _riskTaker);

            _motor = GetComponent<CharacterMotor>();
            _character = GetComponent<Character>();

            _currentMoveDir = RandomUnit2D();
            _motor.SetLastMovement(_currentMoveDir);
        }

        // -----------------------------------------------------------------------------------------
        // Core think loop
        // -----------------------------------------------------------------------------------------

        private void Think()
        {
            Vector3 selfPos = transform.position;
            bool isInside = _character._area.IsPointInside(selfPos);

            Vector3 goalDir;
            bool checkTrail;

            if (isInside)
            {
                if (_currentState != State.InsideBase)
                {
                    _currentState = State.InsideBase;
                    _timeOutside = 0f;
                }

                // Aim outward toward a fresh exit on a random tangent of our territory. No
                // self-trail check needed — we just cleared it on entry.
                goalDir = ChooseExploreOutwardDir(selfPos);
                checkTrail = false;
            }
            else
            {
                if (_currentState == State.InsideBase)
                {
                    _currentState = State.Exploring;
                    _exitPoint = selfPos;
                    _timeOutside = 0f;
                }

                _timeOutside += AiTickRate;
                float distFromExit = Vector3.Distance(selfPos, _exitPoint);

                bool mustReturn = _timeOutside > _maxTimeOutside
                                  || distFromExit > _maxDistOutside
                                  || _currentState == State.Returning;

                if (mustReturn)
                {
                    _currentState = State.Returning;
                    Vector3 returnTarget = GetBestReturnPoint(selfPos);
                    goalDir = (returnTarget - selfPos).normalized;
                }
                else if (TryGetPlayerTrailTarget(selfPos, out Vector3 pursueTarget))
                {
                    goalDir = (pursueTarget - selfPos).normalized;
                }
                else
                {
                    // Keep heading; let the scorer punish anything that would hit the trail or
                    // wall. Occasional intentional perpendicular turns help bots draw square /
                    // U-shaped pockets like real players do, instead of arcs.
                    goalDir = _currentMoveDir;
                    if (Random.value < 0.12f)
                    {
                        float sign = Random.value > 0.5f ? 1f : -1f;
                        goalDir = Quaternion.Euler(0, 0, sign * 90f) * _currentMoveDir;
                    }
                }

                checkTrail = true;
            }

            if (goalDir.sqrMagnitude < 0.0001f)
            {
                goalDir = _currentMoveDir;
            }

            _currentMoveDir = PickBestDirection(selfPos, goalDir.normalized, checkTrail);
            _motor.SetLastMovement(_currentMoveDir);
        }

        // -----------------------------------------------------------------------------------------
        // Direction selection — score every candidate, pick the best
        // -----------------------------------------------------------------------------------------

        private Vector3 PickBestDirection(Vector3 selfPos, Vector3 desired, bool checkTrail)
        {
            float bestScore = float.MinValue;
            Vector3 bestDir = desired;

            for (int i = 0; i < DirectionCandidates; i++)
            {
                float angleDeg = i * (360f / DirectionCandidates);
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 candidate = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

                // Reject a near-reverse direction outright — bots that flip 180° per tick look
                // broken on screen and stall their own progress.
                if (Vector3.Dot(candidate, _currentMoveDir) < ReverseDotThreshold)
                {
                    continue;
                }

                float alignment = Vector3.Dot(candidate, desired);            // -1 .. 1
                float continuity = Vector3.Dot(candidate, _currentMoveDir);    // -1 .. 1
                float score = alignment * AlignmentWeight + continuity * ContinuityWeight;

                if (checkTrail)
                {
                    score -= TrailDangerScore(selfPos, candidate);
                }
                score -= WallDangerScore(selfPos, candidate);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = candidate;
                }
            }

            return bestDir.normalized;
        }

        // Returns 0 if the candidate is safe, TrailCrossPenalty if it would cross an existing
        // trail segment in the next look-ahead window, or a linear proximity penalty otherwise.
        private float TrailDangerScore(Vector3 selfPos, Vector3 dir)
        {
            List<Vector3> points = _character._trail._logicPoints;
            if (points == null || points.Count <= TrailHeadSkip + 1)
            {
                return 0f;
            }

            float lookAhead = Mathf.Max(1.5f, _motor.Speed * TrailLookAheadSeconds);
            Vector2 a = new Vector2(selfPos.x, selfPos.y);
            Vector2 b = a + new Vector2(dir.x, dir.y) * lookAhead;

            // Also sample a midpoint so we catch the case where the trail is parallel to our
            // path and never crosses it but sits dangerously close along the whole stretch.
            Vector2 mid = (a + b) * 0.5f;

            float maxProximity = 0f;
            int endIndex = points.Count - TrailHeadSkip - 1;
            for (int i = 0; i < endIndex; i++)
            {
                Vector2 p = new Vector2(points[i].x, points[i].y);
                Vector2 q = new Vector2(points[i + 1].x, points[i + 1].y);

                if (SegmentsIntersect(a, b, p, q))
                {
                    return TrailCrossPenalty;
                }

                float d1 = PointToSegmentDistance(b, p, q);
                float d2 = PointToSegmentDistance(mid, p, q);
                float closest = d1 < d2 ? d1 : d2;
                if (closest < TrailDangerDistance)
                {
                    float prox = (TrailDangerDistance - closest);
                    if (prox > maxProximity)
                    {
                        maxProximity = prox;
                    }
                }
            }

            return maxProximity * TrailProximityWeight;
        }

        private float WallDangerScore(Vector3 selfPos, Vector3 dir)
        {
            ArenaController arena = ArenaController.Instance;
            if (arena == null)
            {
                return 0f;
            }

            float lookAhead = Mathf.Max(1.5f, _motor.Speed * TrailLookAheadSeconds) + WallSafetyMargin;
            Vector2 future = new Vector2(selfPos.x + dir.x * lookAhead, selfPos.y + dir.y * lookAhead);
            float distFromCenter = future.magnitude;
            float safeRadius = arena.Radius - WallSafetyMargin;
            if (distFromCenter > safeRadius)
            {
                return (distFromCenter - safeRadius) * WallProximityWeight;
            }
            return 0f;
        }

        // -----------------------------------------------------------------------------------------
        // Goal-direction helpers
        // -----------------------------------------------------------------------------------------

        // Pick a direction that aims outward from our territory center but with a tangential
        // bias so successive expeditions don't keep re-tracing the same line. Pure outward radial
        // would make the bot leave from the same point every time.
        private Vector3 ChooseExploreOutwardDir(Vector3 selfPos)
        {
            Vector3 center = GetTerritoryCenter();
            Vector3 radial = selfPos - center;
            if (radial.sqrMagnitude < 0.0001f)
            {
                return RandomUnit2D();
            }
            radial.Normalize();
            // Add a tangent component with a per-tick random sign / magnitude.
            Vector3 tangent = new Vector3(-radial.y, radial.x, 0f);
            float tangentBias = Random.Range(-0.6f, 0.6f);
            return (radial + tangent * tangentBias).normalized;
        }

        // Find the closest point on the outer border of our own territory. Returning there gives
        // the shortest path back that doesn't have to slice through the captured area — and
        // critically, the candidate scorer will reject any direction that would cross the trail
        // along the way, so the bot naturally loops around the explored area instead of cutting it.
        private Vector3 GetBestReturnPoint(Vector3 selfPos)
        {
            Paths64 territory = _character._area.CurrentTerritory;
            if (territory == null || territory.Count == 0)
            {
                return GetTerritoryCenter();
            }

            Vector3 best = selfPos;
            float bestSqr = float.MaxValue;
            const double invScale = 1.0 / GeometryUtils.Scale;

            for (int pi = 0; pi < territory.Count; pi++)
            {
                Path64 path = territory[pi];
                for (int i = 0; i < path.Count; i++)
                {
                    Point64 pt = path[i];
                    float wx = (float)(pt.X * invScale);
                    float wy = (float)(pt.Y * invScale);
                    float dx = wx - selfPos.x;
                    float dy = wy - selfPos.y;
                    float sqr = dx * dx + dy * dy;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = new Vector3(wx, wy, 0f);
                    }
                }
            }

            return best;
        }

        private bool TryGetPlayerTrailTarget(Vector3 selfPos, out Vector3 target)
        {
            target = Vector3.zero;
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

            float chaseRange = Mathf.Lerp(PursueRangeCowardly, PursueRangeAggressive, _riskTaker);
            if (minSqr > chaseRange * chaseRange)
            {
                return false;
            }

            target = closest;
            return true;
        }

        private Vector3 GetTerritoryCenter()
        {
            Rect64 b = _character._area.TerritoryBounds;
            double cx = (b.left + b.right) / 2.0;
            double cy = (b.top + b.bottom) / 2.0;
            return new Vector3(
                (float)(cx / GeometryUtils.Scale),
                (float)(cy / GeometryUtils.Scale),
                0f);
        }

        // -----------------------------------------------------------------------------------------
        // Geometry primitives
        // -----------------------------------------------------------------------------------------

        private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float d1 = Cross2D(p4 - p3, p1 - p3);
            float d2 = Cross2D(p4 - p3, p2 - p3);
            float d3 = Cross2D(p2 - p1, p3 - p1);
            float d4 = Cross2D(p2 - p1, p4 - p1);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
                && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static float PointToSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSqr = ab.x * ab.x + ab.y * ab.y;
            if (lenSqr < 0.000001f)
            {
                return (p - a).magnitude;
            }
            float t = Vector2.Dot(p - a, ab) / lenSqr;
            t = Mathf.Clamp01(t);
            Vector2 proj = a + ab * t;
            return (p - proj).magnitude;
        }

        private static Vector3 RandomUnit2D()
        {
            float angle = Random.value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }
    }
}
