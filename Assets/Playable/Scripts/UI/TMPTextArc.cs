using TMPro;
using UnityEngine;

namespace UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public class TMPTextArc : MonoBehaviour
    {
        [SerializeField] private float _arcHeight = 30f;

        private TMP_Text _text;
        private bool _isApplying;

        private void OnEnable()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }

            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Apply();
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        }

        private void OnTextChanged(Object obj)
        {
            if (obj == _text)
            {
                Apply();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                if (_text == null)
                {
                    _text = GetComponent<TMP_Text>();
                }

                Apply();
            };
        }
#endif

        public void Apply()
        {
            if (_text == null || _isApplying)
            {
                return;
            }

            _isApplying = true;
            try
            {
                ApplyInternal();
            }
            finally
            {
                _isApplying = false;
            }
        }

        private void ApplyInternal()
        {
            _text.ForceMeshUpdate();
            TMP_TextInfo info = _text.textInfo;
            int count = info.characterCount;
            if (count == 0)
            {
                return;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible)
                {
                    continue;
                }

                int matIdx = c.materialReferenceIndex;
                int vIdx = c.vertexIndex;
                Vector3[] verts = info.meshInfo[matIdx].vertices;

                for (int v = 0; v < 4; v++)
                {
                    float x = verts[vIdx + v].x;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
            }

            float width = Mathf.Max(0.0001f, maxX - minX);

            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible)
                {
                    continue;
                }

                int matIdx = c.materialReferenceIndex;
                int vIdx = c.vertexIndex;
                Vector3[] verts = info.meshInfo[matIdx].vertices;

                Vector3 center = (verts[vIdx] + verts[vIdx + 2]) * 0.5f;

                float t = (center.x - minX) / width;
                float arc = 1f - Mathf.Pow(2f * t - 1f, 2f);
                float yOffset = arc * _arcHeight;

                // tangent of y = arcHeight * (1 - (2t-1)^2) wrt x
                float slope = -4f * _arcHeight * (2f * t - 1f) / width;
                float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(0f, 0f, angle);

                Vector3 offset = new Vector3(0f, yOffset, 0f);

                for (int v = 0; v < 4; v++)
                {
                    Vector3 local = verts[vIdx + v] - center;
                    verts[vIdx + v] = center + rot * local + offset;
                }
            }

            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                _text.UpdateGeometry(info.meshInfo[m].mesh, m);
            }
        }
    }
}
