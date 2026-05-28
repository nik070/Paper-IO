using UnityEditor;
using UnityEngine;
using Core;

namespace CoreEditor
{
    /// <summary>
    /// Scene-view tool for placing bot spawn positions on the LevelManager.
    ///
    /// Usage:
    ///   1. Select the GameObject that has LevelManager on it.
    ///   2. Press M (in the Scene view) to enter Bot Placement Mode.
    ///   3. Move the mouse over the arena — green disc = valid ground, red = outside.
    ///   4. Left-click to place a bot spawn point.
    ///   5. Press M or Escape to exit placement mode.
    ///   6. Right-click the last bot entry in the inspector → Remove to delete it,
    ///      or click "Clear All Bot Positions" in the inspector panel below.
    /// </summary>
    [CustomEditor(typeof(LevelManager))]
    public class LevelManagerEditor : Editor
    {
        private bool   _placementMode;
        private Vector3 _previewPos;
        private bool    _previewValid;

        private static readonly Color ColValid    = new Color(0.15f, 1f,   0.15f, 0.85f);
        private static readonly Color ColInvalid  = new Color(1f,   0.15f, 0.15f, 0.85f);
        private static readonly Color ColLabel    = new Color(1f,   0.55f, 0.05f, 1f);
        private static readonly Color ColPlayerLbl= new Color(0.3f, 0.7f,  1f,   1f);

        private const float GizmoRadius    = 0.45f;
        private const float DiscRadius     = 0.60f;

        // ── Inspector GUI ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Bot Spawn Placement", EditorStyles.boldLabel);

            // Placement toggle button
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = _placementMode ? new Color(0.4f, 1f, 0.4f) : Color.white;
            string btnLabel = _placementMode
                ? "✦ Placement Mode ACTIVE  (M or ESC to exit)"
                : "☉  Enter Placement Mode  (press M in Scene view)";
            if (GUILayout.Button(btnLabel, GUILayout.Height(26)))
            {
                _placementMode = !_placementMode;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prev;

            // Clear button
            EditorGUILayout.Space(2);
            if (GUILayout.Button("✕  Clear All Bot Spawn Positions"))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear Bot Positions",
                    "This will remove ALL bot spawn positions and reset the bot count to 0.\nUndo is supported.",
                    "Clear", "Cancel"))
                {
                    Undo.RecordObject(target, "Clear Bot Spawn Positions");
                    serializedObject.FindProperty("_botSpawnPositions").ClearArray();
                    serializedObject.FindProperty("_botSkinIndices").ClearArray();
                    serializedObject.FindProperty("_botCount").intValue = 0;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        // ── Scene GUI ─────────────────────────────────────────────────────────

        private void OnSceneGUI()
        {
            Event e = Event.current;

            // ── M key toggles placement mode ──────────────────────────────────
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.M)
            {
                _placementMode = !_placementMode;
                e.Use();
                Repaint();
            }

            // ── Draw labels for all existing bot positions ────────────────────
            DrawExistingPositionLabels();

            if (!_placementMode)
                return;

            // Prevent scene-view from acting on clicks while we're placing
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            // ── Escape exits placement mode ───────────────────────────────────
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _placementMode = false;
                e.Use();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            // ── Project mouse ray onto the Z = 0 arena plane ─────────────────
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Mathf.Abs(ray.direction.z) > 0.0001f)
            {
                float   t        = -ray.origin.z / ray.direction.z;
                Vector3 hit      = ray.origin + t * ray.direction;
                hit.z            = 0f;
                _previewPos      = hit;
                _previewValid    = IsInsideArena(hit);
            }

            // ── Screen-space overlay hint ─────────────────────────────────────
            Handles.BeginGUI();
            DrawScreenHint(_previewValid);
            Handles.EndGUI();

            // ── World-space preview disc ──────────────────────────────────────
            float spawnRadius  = serializedObject.FindProperty("_botSpawnRadius").floatValue;
            bool  wouldOverlap = _previewValid && OverlapsAny(_previewPos, spawnRadius, serializedObject);

            Color previewColor = !_previewValid  ? ColInvalid
                               : wouldOverlap    ? new Color(1f, 0.85f, 0f, 0.9f)  // yellow = overlap warning
                               :                   ColValid;

            Handles.color = previewColor;
            Handles.DrawSolidDisc(_previewPos, Vector3.forward, DiscRadius * 0.45f);
            Handles.DrawWireDisc (_previewPos, Vector3.forward, DiscRadius);

