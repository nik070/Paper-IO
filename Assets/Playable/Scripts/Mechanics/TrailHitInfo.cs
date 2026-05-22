using Gameplay;
using UnityEngine;

namespace Mechanics
{
    public struct TrailHitInfo
    {
        public Character Killer;
        public Character Victim;
        public Vector2 HitPoint;
        public int SegmentIndex;
    }
}
