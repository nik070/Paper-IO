using System.Collections.Generic;
using UnityEngine;
using LibTessDotNet;
using Clipper2Lib;
using Mesh = UnityEngine.Mesh;

namespace Mechanics
{
    public static class GeometryUtils
    {
        // Clipper2 uses integers. We scale Unity floats by 10,000 to preserve precision.
        public const double Scale = 10000.0;

        // Zone UV scale matching Paper2 (MeshUtils.RecalculateUVs: position / 75f).
        private const float UvScale = 75f;
        private const float InverseUvScale = 1f / UvScale;

        #region Coordinate Conversions
        public static Point64 ToPoint64(Vector3 v)
        {
            return new Point64(v.x * Scale, v.y * Scale);
        }

        public static Vector3 ToVector3(Point64 p, float z = 0f)
        {
            return new Vector3((float)(p.X / Scale), (float)(p.Y / Scale), z);
        }

        public static Path64 ToPath64(List<Vector3> points)
        {
            Path64 path = new Path64(points.Count);
            foreach (var p in points) path.Add(ToPoint64(p));
            return path;
        }
        #endregion

        #region Hole Handling
        /// <summary>
        /// Drops hole rings (negative signed area) from a Clipper2 result so the territory
        /// renders as a solid fill. Paper.io capture semantics treat any enclosed region as
        /// owned, so the inner negative-winding rings that Union/Difference can produce must
        /// not survive into the mesh tessellator (which uses EvenOdd and would carve them out)
        /// or into point-in-polygon checks (which would report the filled hole as "outside").
        /// Positive-area outer rings — including disjoint islands — are preserved unchanged.
        /// </summary>
        public static Paths64 RemoveHoles(Paths64 paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return paths;
            }

            Paths64 outerOnly = new Paths64(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                if (Clipper.Area(paths[i]) > 0)
                {
                    outerOnly.Add(paths[i]);
                }
            }
            return outerOnly;
        }
        #endregion

        #region Initial Shape Generation
        public static Paths64 CreateCirclePath64(float radius, Vector3 center, int segments = 48)
        {
            Path64 path = new Path64(segments);
            float angleStep = (Mathf.PI * 2f) / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                float x = center.x + Mathf.Cos(angle) * radius;
                float y = center.y + Mathf.Sin(angle) * radius;
                path.Add(ToPoint64(new Vector3(x, y, 0)));
            }
            return new Paths64 { path };
        }
        #endregion

        #region Runtime Smoothing
        /// <summary>
        /// Applies a single Chaikin subdivision pass to every path in the set.
        /// Each edge (p0→p1) is replaced with two smoothed points:
        ///   Q = ¾·p0 + ¼·p1   and   R = ¼·p0 + ¾·p1
        /// One pass is enough to round off the sharp corners left by RDP
        /// simplification while keeping vertex counts manageable for runtime.
        /// </summary>
        public static Paths64 SmoothPaths(Paths64 paths, int iterations = 1)
        {
            if (paths == null || paths.Count == 0) return paths;

            Paths64 result = new Paths64(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                Path64 path = paths[i];
                if (path.Count < 3)
                {
                    result.Add(path);
                    continue;
                }

                for (int iter = 0; iter < iterations; iter++)
                {
                    int n = path.Count;
                    Path64 smooth = new Path64(n * 2);
                    for (int j = 0; j < n; j++)
                    {
                        Point64 p0 = path[j];
                        Point64 p1 = path[(j + 1) % n];
                        // Q = 3/4 * p0 + 1/4 * p1
                        smooth.Add(new Point64((3 * p0.X + p1.X) / 4, (3 * p0.Y + p1.Y) / 4));
                        // R = 1/4 * p0 + 3/4 * p1
                        smooth.Add(new Point64((p0.X + 3 * p1.X) / 4, (p0.Y + 3 * p1.Y) / 4));
                    }
                    path = smooth;
                }

                result.Add(path);
            }
            return result;
        }
        #endregion

        #region LibTessDotNet Hyper-Optimized Generation

        // 1. Shared memory pool for Tess. This prevents OutRecPoolList..cctor GC spikes!
        private static DefaultPool _sharedTessPool = new DefaultPool();

        // 2. Pre-allocated buffers prevent generating hundreds of thousands of garbage arrays
        private static List<Vector3> _vertexBuffer = new List<Vector3>(2048);
        private static List<int> _triangleBuffer = new List<int>(6144);
        private static List<Vector2> _uvBuffer = new List<Vector2>(2048);
        private static List<Vector3> _normalBuffer = new List<Vector3>(2048);
        private static List<ContourVertex> _contourBuffer = new List<ContourVertex>(4096);

        /// <summary>
        /// Updates an existing mesh with zero-allocation buffers to prevent GC freezes.
        /// TODO: check if we can use Unity's Collider.Triangulate Mesh API instead of Tess.
        /// </summary>
        public static void UpdateMeshWithPaths(Mesh targetMesh, Paths64 paths, float zPos = 0f)
        {
            if (paths == null || paths.Count == 0)
            {
                targetMesh.Clear();
                return;
            }

            // Pass the shared pool to prevent memory allocation
            Tess tess = new Tess(_sharedTessPool);

            foreach (Path64 path in paths)
            {
                if (path.Count < 3) continue;

                _contourBuffer.Clear();
                for (int i = 0; i < path.Count; i++)
                {
                    _contourBuffer.Add(new ContourVertex(new Vec3((float)(path[i].X / Scale), (float)(path[i].Y / Scale), 0)));
                }

                tess.AddContour(_contourBuffer, ContourOrientation.Original);
            }

            tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

            // Clear static buffers
            _vertexBuffer.Clear();
            _triangleBuffer.Clear();
            _uvBuffer.Clear();
            _normalBuffer.Clear();

            int numVertices = tess.VertexCount;
            for (int i = 0; i < numVertices; i++)
            {
                float x = tess.Vertices[i].Position.X;
                float y = tess.Vertices[i].Position.Y;

                _vertexBuffer.Add(new Vector3(x, y, zPos));
                _uvBuffer.Add(new Vector2(x * InverseUvScale, y * InverseUvScale));
                _normalBuffer.Add(Vector3.back);
            }

            for (int i = 0; i < tess.ElementCount; i++)
            {
                // Flip 2nd and 3rd to fix Backface Culling
                _triangleBuffer.Add(tess.Elements[i * 3]);
                _triangleBuffer.Add(tess.Elements[i * 3 + 2]);
                _triangleBuffer.Add(tess.Elements[i * 3 + 1]);
            }

            // Apply buffers directly to the existing mesh (Extremely fast, zero garbage)
            targetMesh.Clear();
            targetMesh.SetVertices(_vertexBuffer);
            targetMesh.SetTriangles(_triangleBuffer, 0);
            targetMesh.SetUVs(0, _uvBuffer);
            targetMesh.SetNormals(_normalBuffer);

            targetMesh.RecalculateBounds();
        }
        #endregion
    }
}
