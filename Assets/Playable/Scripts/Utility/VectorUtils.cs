using Clipper2Lib;
using Mechanics;
using UnityEngine;
using Random = System.Random;

namespace Utility
{
    public static class VectorUtils
    {
        /*public static Vector3 Abs(this Vector3 vector3) => new Vector3(Mathf.Abs(vector3.x), Mathf.Abs(vector3.y), Mathf.Abs(vector3.z));

        public static Vector2 ToVector2(this Vector3 vector3) => new Vector2(vector3.x, vector3.z);

        public static float RandomBetween(this Vector2 vector2) => UnityEngine.Random.Range(vector2.x, vector2.y);

        public static float RandomBetween(this Vector2 vector2, Random random) =>
            Mathf.Lerp(vector2.x, vector2.y, (float)random.NextDouble());

        public static int RandomBetween(this Vector2Int vector2) => UnityEngine.Random.Range(vector2.x, vector2.y);
        public static int RandomBetweenInclusive(this Vector2Int vector2) => UnityEngine.Random.Range(vector2.x, vector2.y + 1);
        public static float RandomBetween(this Vector2Int vector2, Random random) => random.Next(vector2.x, vector2.y);

        public static float Lerp(this Vector2 vector2, float t) => Mathf.Lerp(vector2.x, vector2.y, t);

        public static float LerpUnclamped(this Vector2 vector2, float t) =>
            Mathf.LerpUnclamped(vector2.x, vector2.y, t);

        public static float InverseLerp(this Vector2 vector2, float t) => Mathf.InverseLerp(vector2.x, vector2.y, t);

        public static Vector3 WithX(this Vector3 vector3, float x) => new(x, vector3.y, vector3.z);

        public static Vector3 WithY(this Vector3 vector3, float y) => new(vector3.x, y, vector3.z);*/

        public static Vector3 WithZ(this Vector3 vector3, float z) => new Vector3(vector3.x, vector3.y, z);

        public static Vector2 WithX(this Vector2 vector2, float x) => new Vector2(x, vector2.y);

        public static Vector2 WithY(this Vector2 vector2, float y) => new Vector2(vector2.x, y);

        public static void SetX(this Transform t, float x)
        {
            var p = t.position;
            p.x = x;
            t.position = p;
        }

        public static Vector3 ToVector3(this Point64 point64, float z = 0)
        {
            return new Vector3((float)(point64.X / GeometryUtils.Scale), (float)(point64.Y / GeometryUtils.Scale), z);
        }

        public static Bounds ToBounds(this Rect64 rect64, float centerZ)
        {
            return new Bounds(rect64.MidPoint().ToVector3(centerZ), rect64.Diagonal().ToVector3(centerZ));
        }
    }
}
