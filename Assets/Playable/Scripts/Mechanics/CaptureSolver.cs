using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Clipper2Lib;

namespace Mechanics
{
    public static class CaptureSolver
    {
        public static Paths64 SolveCapture(List<Vector3> trail, Paths64 currentTerritory, float playerRadius)
        {
            if (trail.Count < 2 || currentTerritory.Count == 0) return new Paths64();

            Path64 rawTrail = GeometryUtils.ToPath64(trail);
            Path64 simplifiedTrail = Clipper.RamerDouglasPeucker(rawTrail, 0.02 * GeometryUtils.Scale);

            Point64 startPt = simplifiedTrail[0];
            Point64 endPt = simplifiedTrail[simplifiedTrail.Count - 1];

            GetClosestVertex(currentTerritory, startPt, out int startIsland, out int startIdx);
            GetClosestVertex(currentTerritory, endPt, out int endIsland, out int endIdx);

            // ISLAND BRIDGE CASE — no boundary walk possible
            if (startIsland != endIsland || startIsland == -1)
            {
                Paths64 bridgeTrail = Clipper.InflatePaths(
                    new Paths64 { simplifiedTrail },
                    playerRadius * GeometryUtils.Scale,
                    JoinType.Miter,
                    EndType.Square);
                return bridgeTrail;
            }

            // STANDARD CAPTURE
            Path64 island = currentTerritory[startIsland];

            // *** KEY FIX: Snap trail endpoints to the actual boundary vertices ***
            // The trail starts/ends at the player's center, which is slightly offset from the
            // territory boundary. This offset creates triangular gap slivers at the junction.
            // By snapping the endpoints directly onto the boundary vertices, the capture polygon
            // becomes a perfectly closed loop with zero gaps.
            simplifiedTrail[0] = island[startIdx];
            simplifiedTrail[simplifiedTrail.Count - 1] = island[endIdx];

            // Inflate the snapped trail so the thick trail also covers the snapped endpoints
            Paths64 thickTrail = Clipper.InflatePaths(
                new Paths64 { simplifiedTrail },
                playerRadius * GeometryUtils.Scale,
                JoinType.Miter,
                EndType.Square);

            Path64 pathForward = GetBoundaryPath(island, endIdx, startIdx, true);
            Path64 pathBackward = GetBoundaryPath(island, endIdx, startIdx, false);
            Path64 shortestBoundary = (GetPathLength(pathForward) <= GetPathLength(pathBackward)) ? pathForward : pathBackward;

            Path64 capturePolygon = new Path64();
            capturePolygon.AddRange(simplifiedTrail);
            capturePolygon.AddRange(shortestBoundary);

            // NonZero natively keeps both the +1 and -1 loops of a Figure-8.
            // Unioning the Lasso with the ThickTrail perfectly welds the crossover pinch point.
            return Clipper.Union(new Paths64 { capturePolygon }, thickTrail, FillRule.NonZero);
        }

        /// <summary>
        /// Same result as SolveCapture, but yields between expensive Clipper2 steps so the work
        /// spans multiple frames instead of stalling one frame for ~150 ms on throttled mobile.
        /// Caller must keep the input lists/territory stable for the lifetime of the coroutine
        /// (snapshot before yielding control to other code).
        /// </summary>
        public static IEnumerator SolveCaptureAsync(
            List<Vector3> trail,
            Paths64 currentTerritory,
            float playerRadius,
            Action<Paths64> onComplete)
        {
            if (trail.Count < 2 || currentTerritory.Count == 0)
            {
                onComplete?.Invoke(new Paths64());
                yield break;
            }

            // ---- Frame 1: prep (RDP + closest-vertex + snap + Inflate + boundary walk) ----
            Path64 rawTrail = GeometryUtils.ToPath64(trail);
            Path64 simplifiedTrail = Clipper.RamerDouglasPeucker(rawTrail, 0.02 * GeometryUtils.Scale);

            Point64 startPt = simplifiedTrail[0];
            Point64 endPt = simplifiedTrail[simplifiedTrail.Count - 1];
            GetClosestVertex(currentTerritory, startPt, out int startIsland, out int startIdx);
            GetClosestVertex(currentTerritory, endPt, out int endIsland, out int endIdx);

            if (startIsland != endIsland || startIsland == -1)
            {
                Paths64 bridgeTrail = Clipper.InflatePaths(
                    new Paths64 { simplifiedTrail },
                    playerRadius * GeometryUtils.Scale,
                    JoinType.Miter,
                    EndType.Square);
                onComplete?.Invoke(bridgeTrail);
                yield break;
            }

            Path64 island = currentTerritory[startIsland];

            // *** KEY FIX: Snap trail endpoints to the actual boundary vertices ***
            simplifiedTrail[0] = island[startIdx];
            simplifiedTrail[simplifiedTrail.Count - 1] = island[endIdx];

            // Inflate the snapped trail
            Paths64 thickTrail = Clipper.InflatePaths(
                new Paths64 { simplifiedTrail },
                playerRadius * GeometryUtils.Scale,
                JoinType.Miter,
                EndType.Square);

            Path64 pathForward = GetBoundaryPath(island, endIdx, startIdx, true);
            Path64 pathBackward = GetBoundaryPath(island, endIdx, startIdx, false);
            Path64 shortestBoundary = (GetPathLength(pathForward) <= GetPathLength(pathBackward)) ? pathForward : pathBackward;

            Path64 capturePolygon = new Path64();
            capturePolygon.AddRange(simplifiedTrail);
            capturePolygon.AddRange(shortestBoundary);

            yield return null;

            // ---- Frame 2: the heavy Clipper.Union ----
            Paths64 result = Clipper.Union(new Paths64 { capturePolygon }, thickTrail, FillRule.NonZero);
            onComplete?.Invoke(result);
        }

        private static void GetClosestVertex(Paths64 territory, Point64 target, out int islandIdx, out int vertexIdx)
        {
            islandIdx = -1;
            vertexIdx = -1;
            double minSqDist = double.MaxValue;

            for (int i = 0; i < territory.Count; i++)
            {
                Path64 island = territory[i];
                for (int j = 0; j < island.Count; j++)
                {
                    double sqDist = Clipper.DistanceSqr(target, island[j]);
                    if (sqDist < minSqDist)
                    {
                        minSqDist = sqDist;
                        islandIdx = i;
                        vertexIdx = j;
                    }
                }
            }
        }

        private static Path64 GetBoundaryPath(Path64 island, int fromIdx, int toIdx, bool forward)
        {
            Path64 path = new Path64();
            int count = island.Count;
            int curr = fromIdx;

            while (curr != toIdx)
            {
                path.Add(island[curr]);
                if (forward)
                {
                    curr = (curr + 1) % count;
                }
                else
                {
                    curr--;
                    if (curr < 0) curr = count - 1;
                }
            }
            path.Add(island[toIdx]);
            return path;
        }

        private static double GetPathLength(Path64 path)
        {
            double length = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                length += System.Math.Sqrt(Clipper.DistanceSqr(path[i], path[i + 1]));
            }
            return length;
        }
    }
}
