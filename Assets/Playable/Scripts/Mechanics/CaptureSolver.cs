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

            // OPTIMIZATION 1: Ramer-Douglas-Peucker Simplification
            // Removes unnecessary vertices along straight lines.
            // 0.1f is the error tolerance. This cuts the math workload by 80%+ instantly.
            Path64 simplifiedTrail = Clipper.RamerDouglasPeucker(rawTrail, 0.1 * GeometryUtils.Scale);

            Point64 startPt = simplifiedTrail[0];
            Point64 endPt = simplifiedTrail[simplifiedTrail.Count - 1];

            GetClosestVertex(currentTerritory, startPt, out int startIsland, out int startIdx);
            GetClosestVertex(currentTerritory, endPt, out int endIsland, out int endIdx);

            // OPTIMIZATION 2: JoinType.Square
            // We still inflate the trail to fix the Figure-8 pinch point, but 'Square'
            // prevents the catastrophic "vertex explosion" caused by 'Round' arcs.
            Paths64 thickTrail = Clipper.InflatePaths(
                new Paths64 { simplifiedTrail },
                playerRadius * GeometryUtils.Scale,
                JoinType.Square,
                EndType.Square);

            // ISLAND BRIDGE CASE
            if (startIsland != endIsland || startIsland == -1)
            {
                return thickTrail; // Return the simplified, thickened bridge
            }

            // STANDARD CAPTURE
            Path64 island = currentTerritory[startIsland];

            Path64 pathForward = GetBoundaryPath(island, endIdx, startIdx, true);
            Path64 pathBackward = GetBoundaryPath(island, endIdx, startIdx, false);
            Path64 shortestBoundary = (GetPathLength(pathForward) <= GetPathLength(pathBackward)) ? pathForward : pathBackward;

            Path64 capturePolygon = new Path64();
            capturePolygon.AddRange(simplifiedTrail);
            capturePolygon.AddRange(shortestBoundary);

            // OPTIMIZATION 3: Single-Pass Merge
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

            // ---- Frame 1: prep (RDP + closest-vertex + Inflate + boundary walk) ----
            Path64 rawTrail = GeometryUtils.ToPath64(trail);
            Path64 simplifiedTrail = Clipper.RamerDouglasPeucker(rawTrail, 0.1 * GeometryUtils.Scale);

            Point64 startPt = simplifiedTrail[0];
            Point64 endPt = simplifiedTrail[simplifiedTrail.Count - 1];
            GetClosestVertex(currentTerritory, startPt, out int startIsland, out int startIdx);
            GetClosestVertex(currentTerritory, endPt, out int endIsland, out int endIdx);

            Paths64 thickTrail = Clipper.InflatePaths(
                new Paths64 { simplifiedTrail },
                playerRadius * GeometryUtils.Scale,
                JoinType.Square,
                EndType.Square);

            if (startIsland != endIsland || startIsland == -1)
            {
                onComplete?.Invoke(thickTrail);
                yield break;
            }

            Path64 island = currentTerritory[startIsland];
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