            // Territory radius ring preview
            if (_previewValid)
            {
                Handles.color = new Color(previewColor.r, previewColor.g, previewColor.b, 0.30f);
                Handles.DrawWireDisc(_previewPos, Vector3.forward, spawnRadius);

                SerializedProperty positions = serializedObject.FindProperty("_botSpawnPositions");
                int nextIndex = positions.arraySize + 1;
                string lbl = wouldOverlap ? $"Bot {nextIndex}  ⚠ overlap" : $"Bot {nextIndex}";
                GUIStyle previewStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = previewColor } };
                Handles.Label(_previewPos + Vector3.up * (spawnRadius + 0.5f), lbl, previewStyle);
            }
            else
            {
                GUIStyle badStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColInvalid } };
                Handles.Label(_previewPos + Vector3.up * 0.9f, "Outside arena", badStyle);
            }

            // ── Left-click on valid ground → add position ─────────────────────
            if (e.type == EventType.MouseDown && e.button == 0 && _previewValid)
            {
                AddBotPosition(_previewPos);
                e.Use();
            }

            // Keep scene repainting so the preview disc follows the mouse
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
                SceneView.RepaintAll();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void DrawExistingPositionLabels()
        {
            SerializedProperty positions = serializedObject.FindProperty("_botSpawnPositions");
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColLabel },
                fontSize = 11
            };

            for (int i = 0; i < positions.arraySize; i++)
            {
                Vector2 p2  = positions.GetArrayElementAtIndex(i).vector2Value;
                Vector3 pos = new Vector3(p2.x, p2.y, 0f);

                Handles.color = ColLabel;
                Handles.DrawWireDisc(pos, Vector3.forward, GizmoRadius + 0.05f);
                Handles.Label(pos + Vector3.up * (GizmoRadius + 0.45f), $"Bot {i + 1}", labelStyle);
            }

            // Player label
            SerializedProperty spawnProp = serializedObject.FindProperty("_playerSpawnPos");
            Vector2 sp = spawnProp.vector2Value;
            Vector3 spawnPos = new Vector3(sp.x, sp.y, 0f);
            GUIStyle playerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColPlayerLbl },
                fontSize = 11
            };
            Handles.color = ColPlayerLbl;
            Handles.DrawWireDisc(spawnPos, Vector3.forward, GizmoRadius + 0.05f);
            Handles.Label(spawnPos + Vector3.up * (GizmoRadius + 0.45f), "Player", playerStyle);
        }

        private void AddBotPosition(Vector3 worldPos)
        {
            Undo.RecordObject(target, "Add Bot Spawn Position");

            SerializedProperty positions = serializedObject.FindProperty("_botSpawnPositions");
            SerializedProperty skins     = serializedObject.FindProperty("_botSkinIndices");
            SerializedProperty count     = serializedObject.FindProperty("_botCount");

            int idx = positions.arraySize;

            // Insert spawn position
            positions.InsertArrayElementAtIndex(idx);
            positions.GetArrayElementAtIndex(idx).vector2Value = new Vector2(worldPos.x, worldPos.y);

            // Insert default skin index (reuse last skin if possible, otherwise 0)
            int defaultSkin = skins.arraySize > 0
                ? skins.GetArrayElementAtIndex(skins.arraySize - 1).intValue
                : 0;
            skins.InsertArrayElementAtIndex(skins.arraySize);
            skins.GetArrayElementAtIndex(skins.arraySize - 1).intValue = defaultSkin;

            // Keep _botCount in sync
            if (count.intValue < positions.arraySize)
                count.intValue = positions.arraySize;

            serializedObject.ApplyModifiedProperties();

            Debug.Log($"LevelManager: added Bot {idx + 1} spawn at ({worldPos.x:F2}, {worldPos.y:F2}). " +
                      $"Total bot positions: {positions.arraySize}");
        }

        private static bool IsInsideArena(Vector3 pos)
        {
            ArenaController arena = FindObjectOfType<ArenaController>();
            float radius = arena != null ? arena.Radius : 30f;
            return pos.x * pos.x + pos.y * pos.y <= radius * radius;
        }

        /// <summary>
        /// Returns true if <paramref name="candidate"/> is within <paramref name="spawnRadius"/> * 2
        /// of any existing bot spawn position or the player spawn position.
        /// </summary>
        private static bool OverlapsAny(Vector3 candidate, float spawnRadius, SerializedObject so)
        {
            float minDist = spawnRadius * 2f;
            Vector2 c2 = new Vector2(candidate.x, candidate.y);

            // Check against all existing bot positions
            SerializedProperty positions = so.FindProperty("_botSpawnPositions");
            for (int i = 0; i < positions.arraySize; i++)
            {
                if (Vector2.Distance(c2, positions.GetArrayElementAtIndex(i).vector2Value) < minDist)
                    return true;
            }

            // Check against player spawn
            Vector2 playerSpawn = so.FindProperty("_playerSpawnPos").vector2Value;
            if (Vector2.Distance(c2, playerSpawn) < minDist)
                return true;

            return false;
        }

        private static void DrawScreenHint(bool valid)
        {
            string msg = valid
                ? "Bot Placement Mode  |  Left-click to place  |  M or ESC to exit"
                : "Bot Placement Mode  |  Click inside the arena circle  |  M or ESC to exit";

            Color bg  = valid ? new Color(0f, 0.25f, 0f, 0.82f) : new Color(0.25f, 0f, 0f, 0.82f);
            Color txt = valid ? new Color(0.7f, 1f, 0.7f)        : new Color(1f, 0.7f, 0.7f);

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)   { fontSize = 0 };
            GUIStyle lblStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = txt }
            };

            Rect rect = new Rect(6, 38, 410, 26);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = bg;
            GUI.Box(rect, GUIContent.none, boxStyle);
            GUI.backgroundColor = prevBg;
            GUI.Label(rect, msg, lblStyle);
        }
    }
}
