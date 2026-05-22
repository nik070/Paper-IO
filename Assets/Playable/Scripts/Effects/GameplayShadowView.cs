using DG.Tweening;
using UnityEngine;

namespace Effects
{
    public class GameplayShadowView : MonoBehaviour
    {
        private static readonly int BigJumpTrigger = Animator.StringToHash("BigJump");
        private static readonly int MidJumpTrigger = Animator.StringToHash("MidJump");

        private const float ShadowAnimationDuration = 0.25f;

        [SerializeField] private MeshRenderer _playgroundShadow;
        [SerializeField] private Animator _animator;

        private bool _isVisible;
        private float _shadowDimension = 2;

        public void Setup(float shadowDimension)
        {
            gameObject.SetActive(true);
            _shadowDimension = shadowDimension;
        }

        public void Show(bool state, bool animate = true)
        {
            if (_isVisible == state)
            {
                return;
            }

            _isVisible = state;

            if (animate == false)
            {
                _playgroundShadow.transform.localScale = Vector3.one * (_isVisible ? _shadowDimension : 0);
            }
            else
            {
                var endpoint = Vector3.one * (_isVisible ? _shadowDimension : 0);
                _playgroundShadow.transform.DOScale(endpoint, ShadowAnimationDuration);
            }
        }

        public void PlayBigJumpAnimation()
        {
            if (_isVisible == false)
            {
                return;
            }

            _animator.SetTrigger(BigJumpTrigger);
        }

        public void PlayMidJumpAnimation()
        {
            if (_isVisible == false)
            {
                return;
            }

            _animator.SetTrigger(MidJumpTrigger);
        }
    }
}
