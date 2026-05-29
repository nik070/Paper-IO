using System;
using System.Collections;
using System.Collections.Generic;
using Clipper2Lib;
using Gameplay;
using Mechanics;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Plain C# class owning collision detection, character registry, and territory capture.
    /// Created and owned by GameManager — no MonoBehaviour lifecycle.
    /// </summary>
    public class CollisionManager : Singleton<CollisionManager>
    {
        private readonly List<Character> _allCharacters = new List<Character>();
        private SpatialHashGrid _grid;
        private readonly List<TrailHitInfo> _currentTrailHits = new List<TrailHitInfo>();
        private readonly List<TrailHitInfo> _trailHitsThisFrame = new List<TrailHitInfo>();
        private readonly HashSet<DeathInfo> _deathsThisFrame = new HashSet<DeathInfo>();
        private readonly List<Vector3> _takeoverTrail = new List<Vector3>(256);
        // Scaled (Clipper-units, i.e. world_area * Scale²) area of the arena polygon.
        // Computed from the same polygonal approximation used to draw the ground mesh so a
        // player whose territory equals the arena polygon reaches exactly 1.0 fill.
        // Using π·r² here instead would leave a small deficit (polygon < circle) and cap
        // the HUD progress bar below 100% even at visually-full fill.
        private double _scaledArenaArea;

        // Zone occupancy tracking — one current zone owner per character. Mirrors Paper2's
        // PlayerZoneInfo.InsideZone. Enter/exit events fire exclusively when this reference
        // changes. See zone_enter_exit_events plan for semantics.
        private readonly Dictionary<Character, Character> _zoneOccupancy = new Dictionary<Character, Character>();
        // Separate pre-allocated buffers so a nested DeregisterCharacter invoked from within an
        // UpdateZoneOccupancy fanout (or vice versa) cannot clobber the outer invocation's list.
        private readonly List<ZoneTransition> _sweepTransitions = new List<ZoneTransition>();
        private readonly List<ZoneTransition> _deregTransitions = new List<ZoneTransition>();
        private bool _isSubscribedToStateChanges;

        private readonly struct ZoneTransition
        {
            public readonly Character Mover;
            public readonly Character PrevOwner;
            public readonly Character NewOwner;

            public ZoneTransition(Character mover, Character prev, Character next)
            {
                Mover = mover;
                PrevOwner = prev;
                NewOwner = next;
            }
        }

        public void Init(float arenaRadius, float gridCellSize)
        {
            float diameter = arenaRadius * 2f;
            _grid = new SpatialHashGrid(diameter, diameter, gridCellSize);
            // ArenaController.Init() runs before this in GameManager.Start(), so the arena
            // polygon is already generated and CreateArenaPath() is safe to call here.
            _scaledArenaArea = Math.Abs(Clipper.Area(ArenaController.Instance.CreateArenaPath()));

            // Ordering contract: GameManager.Start() calls Init() BEFORE LevelManager.Init() spawns
            // characters and BEFORE SetState(Tutorial) fires the first OnGameStateChanged event, so
            // the subscription is live in time to seed initial occupancy once all characters exist.
            if (!_isSubscribedToStateChanges)
            {
                GameEvents.OnGameStateChanged += HandleGameStateChanged;
                _isSubscribedToStateChanges = true;
            }

            WarmUpClipper();
        }

        public override void Dispose()
        {
            // Run BEFORE base.Dispose() so the static GameEvents delegate no longer holds a
            // reference to this collision manager across play-mode enter/exit cycles.
            if (_isSubscribedToStateChanges)
            {
                GameEvents.OnGameStateChanged -= HandleGameStateChanged;
                _isSubscribedToStateChanges = false;
            }
            _zoneOccupancy.Clear();
            _sweepTransitions.Clear();
            _deregTransitions.Clear();
            base.Dispose();
        }

        public void RegisterCharacter(Character character)
        {
            if (!_allCharacters.Contains(character))
            {
                _allCharacters.Add(character);
            }
        }

        /// <summary>
        /// Removes <paramref name="character"/> from tracking and fires exit events for every
        /// character currently recorded inside this character's zone (plus this character's own
        /// occupancy, if any). Caller contract: must be invoked while <paramref name="character"/>
        /// and its <c>_area</c> are still valid Unity objects so subscribers can safely read them
        /// for VFX/audio lookup. See <c>Character.Die()</c> for the load-bearing ordering.
        /// </summary>
        public void DeregisterCharacter(Character character)
        {
            if (character == null)
            {
                return;
            }

            // Enumeration phase: collect exits into a dedicated buffer so nested calls (e.g. a
            // subscriber that triggers another Die) don't corrupt an outer sweep's buffer.
            _deregTransitions.Clear();
            foreach (KeyValuePair<Character, Character> kvp in _zoneOccupancy)
            {
                // Every occupant of the dying zone owner gets an exit. This includes the dying
                // character itself if they were standing in their own zone (kvp.Key==kvp.Value==character).
                if (kvp.Value == character)
                {
                    _deregTransitions.Add(new ZoneTransition(kvp.Key, character, null));
                }
                // Dying character was inside someone else's zone — balance the pending enter with
                // an exit so subscribers see matched pairs.
                else if (kvp.Key == character)
                {
                    _deregTransitions.Add(new ZoneTransition(character, kvp.Value, null));
                }
            }

            // Commit phase: remove dictionary entries for every occupant of the dying zone, and
            // also remove this character itself from _allCharacters before firing so a re-entrant
            // UpdateZoneOccupancy can never re-assign anyone back to this doomed owner.
            for (int i = 0; i < _deregTransitions.Count; i++)
            {
                _zoneOccupancy.Remove(_deregTransitions[i].Mover);
            }
            _allCharacters.Remove(character);

            // Fanout phase: fire exits only. Any occupant who needs a new owner will get their
            // enter on the next UpdateZoneOccupancy sweep.
            for (int i = 0; i < _deregTransitions.Count; i++)
            {
                ZoneTransition t = _deregTransitions[i];
                GameEvents.FireCharacterExitedZone(t.Mover, t.PrevOwner);
            }
            _deregTransitions.Clear();
        }

        public int EnemyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _allCharacters.Count; i++)
                {
                    if (!_allCharacters[i].IsPlayer)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool TryGetPlayer(out Character player)
        {
            for (int i = 0; i < _allCharacters.Count; i++)
            {
                if (_allCharacters[i].IsPlayer)
                {
                    player = _allCharacters[i];
                    return true;
                }
            }

            player = null;
            return false;
        }

        public bool TryGetZoneOwnerAtPoint(Vector3 point, Character ignoreCharacter, out Character owner)
        {
            owner = ScanForZoneOwner(point, ignoreCharacter, null);
            return owner != null;
        }

        /// <summary>
        /// Finds a spawn position whose <paramref name="clearance"/>-radius starting circle does
        /// not overlap any existing territory or trail. Walks outward from <paramref name="desired"/>
        /// in angular rings, returning the first free candidate. Falls back to <paramref name="desired"/>
        /// (with <c>false</c>) if no safe spot is found inside the arena.
        /// </summary>
        /// <summary>
        /// Returns <paramref name="territory"/> with every other registered character's territory
        /// subtracted from it via Clipper.Difference. Call this immediately after spawning a new
        /// character so their starting zone never visually overlaps an existing zone — overlapping
        /// territories share the same Z=0 depth and cause stencil write conflicts that make the
        /// new character's territory look blended or invisible.
        /// </summary>
        public Paths64 ClipAgainstExistingTerritories(Character self, Paths64 territory)
        {
            Paths64 result = territory;
            for (int i = 0; i < _allCharacters.Count; i++)
            {
                Character other = _allCharacters[i];
                if (other == self || other._area == null ||
                    other._area.CurrentTerritory == null ||
                    other._area.CurrentTerritory.Count == 0)
                    continue;

                result = Clipper.Difference(result, other._area.CurrentTerritory, FillRule.NonZero);

                if (result == null || result.Count == 0)
                    return new Paths64(); // fully consumed — return empty rather than null
            }
            return result;
        }

        public bool TryFindSafeSpawn(Vector3 desired, float clearance, out Vector3 safePosition)
        {
            if (IsSpawnSafe(desired, clearance))
            {
                safePosition = desired;
                return true;
            }

            float arenaRadius = ArenaController.Instance.Radius;
            float maxOffsetFromCenter = Mathf.Max(0f, arenaRadius - clearance - 0.5f);
            float maxOffsetSqr = maxOffsetFromCenter * maxOffsetFromCenter;
            const int angularSamples = 16;
            float angleStep = Mathf.PI * 2f / angularSamples;
            float radiusStep = Mathf.Max(0.5f, clearance * 0.75f);
            float maxSearchRadius = arenaRadius * 2f;

            for (float r = radiusStep; r <= maxSearchRadius; r += radiusStep)
            {
                for (int a = 0; a < angularSamples; a++)
                {
                    float angle = a * angleStep;
                    Vector3 candidate = new Vector3(
                        desired.x + Mathf.Cos(angle) * r,
                        desired.y + Mathf.Sin(angle) * r,
                        desired.z);

                    if (candidate.x * candidate.x + candidate.y * candidate.y > maxOffsetSqr)
                    {
                        continue;
                    }

                    if (IsSpawnSafe(candidate, clearance))
                    {
                        safePosition = candidate;
                        return true;
                    }
                }
            }

            safePosition = desired;
            return false;
        }

        private bool IsSpawnSafe(Vector3 position, float clearance)
        {
            const int perimeterSamples = 8;
            float angleStep = Mathf.PI * 2f / perimeterSamples;
            float clearanceSqr = clearance * clearance;

            for (int i = 0; i < _allCharacters.Count; i++)
            {
                Character c = _allCharacters[i];
                if (c == null || c._area == null)
                {
                    continue;
                }

                // Reject if the candidate centre or any perimeter point lands inside an existing zone.
                if (c._area.IsPointInside(position))
                {
                    return false;
                }
                for (int p = 0; p < perimeterSamples; p++)
                {
                    float angle = p * angleStep;
                    Vector3 perimeter = new Vector3(
                        position.x + Mathf.Cos(angle) * clearance,
                        position.y + Mathf.Sin(angle) * clearance,
                        position.z);
                    if (c._area.IsPointInside(perimeter))
                    {
                        return false;
                    }
                }

                Vector2 candidate2 = new Vector2(position.x, position.y);

                // Reject if the candidate circle clips a recorded trail segment or the live head segment.
                List<Vector3> trail = c._trail._logicPoints;
                for (int j = 0; j < trail.Count - 1; j++)
                {
                    if (SqrDistanceToSegment(candidate2, trail[j], trail[j + 1]) < clearanceSqr)
                    {
                        return false;
                    }
                }
                if (trail.Count > 0)
                {
                    if (SqrDistanceToSegment(candidate2, trail[trail.Count - 1], c.transform.position) < clearanceSqr)
                    {
                        return false;
                    }
                }

                // Reject if another character is too close to the spawn centre.
                Vector2 charPos = new Vector2(c.transform.position.x, c.transform.position.y);
                if ((candidate2 - charPos).sqrMagnitude < clearanceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private static float SqrDistanceToSegment(Vector2 p, Vector3 a3, Vector3 b3)
        {
            Vector2 a = new Vector2(a3.x, a3.y);
            Vector2 b = new Vector2(b3.x, b3.y);
            Vector2 ab = b - a;
            float abSqr = ab.x * ab.x + ab.y * ab.y;
            if (abSqr < 1e-6f)
            {
                return (p - a).sqrMagnitude;
            }
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abSqr);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude;
        }

        public float GetPlayerFillPercent(Character player)
        {
            if (player == null || player._area == null || player._area.CurrentTerritory == null || _scaledArenaArea <= 0)
            {
                return 0f;
            }

            double area = Math.Abs(Clipper.Area(player._area.CurrentTerritory));
            // Clamp to [0, 1]: the player's union polygon can slightly overshoot the arena
            // polygon's bounds (capture shapes are clipped per-step, but Clipper union of
            // many polygons can drift a hair past the arena boundary on the scaled grid),
            // which would push the HUD progress bar past 100%.
            return Mathf.Clamp01((float)(area / _scaledArenaArea));
        }

        public void FreezeAllEnemies()
        {
            for (int i = 0; i < _allCharacters.Count; i++)
            {
                if (!_allCharacters[i].IsPlayer)
                {
                    _allCharacters[i].Motor.SetEnabled(false);
                }
            }
        }

        public void FreezeAllPlayers()
        {
            for (int i = 0; i < _allCharacters.Count; i++)
            {
                _allCharacters[i].Motor.SetEnabled(false);
            }
        }

        public void UnfreezeAllEnemies()
        {
            for (int i = 0; i < _allCharacters.Count; i++)
            {
                if (!_allCharacters[i].IsPlayer)
                {
                    _allCharacters[i].Motor.SetEnabled(true);
                }
            }
        }

        public bool IsAnyBotAlive()
        {
            for (int i = 0; i < _allCharacters.Count; i++)
            {
                if (!_allCharacters[i].IsPlayer)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Called by GameManager.LateUpdate() when state == Playing.
        /// Populates the spatial grid, checks intersections, resolves deaths.
        /// </summary>
        public void Tick()
        {
            if (_allCharacters.Count == 0)
            {
                return;
            }

            _grid.Clear();
            _deathsThisFrame.Clear();
            _trailHitsThisFrame.Clear();

            // 1. Populate the Grid with all active trail segments
            for (int ci = 0; ci < _allCharacters.Count; ci++)
            {
                Character c = _allCharacters[ci];
                List<Vector3> logicPoints = c._trail._logicPoints;
                if (logicPoints.Count >= 2)
                {
                    for (int i = 0; i < logicPoints.Count - 1; i++)
                    {
                        var segment = new TrailSegment
                        {
                            Owner = c,
                            SegmentIndex = i,
                            Start = new Vector2(logicPoints[i].x, logicPoints[i].y),
                            End = new Vector2(logicPoints[i + 1].x, logicPoints[i + 1].y)
                        };
                        _grid.InsertSegment(segment);
                    }
                }

                if (logicPoints.Count > 0)
                {
                    int lastIdx = logicPoints.Count - 1;
                    var tipSegment = new TrailSegment
                    {
                        Owner = c,
                        SegmentIndex = lastIdx,
                        Start = new Vector2(logicPoints[lastIdx].x, logicPoints[lastIdx].y),
                        End = new Vector2(c.transform.position.x, c.transform.position.y)
                    };
                    _grid.InsertSegment(tipSegment);
                }
            }

            // 2. Continuous Collision Detection against the Grid
            for (int ci = 0; ci < _allCharacters.Count; ci++)
            {
                Character c = _allCharacters[ci];
                if (c.Config == null)
                {
                    continue;
                }

                var moveStart = new Vector2(c.PreviousPosition.x, c.PreviousPosition.y);
                var moveEnd = new Vector2(c.transform.position.x, c.transform.position.y);

                if (Vector2.SqrMagnitude(moveEnd - moveStart) < 0.0001f)
                {
                    c.UpdatePreviousPosition();
                    continue;
                }

                int headIndex = c._trail._logicPoints.Count - 1;
                _grid.CheckIntersection(moveStart, moveEnd, c, headIndex, _currentTrailHits);

                if (_currentTrailHits.Count > 0)
                {
                    for (int i = 0; i < _currentTrailHits.Count; i++)
                    {
                        TrailHitInfo hit = _currentTrailHits[i];
                        _deathsThisFrame.Add(new DeathInfo(hit.Killer, hit.Victim));

                        if (!ContainsTrailHitVictim(_trailHitsThisFrame, hit.Victim))
                        {
                            _trailHitsThisFrame.Add(hit);
                        }
                    }
                }

                c.UpdatePreviousPosition();
            }

            // 3. Resolve Deaths — fire event BEFORE Die() so victim transform is still valid
            if (_deathsThisFrame.Count > 0)
            {
                for (int i = 0; i < _trailHitsThisFrame.Count; i++)
                {
                    TrailHitInfo trailHit = _trailHitsThisFrame[i];
                    if (_allCharacters.Contains(trailHit.Victim))
                    {
                        Vector3 hitPosition = new Vector3(trailHit.HitPoint.x, trailHit.HitPoint.y, 0f);
                        GameEvents.FireTrailCut(trailHit.Killer, trailHit.Victim, hitPosition);
                    }
                }

                foreach (DeathInfo deathInfo in _deathsThisFrame)
                {
                    if (_allCharacters.Contains(deathInfo.Victim))
                    {
                        if (TryGetTrailHit(deathInfo.Killer, deathInfo.Victim, out TrailHitInfo trailHit))
                        {
                            Vector3 hitPosition = new Vector3(trailHit.HitPoint.x, trailHit.HitPoint.y, 0f);
                            if (TryApplyPlayerTerritoryTakeover(deathInfo.Killer, deathInfo.Victim, hitPosition, trailHit.SegmentIndex, true, out float stolenArea))
                            {
                                float fillPct = GetPlayerFillPercent(deathInfo.Killer);
                                GameEvents.FireTerritoryCaptured(deathInfo.Killer, fillPct, stolenArea);
                            }
                        }

                        GameEvents.FireCharacterDied(deathInfo);
                        deathInfo.Victim.Die();
                    }
                }
            }

            // Re-evaluate zone occupancy over all surviving characters. Analogous to Paper2's
            // ZonePostCollisionSystem — runs after deaths have deregistered so the sweep sees a
            // clean _allCharacters.
            UpdateZoneOccupancy();
        }

        public void ProcessTerritoryCapture(Character capturer, Paths64 captureShape)
        {
            Rect64 captureBounds = Clipper.GetBounds(captureShape);
            float capturedArea = (float)(Math.Abs(Clipper.Area(captureShape)) / (GeometryUtils.Scale * GeometryUtils.Scale));

            // Give the area to the capturer
            Paths64 newCapturerTerritory = Clipper.Union(capturer._area.CurrentTerritory, captureShape, FillRule.NonZero);
            newCapturerTerritory = Clipper.RamerDouglasPeucker(newCapturerTerritory, 0.05 * GeometryUtils.Scale);
            capturer._area.SetTerritory(newCapturerTerritory);
            capturer._area.ShowCreatedTerritory(captureShape);

            // Process steals and area-capture kills
            for (int i = _allCharacters.Count - 1; i >= 0; i--)
            {
                Character victim = _allCharacters[i];
                if (victim == capturer)
                {
                    continue;
                }

                if (!victim._area.TerritoryBounds.Intersects(captureBounds))
                {
                    continue;
                }

                // Area Capture Kill (Point-in-Polygon Check)
                // TODO: add killing when trail is in captured zone
                var victimPt = new Point64(
                    victim.transform.position.x * GeometryUtils.Scale,
                    victim.transform.position.y * GeometryUtils.Scale);
                bool isInsideCapture = false;
                foreach (Path64 path in captureShape)
                {
                    if (Clipper.PointInPolygon(victimPt, path) != PointInPolygonResult.IsOutside)
                    {
                        isInsideCapture = true;
                        break;
                    }
                }

                if (isInsideCapture && victim.Config.CanBeKilledByAreaCapture)
                {
                    if (TryApplyPlayerTerritoryTakeover(capturer, victim, victim.transform.position, -1, false, out float stolenArea))
                    {
                        capturedArea += stolenArea;
                    }

                    GameEvents.FireCharacterDied(new DeathInfo(capturer, victim));
                    victim.Die();
                    continue;
                }

                // Normal Territory Steal
                Paths64 newVictimTerritory = Clipper.Difference(victim._area.CurrentTerritory, captureShape, FillRule.NonZero);

                if (newVictimTerritory.Count == 0)
                {
                    if (TryApplyPlayerTerritoryTakeover(capturer, victim, victim.transform.position, -1, false, out float stolenArea))
                    {
                        capturedArea += stolenArea;
                    }

                    GameEvents.FireCharacterDied(new DeathInfo(capturer, victim));
                    victim.Die();
                }
                else
                {
                    newVictimTerritory = Clipper.RamerDouglasPeucker(newVictimTerritory, 0.05 * GeometryUtils.Scale);
                    victim._area.SetTerritory(newVictimTerritory);
                }
            }

            // Fire territory captured event with current player fill %
            if (TryGetPlayer(out Character player))
            {
                float fillPct = GetPlayerFillPercent(player);
                GameEvents.FireTerritoryCaptured(capturer, fillPct, capturedArea);
            }

            // Analogous to Paper2's ISignalOnZoneDecreased. Territory bounds are already updated,
            // so this sweep catches both the capturer (zone grew over a stationary enemy) and the
            // victims (zone shrank beneath them).
            UpdateZoneOccupancy();
        }

        /// <summary>
        /// Non-blocking territory capture.
        ///
        /// The expensive Clipper.Union (territory + capture shape) runs on the thread-pool
        /// via Task.Run. The main thread polls <c>task.IsCompleted</c> every frame through
        /// WaitUntil — zero blocking, full frame-rate during computation.
        /// Once the thread finishes, all Unity API calls (SetTerritory, Die, events) execute
        /// back on the main thread as normal.
        ///
        /// Called with <c>yield return CollisionManager.Instance.ProcessTerritoryCaptureAsync(...)</c>
        /// from Character.RunCaptureCoroutine.
        /// </summary>
        public IEnumerator ProcessTerritoryCaptureAsync(Character capturer, Paths64 captureShape)
        {
            // Snapshot everything the background thread needs BEFORE yielding.
            // CurrentTerritory is immutable during the computation because
            // _isCaptureInFlight blocks HandleAreaState from starting a second capture.
            Paths64 currentTerritory = capturer._area.CurrentTerritory;
            Rect64 captureBounds = Clipper.GetBounds(captureShape);
            float capturedArea = (float)(Math.Abs(Clipper.Area(captureShape)) /
                                               (GeometryUtils.Scale * GeometryUtils.Scale));

            // ── Background thread: heavy Clipper Union ────────────────────────
            Paths64 newCapturerTerritory = null;
            Exception bgException = null;

            var task = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Paths64 united = Clipper.Union(currentTerritory, captureShape, FillRule.NonZero);
                    newCapturerTerritory = Clipper.RamerDouglasPeucker(united, 0.05 * GeometryUtils.Scale);
                }
                catch (Exception ex)
                {
                    bgException = ex;
                }
            });

            // Yield every frame until the thread-pool task finishes.
            // The main thread keeps running at full speed — no freeze.
            yield return new WaitUntil(() => task.IsCompleted);

            if (bgException != null)
            {
                Debug.LogError("CollisionManager.ProcessTerritoryCaptureAsync: " +
                               $"background Union failed — {bgException.Message}");
                yield break;
            }

            // ── Main thread: apply results ────────────────────────────────────
            // Capturer may have been killed by a trail-cut while the thread was running.
            if (!_allCharacters.Contains(capturer))
                yield break;

            capturer._area.SetTerritory(newCapturerTerritory);
            capturer._area.ShowCreatedTerritory(captureShape);

            // Process steals and area-capture kills against every other character
            for (int i = _allCharacters.Count - 1; i >= 0; i--)
            {
                Character victim = _allCharacters[i];
                if (victim == capturer) continue;
                if (!victim._area.TerritoryBounds.Intersects(captureBounds)) continue;

                // Area Capture Kill (point-in-polygon)
                var victimPt = new Point64(
                    victim.transform.position.x * GeometryUtils.Scale,
                    victim.transform.position.y * GeometryUtils.Scale);
                bool isInsideCapture = false;
                foreach (Path64 path in captureShape)
                {
                    if (Clipper.PointInPolygon(victimPt, path) != PointInPolygonResult.IsOutside)
                    {
                        isInsideCapture = true;
                        break;
                    }
                }

                if (isInsideCapture && victim.Config.CanBeKilledByAreaCapture)
                {
                    if (TryApplyPlayerTerritoryTakeover(capturer, victim,
                            victim.transform.position, -1, false, out float stolenAreaKill))
                        capturedArea += stolenAreaKill;

                    GameEvents.FireCharacterDied(new DeathInfo(capturer, victim));
                    victim.Die();
                    continue;
                }

                // Normal territory steal
                Paths64 newVictimTerritory = Clipper.Difference(
                    victim._area.CurrentTerritory, captureShape, FillRule.NonZero);

                if (newVictimTerritory.Count == 0)
                {
                    if (TryApplyPlayerTerritoryTakeover(capturer, victim,
                            victim.transform.position, -1, false, out float stolenAreaSteal))
                        capturedArea += stolenAreaSteal;

                    GameEvents.FireCharacterDied(new DeathInfo(capturer, victim));
                    victim.Die();
                }
                else
                {
                    newVictimTerritory = Clipper.RamerDouglasPeucker(
                        newVictimTerritory, 0.05 * GeometryUtils.Scale);
                    victim._area.SetTerritory(newVictimTerritory);
                }
            }

            if (TryGetPlayer(out Character player))
            {
                float fillPct = GetPlayerFillPercent(player);
                GameEvents.FireTerritoryCaptured(capturer, fillPct, capturedArea);
            }

            UpdateZoneOccupancy();
        }

        private bool TryApplyPlayerTerritoryTakeover(Character killer, Character victim, Vector3 trailEndPosition, int hitSegmentIndex, bool useHitSegment, out float stolenArea)
        {
            stolenArea = 0f;
            if (killer == null || victim == null || !killer.IsPlayer || victim.IsPlayer || killer._area == null || victim._area == null)
            {
                return false;
            }

            Paths64 stolenShape = victim._area.CurrentTerritory;
            if (stolenShape == null || stolenShape.Count == 0)
            {
                return false;
            }

            Paths64 pendingShape = BuildVictimPendingShape(victim, trailEndPosition, hitSegmentIndex, useHitSegment);
            if (pendingShape != null && pendingShape.Count > 0)
            {
                stolenShape = Clipper.Union(stolenShape, pendingShape, FillRule.NonZero);
            }

            if (stolenShape.Count == 0)
            {
                return false;
            }

            float previousArea = GetScaledArea(killer._area.CurrentTerritory);
            Paths64 newKillerTerritory = Clipper.Union(killer._area.CurrentTerritory, stolenShape, FillRule.NonZero);
            newKillerTerritory = Clipper.RamerDouglasPeucker(newKillerTerritory, 0.05 * GeometryUtils.Scale);
            killer._area.SetTerritory(newKillerTerritory);
            killer._area.PlayZoneTransitionVfx(stolenShape, victim.Skin, killer.transform.position);

            stolenArea = Mathf.Max(0f, GetScaledArea(newKillerTerritory) - previousArea);
            return stolenArea > 0f;
        }

        private Paths64 BuildVictimPendingShape(Character victim, Vector3 trailEndPosition, int hitSegmentIndex, bool useHitSegment)
        {
            List<Vector3> logicPoints = victim._trail._logicPoints;
            if (logicPoints.Count == 0)
            {
                return null;
            }

            _takeoverTrail.Clear();
            int lastPointIndex = logicPoints.Count - 1;
            if (useHitSegment)
            {
                lastPointIndex = Mathf.Clamp(hitSegmentIndex, 0, logicPoints.Count - 1);
            }

            for (int i = 0; i <= lastPointIndex; i++)
            {
                _takeoverTrail.Add(logicPoints[i]);
            }

            _takeoverTrail.Add(trailEndPosition);
            if (_takeoverTrail.Count < 2)
            {
                return null;
            }

            return CaptureSolver.SolveCapture(_takeoverTrail, victim._area.CurrentTerritory, victim._characterRadius);
        }

        private bool TryGetTrailHit(Character killer, Character victim, out TrailHitInfo trailHit)
        {
            for (int i = 0; i < _trailHitsThisFrame.Count; i++)
            {
                TrailHitInfo candidate = _trailHitsThisFrame[i];
                if (candidate.Killer == killer && candidate.Victim == victim)
                {
                    trailHit = candidate;
                    return true;
                }
            }

            trailHit = default;
            return false;
        }

        private float GetScaledArea(Paths64 paths)
        {
            return (float)(Math.Abs(Clipper.Area(paths)) / (GeometryUtils.Scale * GeometryUtils.Scale));
        }

        private void HandleGameStateChanged(GameState state)
        {
            // Seed occupancy once the simulation becomes active so own-zone enter events fire on
            // spawn. Death/Win/EndCard intentionally freeze tracking — last-known occupancy sticks.
            if (state == GameState.Tutorial || state == GameState.Playing)
            {
                UpdateZoneOccupancy();
            }
        }

        private void UpdateZoneOccupancy()
        {
            // Enumeration phase: read-only over _allCharacters, write-only to _sweepTransitions.
            _sweepTransitions.Clear();
            for (int i = 0; i < _allCharacters.Count; i++)
            {
                Character c = _allCharacters[i];
                if (c == null || c._area == null)
                {
                    continue;
                }

                _zoneOccupancy.TryGetValue(c, out Character prevOwner);
                Character newOwner = ComputeZoneOwner(c, prevOwner);
                if (newOwner != prevOwner)
                {
                    _sweepTransitions.Add(new ZoneTransition(c, prevOwner, newOwner));
                }
            }

            // Commit phase: apply dictionary mutations BEFORE firing any events so a subscriber
            // that calls back into CollisionManager observes a fully consistent _zoneOccupancy.
            for (int i = 0; i < _sweepTransitions.Count; i++)
            {
                ZoneTransition t = _sweepTransitions[i];
                if (t.NewOwner != null)
                {
                    _zoneOccupancy[t.Mover] = t.NewOwner;
                }
                else
                {
                    _zoneOccupancy.Remove(t.Mover);
                }
            }

            // Fanout phase: fire exit(prev) then enter(next) for each buffered transition. The
            // local index i is safe against nested calls because DeregisterCharacter and any other
            // nested sweep use their own dedicated buffers.
            for (int i = 0; i < _sweepTransitions.Count; i++)
            {
                ZoneTransition t = _sweepTransitions[i];
                if (t.PrevOwner != null)
                {
                    GameEvents.FireCharacterExitedZone(t.Mover, t.PrevOwner);
                }
                if (t.NewOwner != null)
                {
                    GameEvents.FireCharacterEnteredZone(t.Mover, t.NewOwner);
                }
            }
            _sweepTransitions.Clear();
        }

        /// <summary>
        /// Three-rule priority: own zone wins, else prev-owner stability (prevents single-frame
        /// flicker on shared borders where Clipper treats boundary points as inside both zones),
        /// else scan remaining characters for a containing zone. Returns null for neutral ground.
        /// </summary>
        private Character ComputeZoneOwner(Character c, Character prevOwner)
        {
            Vector3 point = c.transform.position;

            // Rule 1: own zone wins.
            if (c._area.IsPointInside(point))
            {
                return c;
            }

            // Rule 2: prev-owner stability. No zone in paper.io is nested inside another (Clipper's
            // Difference enforces this at capture time), so sticking with the recorded owner can't
            // lock the player to a stale owner once the true owner changes.
            if (prevOwner != null && prevOwner != c && prevOwner._area != null && prevOwner._area.IsPointInside(point))
            {
                return prevOwner;
            }

            // Rule 3: scan others, skipping self (rule 1) and prevOwner (rule 2) since both were
            // already tested.
            return ScanForZoneOwner(point, c, prevOwner);
        }

        private Character ScanForZoneOwner(Vector3 point, Character skipA, Character skipB)
        {
            for (int i = 0; i < _allCharacters.Count; i++)
            {
                Character candidate = _allCharacters[i];
                if (candidate == skipA || candidate == skipB || candidate._area == null)
                {
                    continue;
                }

                if (candidate._area.IsPointInside(point))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool ContainsTrailHitVictim(List<TrailHitInfo> hits, Character victim)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Victim == victim)
                {
                    return true;
                }
            }

            return false;
        }

        private void WarmUpClipper()
        {
            Paths64 dummyPath1 = GeometryUtils.CreateCirclePath64(1f, Vector3.zero, 8);
            Paths64 dummyPath2 = GeometryUtils.CreateCirclePath64(1f, Vector3.right, 8);
            Clipper.Union(dummyPath1, dummyPath2, FillRule.NonZero);
            var dummyMesh = new Mesh();
            GeometryUtils.UpdateMeshWithPaths(dummyMesh, dummyPath1);
            UnityEngine.Object.Destroy(dummyMesh);
        }
    }
}
