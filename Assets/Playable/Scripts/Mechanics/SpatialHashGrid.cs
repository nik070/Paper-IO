using System;
using System.Collections.Generic;
using Gameplay;
using UnityEngine;

namespace Mechanics
{
    public class SpatialHashGrid
    {
        private const int MaxSegmentsPerCell = 128;
        private readonly float _cellSize;
        private readonly int _width, _height;
        private readonly float _offsetX, _offsetY;

        private readonly int[] _cellSegmentCounts;
        private readonly TrailSegment[] _gridData;

        public SpatialHashGrid(float width, float height, float cellSize)
        {
            _cellSize = cellSize;
            _offsetX = width / 2f;
            _offsetY = height / 2f;

            _width = Mathf.CeilToInt(width / cellSize);
            _height = Mathf.CeilToInt(height / cellSize);

            int totalCells = _width * _height;
            _cellSegmentCounts = new int[totalCells];
            _gridData = new TrailSegment[totalCells * MaxSegmentsPerCell];
        }

        public void Clear()
        {
            Array.Clear(_cellSegmentCounts, 0, _cellSegmentCounts.Length);
        }

        public void InsertSegment(TrailSegment segment)
        {
            float sx1 = segment.Start.x + _offsetX;
            float sy1 = segment.Start.y + _offsetY;
            float sx2 = segment.End.x + _offsetX;
            float sy2 = segment.End.y + _offsetY;

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(sx1, sx2) / _cellSize), 0, _width - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(sx1, sx2) / _cellSize), 0, _width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(sy1, sy2) / _cellSize), 0, _height - 1);
            int maxY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(sy1, sy2) / _cellSize), 0, _height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int cellIdx = y * _width + x;
                    int count = _cellSegmentCounts[cellIdx];

                    if (count < MaxSegmentsPerCell)
                    {
                        _gridData[cellIdx * MaxSegmentsPerCell + count] = segment;
                        _cellSegmentCounts[cellIdx]++;
                    }
                }
            }
        }

        public void CheckIntersection(Vector2 moveStart, Vector2 moveEnd, Character checkingCharacter, int headIndex, List<TrailHitInfo> outHits)
        {
            outHits.Clear();

            float mx1 = moveStart.x + _offsetX;
            float my1 = moveStart.y + _offsetY;
            float mx2 = moveEnd.x + _offsetX;
            float my2 = moveEnd.y + _offsetY;

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(mx1, mx2) / _cellSize), 0, _width - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(mx1, mx2) / _cellSize), 0, _width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(my1, my2) / _cellSize), 0, _height - 1);
            int maxY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(my1, my2) / _cellSize), 0, _height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int cellIdx = y * _width + x;
                    int count = _cellSegmentCounts[cellIdx];
                    int offset = cellIdx * MaxSegmentsPerCell;

                    for (int i = 0; i < count; i++)
                    {
                        TrailSegment targetSeg = _gridData[offset + i];

                        bool isSelf = targetSeg.Owner == checkingCharacter;

                        if (isSelf && !checkingCharacter.Config.CanKillSelfWithTrail)
                        {
                            continue;
                        }

                        if (!isSelf && !targetSeg.Owner.Config.CanBeKilledIfTrailCut)
                        {
                            continue;
                        }

                        if (isSelf && headIndex - targetSeg.SegmentIndex <= 4)
                        {
                            continue;
                        }

                        if (ContainsVictim(outHits, targetSeg.Owner))
                        {
                            continue;
                        }

                        var p1 = new Vector3(moveStart.x, moveStart.y, -0.1f);
                        var p2 = new Vector3(moveEnd.x, moveEnd.y, -0.1f);
                        var p3 = new Vector3(targetSeg.Start.x, targetSeg.Start.y, -0.1f);
                        var p4 = new Vector3(targetSeg.End.x, targetSeg.End.y, -0.1f);

                        Debug.DrawLine(p1, p2, Color.yellow, 0.1f);
                        Debug.DrawLine(p3, p4, Color.blue, 0.1f);

                        if (FastMath.TryGetSegmentsIntersectionPoint(moveStart, moveEnd, targetSeg.Start, targetSeg.End, out Vector2 hitPoint))
                        {
                            Debug.DrawLine(p1, p2, Color.red, 2f);
                            Debug.DrawLine(p3, p4, Color.red, 2f);
                            outHits.Add(new TrailHitInfo
                            {
                                Killer = checkingCharacter,
                                Victim = targetSeg.Owner,
                                HitPoint = hitPoint,
                                SegmentIndex = targetSeg.SegmentIndex
                            });
                        }
                    }
                }
            }
        }

        private bool ContainsVictim(List<TrailHitInfo> hits, Character victim)
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
    }
}
