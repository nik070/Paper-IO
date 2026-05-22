using UnityEngine;

namespace Mechanics
{
    public static class FastMath
    {
        public static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float epsilon = 0.001f;

            if (Mathf.Max(p1.x, p2.x) < Mathf.Min(p3.x, p4.x) - epsilon ||
                Mathf.Min(p1.x, p2.x) > Mathf.Max(p3.x, p4.x) + epsilon ||
                Mathf.Max(p1.y, p2.y) < Mathf.Min(p3.y, p4.y) - epsilon ||
                Mathf.Min(p1.y, p2.y) > Mathf.Max(p3.y, p4.y) + epsilon)
            {
                return false;
            }

            float cp1 = CrossProduct2D(p1, p2, p3);
            float cp2 = CrossProduct2D(p1, p2, p4);
            float cp3 = CrossProduct2D(p3, p4, p1);
            float cp4 = CrossProduct2D(p3, p4, p2);

            if (((cp1 > 0 && cp2 < 0) || (cp1 < 0 && cp2 > 0)) &&
                ((cp3 > 0 && cp4 < 0) || (cp3 < 0 && cp4 > 0)))
            {
                return true;
            }

            if (Mathf.Abs(cp1) < epsilon && IsPointOnSegment(p1, p2, p3, epsilon))
            {
                return true;
            }

            if (Mathf.Abs(cp2) < epsilon && IsPointOnSegment(p1, p2, p4, epsilon))
            {
                return true;
            }

            if (Mathf.Abs(cp3) < epsilon && IsPointOnSegment(p3, p4, p1, epsilon))
            {
                return true;
            }

            if (Mathf.Abs(cp4) < epsilon && IsPointOnSegment(p3, p4, p2, epsilon))
            {
                return true;
            }

            return false;
        }

        public static bool TryGetSegmentsIntersectionPoint(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 point)
        {
            point = Vector2.zero;

            if (!SegmentsIntersect(p1, p2, p3, p4))
            {
                return false;
            }

            Vector2 r = p2 - p1;
            Vector2 s = p4 - p3;
            float denominator = (r.x * s.y) - (r.y * s.x);

            if (Mathf.Abs(denominator) < 0.0001f)
            {
                point = (p2 + p3) * 0.5f;
                return true;
            }

            Vector2 diff = p3 - p1;
            float t = ((diff.x * s.y) - (diff.y * s.x)) / denominator;
            point = p1 + (r * t);
            return true;
        }

        private static float CrossProduct2D(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static bool IsPointOnSegment(Vector2 start, Vector2 end, Vector2 point, float epsilon)
        {
            return point.x >= Mathf.Min(start.x, end.x) - epsilon && point.x <= Mathf.Max(start.x, end.x) + epsilon &&
                   point.y >= Mathf.Min(start.y, end.y) - epsilon && point.y <= Mathf.Max(start.y, end.y) + epsilon;
        }
    }
}
