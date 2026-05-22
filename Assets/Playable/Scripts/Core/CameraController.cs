using UnityEngine;

namespace Core
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private FollowTarget _followTarget;
        [SerializeField] private GameObject _background;

        public Camera Camera => _camera;
        public FollowTarget FollowTarget => _followTarget;
        public GameObject Background => _background;

        public void Enable()
        {
            _camera.enabled = true;
        }

        public void Disable()
        {
            _camera.enabled = false;
        }
    }
}
