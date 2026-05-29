using Clipper2Lib;
using Gameplay;
using Mechanics;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Generates a Paths64 polygon from the black pixels of a B&W texture.
    /// LevelManager calls ApplyTo(player._area) right after the player spawns, so the
    /// pattern IS the player's starting territory — no extra mesh, nothing decorative.
    ///
    /// Key settings:
    ///   _generationResolution — how many pixels are sampled per axis (independent of
    ///     texture size). Low values = fast bake + blocky; high values = slower + detailed.
    ///     256 is the sweet spot. Fixes both the "pixelated at 225px" and "full arena at 4096px" issues.
    ///   _edgeSmoothing — inflate + deflate pass that rounds pixel-stair edges into curves.
    ///
    /// For zero runtime cost, right-click → "Bake Pattern". Polygons are serialized into
    /// the scene so ApplyTo just deserializes them instantly on Play.
    ///
    /// Texture must have Read/Write Enabled in its import settings.
    /// </summary>
    public class TexturePatternTerritory : MonoBehaviour
    {
        [System.Serializable]
        private struct BakedPath
        {
            public Vector2[] points;
        }

        // ── Pattern ──────────────────────────────────────────────────────────
        [Header("Pattern")]
        [Tooltip("Black & white texture. Black pixels become territory. Must have Read/Write Enabled.")]
        [SerializeField] private Texture2D _patternTexture;

        [Tooltip("Pixels darker than this become territory. 0 = pure black only, 1 = everything.")]
        [Range(0f, 1f)][SerializeField] private float _blackThreshold = 0.5f;

        [Tooltip("Treat transparent pixels as non-territory regardless of colour.")]
        [SerializeField] private bool _ignoreTransparent = true;

        // ── Quality ───────────────────────────────────────────────────────────
        [Header("Quality")]
        [Tooltip("How many pixels are sampled per axis when building polygons.\n" +
                 "The texture is resampled to this resolution using area-averaging (not nearest-neighbour)\n" +
                 "so edges get proper sub-pixel coverage at any source size.\n" +
                 "• 256  — fast bake, good quality for most patterns.\n" +
                 "• 512  — recommended for 2048+ textures, smooth curves, ~2× slower bake.\n" +
                 "• 1024 — maximum detail, slow bake, use for very intricate designs.")]
        [Range(64, 1024)][SerializeField] private int _generationResolution = 512;

        [Tooltip("Chaikin subdivision passes applied to the polygon outline.\n" +
                 "Each pass halves the staircase sharpness and doubles vertex count (then SimplifyPaths trims it back).\n" +
                 "0 = raw pixel blocks.  3 = soft curves.  5 = very smooth.  6 = maximum.")]
        [Range(0, 6)][SerializeField] private int _smoothingIterations = 5;

        // ── Placement ─────────────────────────────────────────────────────────
        [Header("Placement")]
        [Tooltip("World-space centre of the pattern square.")]
        [SerializeField] private Vector2 _worldCenter = Vector2.zero;

        [Tooltip("World-space size of the pattern square. Set to 0 to auto-fit the arena diameter.")]
        [SerializeField] private float _worldSize = 0f;

        [Tooltip("Clip the generated territory to the arena circle.")]
        [SerializeField] private bool _clipToArena = true;

        [Header("Performance")]
        [Tooltip("Maximum total polygon vertices kept in the baked territory.\n" +
                 "Directly controls how fast every subsequent capture is:\n" +
                 "  400  = fastest captures, noticeably simplified shape.\n" +
                 "  800  = good balance (recommended).\n" +
                 "  1500 = most faithful to the texture, slightly slower captures.\n" +
                 "  0    = no limit (shape is exact, use only if captures feel instant).\n" +
                 "Re-bake after changing this value.")]
        [Range(0, 2000)][SerializeField] private int _maxVertexCount = 800;

        // ── Solid Fill ────────────────────────────────────────────────────────
        [Header("Solid Fill")]
        [Tooltip("Merge the pattern into ONE solid region with no internal gaps so the player " +
                 "starts on a single fillable island.\n" +
                 "Leave this ON for normal play: a fragmented pattern (separate petals/holes) makes " +
                 "capture loops land on different islands, which CaptureSolver can only outline — not " +
                 "fill — leaving the unfilled lattice.\n" +
                 "Turn OFF only if you deliberately want the decorative holey pattern.")]
        [SerializeField] private bool _fillSolid = true;

        [Tooltip("Largest gap (in world units) bridged when solidifying the pattern. Must exceed the " +
                 "spacing between pattern pieces to weld them into one island. Larger = more reliably " +
                 "solid but rounds away fine detail.")]
        [Range(0f, 10f)][SerializeField] private float _solidFillGapBridge = 3f;

        // ── Bake cache (hidden from inspector to prevent accidental edits) ────
        [SerializeField, HideInInspector] private BakedPath[] _bakedPaths;

        public bool HasBake => _bakedPaths != null && _bakedPaths.Length > 0;

        // ── Context-menu actions ──────────────────────────────────────────────

        [ContextMenu("Bake Pattern")]
        private void BakePattern()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Paths64 paths = GeneratePathsLive();
            _bakedPaths = ToBaked(paths);
            sw.Stop();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            int pathCount = _bakedPaths == null ? 0 : _bakedPaths.Length;
            int vertCount = 0;
            if (_bakedPaths != null)
                foreach (var bp in _bakedPaths)
                    if (bp.points != null) vertCount += bp.points.Length;

            Debug.Log($"TexturePatternTerritory: baked {pathCount} path(s), " +
                      $"{vertCount} total vertices in {sw.ElapsedMilliseconds}ms. " +
                      $"Runtime will now load instantly.");
        }

        [ContextMenu("Clear Bake")]
        private void ClearBake()
        {
            _bakedPaths = null;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log("TexturePatternTerritory: bake cleared — will regenerate live on next Play.");
        }

        [ContextMenu("Generate (debug log)")]
        private void DebugGenerate()
        {
            Paths64 paths = GeneratePaths();
            Debug.Log($"TexturePatternTerritory: {(paths == null ? 0 : paths.Count)} polygons (baked={HasBake}).");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Overwrite a CharacterArea's territory with the texture pattern.
        /// Pass <paramref name="spawnPosition"/> to union a starter circle in so the
        /// player always has a round safe zone at their spawn point.
        /// </summary>
        public void ApplyTo(CharacterArea area, Vector3? spawnPosition = null)
        {
            if (area == null) return;
            Paths64 paths = GeneratePaths();

            if (spawnPosition.HasValue)
            {
                Paths64 circle = GeometryUtils.CreateCirclePath64(
                    area.StartingAreaRadius, spawnPosition.Value);

                paths = (paths == null || paths.Count == 0)
                    ? circle
                    : Clipper.Union(paths, circle, FillRule.NonZero);
            }

            if (paths == null || paths.Count == 0)
            {
                Debug.LogWarning("TexturePatternTerritory: generated no polygons — nothing applied.");
                return;
            }

            if (_fillSolid)
            {
                // Weld the (often fragmented) pattern into one solid, gap-free island so capture
                // loops always close against a single boundary and get filled — see _fillSolid tooltip.
                paths = MakeSolid(paths);
            }

            // SetTerritoryClean skips RemoveHoles. When _fillSolid is on, MakeSolid has already
            // removed holes; when it's off, the pattern's white gaps are kept intentionally.
            area.SetTerritoryClean(paths);
        }

        /// <summary>
        /// Turns a possibly fragmented / holey pattern into a single solid region.
        /// A morphological "close" (inflate then deflate by the same radius) bridges the gaps
        /// between separate pattern pieces and seals enclosed cells; RemoveHoles then drops any
        /// remaining negative rings so the whole footprint is owned. Mirrors the inflate/deflate
        /// idiom already used in CharacterArea.GetMorphologicallyRoundedPaths.
        /// </summary>
        private Paths64 MakeSolid(Paths64 paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return paths;
            }

            double delta = _solidFillGapBridge * 0.5 * GeometryUtils.Scale;
            if (delta > 0)
            {
                Paths64 inflated = Clipper.InflatePaths(paths, delta, JoinType.Round, EndType.Polygon);
                if (inflated != null && inflated.Count > 0)
                {
                    Paths64 closed = Clipper.InflatePaths(inflated, -delta, JoinType.Round, EndType.Polygon);
                    if (closed != null && closed.Count > 0)
                    {
                        paths = closed;
                    }
                }
            }

            return GeometryUtils.RemoveHoles(paths);
        }

        /// <summary>
        /// Returns the Paths64 polygon set, using the baked cache when available (instant).
        /// Falls back to live generation with a reminder to bake.
        /// </summary>
        public Paths64 GeneratePaths()
        {
            if (HasBake)
                return FromBaked(_bakedPaths);

            Debug.LogWarning("TexturePatternTerritory: running live generation. " +
                             "Right-click → 'Bake Pattern' to cache for instant startup.");
            return GeneratePathsLive();
        }

        // ── Internal generation ───────────────────────────────────────────────

        private Paths64 GeneratePathsLive()
        {
            if (_patternTexture == null)
            {
                Debug.LogWarning("TexturePatternTerritory: no texture assigned.");
                return new Paths64();
            }

            // Read the source texture once at full resolution.
            Color32[] srcPixels;
            try
            {
                srcPixels = _patternTexture.GetPixels32();
            }
            catch (UnityException e)
            {
                Debug.LogError($"TexturePatternTerritory: texture '{_patternTexture.name}' is not readable. " +
                               $"Enable Read/Write in its import settings. ({e.Message})");
                return new Paths64();
            }

            int srcW = _patternTexture.width;
            int srcH = _patternTexture.height;

            // Resample to _generationResolution × _generationResolution.
            // This decouples polygon quality from the actual texture pixel count:
            //   • 225×225 src → 256×256 sample → no more tiny per-pixel quads
            //   • 4096×4096 src → 256×256 sample → no more arena flood-fill
            int res = _generationResolution;
            int threshold = Mathf.Clamp(Mathf.RoundToInt(_blackThreshold * 255f), 0, 255);

            GetWorldRect(out Vector2 rectMin, out Vector2 rectSize);
            float pxWorld = rectSize.x / res;
            float pyWorld = rectSize.y / res;

            // Area-average sampling via per-band column sums.
            // For each output row we sum the source pixel brightnesses column-by-column
            // across the corresponding band of source rows, build a 1-D prefix sum, then
            // query any x-range in O(1).  This is O(srcW × srcH) total — one pass through
            // the source pixels — and uses only O(srcW) working memory (no large SAT array,
            // no int overflow even for 4096×4096 textures since per-column sums stay in int
            // and the prefix uses long).
            int[] colSums = new int[srcW];
            long[] colPrefix = new long[srcW + 1];

            Paths64 rects = new Paths64();

            for (int y = 0; y < res; y++)
            {
                int srcY0 = y * srcH / res;
                int srcY1 = Mathf.Min((y + 1) * srcH / res, srcH);
                if (srcY1 <= srcY0) srcY1 = srcY0 + 1;
                int numSrcRows = srcY1 - srcY0;

                // Accumulate brightness for every source column across this y-band
                System.Array.Clear(colSums, 0, srcW);
                for (int sy = srcY0; sy < srcY1; sy++)
                {
                    int rowBase = sy * srcW;
                    for (int sx = 0; sx < srcW; sx++)
                    {
                        Color32 c = srcPixels[rowBase + sx];
                        int bright = (_ignoreTransparent && c.a < 128)
                            ? 255
                            : (c.r + c.g + c.b) / 3;
                        colSums[sx] += bright;
                    }
                }

                // 1-D prefix sum over column sums (long to avoid overflow on large textures)
                colPrefix[0] = 0;
                for (int sx = 0; sx < srcW; sx++)
                    colPrefix[sx + 1] = colPrefix[sx] + colSums[sx];

                int runStart = -1;

                for (int x = 0; x < res; x++)
                {
                    int srcX0 = x * srcW / res;
                    int srcX1 = Mathf.Min((x + 1) * srcW / res, srcW);
                    if (srcX1 <= srcX0) srcX1 = srcX0 + 1;

                    long sumBright = colPrefix[srcX1] - colPrefix[srcX0];
                    int pixelCount = (srcX1 - srcX0) * numSrcRows;
                    float avgBright = (float)sumBright / pixelCount;
                    bool isBlack = avgBright <= threshold;

                    if (isBlack)
                    {
                        if (runStart < 0) runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        rects.Add(MakeRect(runStart, x, y, rectMin, pxWorld, pyWorld));
                        runStart = -1;
                    }
                }

                if (runStart >= 0)
                    rects.Add(MakeRect(runStart, res, y, rectMin, pxWorld, pyWorld));
            }

            if (rects.Count == 0)
                return new Paths64();

            Paths64 unioned = Clipper.Union(rects, FillRule.NonZero);

            // Chaikin curve subdivision: smooths pixel-stair edges into organic curves.
            // Each iteration cuts every corner to a smooth arc (Q = ¾p0+¼p1, R = ¼p0+¾p1).
            // Use ALL requested iterations for maximum shape fidelity — the user said
            // they don't mind generation taking time, only runtime captures must be fast.
            if (_smoothingIterations > 0)
            {
                unioned = ApplyChaikin(unioned, _smoothingIterations);

                // Light simplify to remove co-linear points Chaikin left behind,
                // but keep all the smooth curve detail intact.
                double epsilon = (rectSize.x / res) * 0.25 * GeometryUtils.Scale;
                unioned = Clipper.SimplifyPaths(unioned, epsilon, true);
            }

            if (_clipToArena && ArenaController.Instance != null)
            {
                Paths64 arena = ArenaController.Instance.CreateArenaPath();
                unioned = Clipper.Intersect(unioned, arena, FillRule.NonZero);
            }

            // NOTE: Do NOT call RemoveHoles here!
            // The white gaps in the texture pattern are legitimate design holes that must be
            // preserved for the pattern to render correctly. RemoveHoles would fill them in
            // and turn the intricate design into a solid blob.

            // Reduce vertex count so every subsequent capture Union is fast.
            // This is the critical step: the pattern can have 10k+ verts after
            // full-quality generation, but runtime captures need ~800 to stay instant.
            unioned = CapVertexCount(unioned, _maxVertexCount);

            return unioned;
        }

        private void GetWorldRect(out Vector2 min, out Vector2 size)
        {
            float worldSize = _worldSize > 0.01f
                ? _worldSize
                : (ArenaController.Instance != null ? ArenaController.Instance.Radius * 2f : 60f);

            size = new Vector2(worldSize, worldSize);
            min = _worldCenter - size * 0.5f;
        }

        // ── Bake serialisation helpers ────────────────────────────────────────

        private static BakedPath[] ToBaked(Paths64 paths)
        {
            if (paths == null || paths.Count == 0) return new BakedPath[0];

            var baked = new BakedPath[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                Path64 p = paths[i];
                var pts = new Vector2[p.Count];
                for (int j = 0; j < p.Count; j++)
                    pts[j] = new Vector2((float)(p[j].X / GeometryUtils.Scale),
                                         (float)(p[j].Y / GeometryUtils.Scale));
                baked[i] = new BakedPath { points = pts };
            }
            return baked;
        }

        private static Paths64 FromBaked(BakedPath[] baked)
        {
            Paths64 result = new Paths64();
            if (baked == null) return result;

            for (int i = 0; i < baked.Length; i++)
            {
                Vector2[] pts = baked[i].points;
                if (pts == null || pts.Length < 3) continue;

                Path64 path = new Path64(pts.Length);
                for (int j = 0; j < pts.Length; j++)
                    path.Add(GeometryUtils.ToPoint64(new Vector3(pts[j].x, pts[j].y, 0f)));
                result.Add(path);
            }
            return result;
        }

        // ── Smoothing ─────────────────────────────────────────────────────────

        /// <summary>
        /// Chaikin curve subdivision applied to every path in a Paths64 set.
        /// Each iteration replaces every edge (p0→p1) with two new points:
        ///   Q = ¾·p0 + ¼·p1   (near start)
        ///   R = ¼·p0 + ¾·p1   (near end)
        /// This converts staircase pixel edges into smooth B-spline-like curves.
        /// </summary>
        private static Paths64 ApplyChaikin(Paths64 paths, int iterations)
        {
            Paths64 result = new Paths64(paths.Count);
            for (int i = 0; i < paths.Count; i++)
                result.Add(ChaikinPath(paths[i], iterations));
            return result;
        }

        private static Path64 ChaikinPath(Path64 path, int iterations)
        {
            for (int iter = 0; iter < iterations; iter++)
            {
                int n = path.Count;
                Path64 smooth = new Path64(n * 2);
                for (int i = 0; i < n; i++)
                {
                    Point64 p0 = path[i];
                    Point64 p1 = path[(i + 1) % n];
                    // Q = 3/4 * p0 + 1/4 * p1
                    smooth.Add(new Point64((3 * p0.X + p1.X) / 4, (3 * p0.Y + p1.Y) / 4));
                    // R = 1/4 * p0 + 3/4 * p1
                    smooth.Add(new Point64((p0.X + 3 * p1.X) / 4, (p0.Y + 3 * p1.Y) / 4));
                }
                path = smooth;
            }
            return path;
        }

        // ── Vertex-count budget ───────────────────────────────────────────────────

        /// <summary>
        /// Reduces total polygon vertices to at most <paramref name="maxTotal"/> using
        /// Clipper.SimplifyPaths with a binary-search over epsilon.
        /// • maxTotal == 0  → returns paths unchanged (no limit).
        /// • Already under budget → returns paths unchanged.
        /// Binary search finds the smallest epsilon that meets the budget,
        /// giving maximum shape fidelity within the vertex limit.
        /// </summary>
        private static Paths64 CapVertexCount(Paths64 paths, int maxTotal)
        {
            if (maxTotal <= 0) return paths;              // 0 = unlimited
            int before = CountVertices(paths);
            if (before <= maxTotal) return paths;         // already fits

            // Binary search for the smallest SimplifyPaths epsilon that brings
            // total vertices at or below maxTotal. This gives the best possible
            // shape quality that fits the budget.
            double lo = 0;
            double hi = GeometryUtils.Scale * 5.0; // 5 world units max tolerance
            Paths64 best = paths;
            int bestVerts = before;

            // 20 iterations of binary search gives ~1e-6 precision — more than enough.
            for (int i = 0; i < 20; i++)
            {
                double mid = (lo + hi) * 0.5;
                Paths64 candidate = Clipper.SimplifyPaths(paths, mid, true);

                // Clean up: fix any self-intersections but KEEP holes (they are part of the pattern design)
                candidate = Clipper.Union(candidate, FillRule.NonZero);

                if (candidate == null || candidate.Count == 0)
                {
                    hi = mid; // too aggressive, back off
                    continue;
                }

                int verts = CountVertices(candidate);

                if (verts <= maxTotal)
                {
                    // Fits budget — save it and try for better quality (smaller epsilon)
                    best = candidate;
                    bestVerts = verts;
                    hi = mid;
                }
                else
                {
                    // Too many vertices — need more simplification
                    lo = mid;
                }
            }

            if (bestVerts != before)
                Debug.Log($"TexturePatternTerritory: vertex cap {maxTotal} → reduced {before} → {bestVerts} vertices.");

            return best;
        }

        private static int CountVertices(Paths64 paths)
        {
            int total = 0;
            for (int i = 0; i < paths.Count; i++)
                total += paths[i].Count;
            return total;
        }

        // ── Geometry helpers ──────────────────────────────────────────────────

        private static Path64 MakeRect(int xStart, int xEnd, int y,
                                        Vector2 rectMin, float pxW, float pyH)
        {
            float x0 = rectMin.x + xStart * pxW;
            float x1 = rectMin.x + xEnd * pxW;
            float y0 = rectMin.y + y * pyH;
            float y1 = rectMin.y + (y + 1) * pyH;

            Path64 r = new Path64(4);
            r.Add(GeometryUtils.ToPoint64(new Vector3(x0, y0, 0f)));
            r.Add(GeometryUtils.ToPoint64(new Vector3(x1, y0, 0f)));
            r.Add(GeometryUtils.ToPoint64(new Vector3(x1, y1, 0f)));
            r.Add(GeometryUtils.ToPoint64(new Vector3(x0, y1, 0f)));
            return r;
        }

        private void OnDrawGizmosSelected()
        {
            GetWorldRect(out Vector2 min, out Vector2 size);
            Vector3 centre = new Vector3(min.x + size.x * 0.5f, min.y + size.y * 0.5f, 0f);
            Gizmos.color = new Color(1f, 0.45f, 0f, 0.7f);
            Gizmos.DrawWireCube(centre, new Vector3(size.x, size.y, 0.01f));
        }
    }
}