using Gameplay;
using UnityEngine;

namespace Mechanics
{
    public struct TrailSegment
    {
        public Character Owner;
        public int SegmentIndex;
        public Vector2 Start;
        public Vector2 End;
    }
}
