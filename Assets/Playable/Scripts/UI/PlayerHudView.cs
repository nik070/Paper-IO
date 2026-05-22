using Core;
using TMPro;
using UnityEngine;

namespace UI
{
    public class PlayerHudView : MonoBehaviour
    {
        private Canvas _canvas;
        private Transform _cameraTransform;

        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private FollowTarget _followTarget;

        public FollowTarget FollowTarget => _followTarget;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        private void LateUpdate()
        {
            if (_cameraTransform != null)
            {
                transform.forward = _cameraTransform.forward;
            }
        }

        public void Setup(Transform target, string displayName, Color nameColor)
        {
            Camera mainCamera = Camera.main;
            _cameraTransform = mainCamera != null ? mainCamera.transform : null;

            if (_canvas != null && mainCamera != null)
            {
                _canvas.worldCamera = mainCamera;
            }

            _nameText.text = displayName;
            _nameText.color = nameColor;

            transform.SetParent(null, true);
            _followTarget.references.target = target;
            transform.position = target.position + _followTarget.references.offset;
        }
    }
}
