#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Core
{
    [CustomEditor(typeof(LevelManager))]
    public class LevelManagerEditor : Editor
    {
        private SerializedProperty _botSpawnPositionsProp;
        private SerializedProperty _botCountProp;
        private SerializedProperty _botSkinIndicesProp;
        private static bool _isAddMode;

        private void OnEnable()
        {
            _botSpawnPositionsProp = serializedObject.FindProperty("_botSpawnPositions");
            _botCountProp = serializedObject.FindProperty("_botCount");
            _botSkinIndicesProp = serializedObject.FindProperty("_botSkinIndices");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Hold M and left-click in the Scene view to add a bot spawn position on the arena ground. " +
                "Spawn positions are stored in _botSpawnPositions and are visualized by LevelManager gizmos.",
                MessageType.Info);

            if (_isAddMode)
            {
                EditorGUILayout.HelpBox("Place mode active: click to add a bot spawn position.", MessageType.Warning);
            }
        }

        private void OnSceneGUI()
        {
            Event evt = Event.current;
            if (evt == null)
            {
                return;
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.M)
            {
                _isAddMode = true;
                SceneView.RepaintAll();
            }
            else if (evt.type == EventType.KeyUp && evt.keyCode == KeyCode.M)
            {
                _isAddMode = false;
                SceneView.RepaintAll();
            }

            if (!_isAddMode)
            {
                return;
            }

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 330, 50));
            GUI.color = Color.yellow;
            GUILayout.Label("LevelManager: Hold M and left-click to add a bot spawn point.", EditorStyles.helpBox);
            GUILayout.EndArea();
            Handles.EndGUI();

            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                if (TryGetGroundPosition(ray, out Vector3 hitPoint))
                {
                    AddBotSpawnPoint(hitPoint);
                    evt.Use();
                }
            }
        }

        private bool TryGetGroundPosition(Ray ray, out Vector3 point)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                point = hit.point;
                return true;
            }

            Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private void AddBotSpawnPoint(Vector3 worldPosition)
        {
            serializedObject.Update();

            int newIndex = _botSpawnPositionsProp.arraySize;
            _botSpawnPositionsProp.InsertArrayElementAtIndex(newIndex);
            _botSpawnPositionsProp.GetArrayElementAtIndex(newIndex).vector2Value =
                new Vector2(worldPosition.x, worldPosition.y);

            if (_botCountProp != null)
            {
                _botCountProp.intValue = Mathf.Max(_botCountProp.intValue, newIndex + 1);
            }

            if (_botSkinIndicesProp != null && _botSkinIndicesProp.arraySize < newIndex + 1)
            {
                int previousSize = _botSkinIndicesProp.arraySize;
                _botSkinIndicesProp.arraySize = newIndex + 1;
                for (int i = previousSize; i < _botSkinIndicesProp.arraySize; i++)
                {
                    _botSkinIndicesProp.GetArrayElementAtIndex(i).intValue = 0;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
#endif