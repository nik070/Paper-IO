using DG.Tweening;
using UnityEngine;

namespace UI
{
    public class WinScreenView : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private CanvasGroup _contentGroup;
        [SerializeField] private RectTransform _titleRect;
        [SerializeField] private RectTransform _crownRect;
        [SerializeField] private RectTransform _subtitleRect;
        [SerializeField] private RectTransform _shineRect;
        [SerializeField] private CanvasGroup _shineGroup;

        [Header("Timing")]
        [SerializeField] private float _contentFadeDuration = 0.45f;
        [SerializeField] private float _titleDuration = 0.50f;
        [SerializeField] private float _titleStartScale = 0.5f;
        [SerializeField] private float _crownDuration = 0.55f;
        [SerializeField] private float _crownDropOffset = 250f;
        [SerializeField] private float _crownPunchAngle = 15f;
        [SerializeField] private float _crownPunchDuration = 0.4f;
        [SerializeField] private float _subtitleDuration = 0.55f;
        [SerializeField] private float _subtitleSlideOffset = 30f;
        [SerializeField] private float _shineDuration = 0.60f;
        [SerializeField] private float _shineRotationDuration = 8f;

        [Header("Base Positions")]
        [SerializeField] private float _crownBaseY = 306f;
        [SerializeField] private float _subtitleBaseY = -45f;

        private void Awake()
        {
            ResetToHidden();
        }

        private void OnDisable()
        {
            KillAllTweens();
        }

        public void Show()
        {
            KillAllTweens();
            ResetToHidden();
            PlayShowTweens();
        }

        private void PlayShowTweens()
        {
            _contentGroup
                .DOFade(1f, _contentFadeDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

            _titleRect
                .DOScale(1f, _titleDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);

            Sequence crownSequence = DOTween.Sequence().SetUpdate(true);
            crownSequence.SetTarget(_crownRect);
            crownSequence.Append(_crownRect.DOAnchorPosY(_crownBaseY, _crownDuration).SetEase(Ease.OutBack));
            crownSequence.Append(_crownRect.DOPunchRotation(new Vector3(0f, 0f, _crownPunchAngle), _crownPunchDuration, 6, 0.6f));

            _subtitleRect
                .DOAnchorPosY(_subtitleBaseY, _subtitleDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

            _shineGroup.DOFade(1f, _shineDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            _shineRect
                .DOScale(1f, _shineDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);

            _shineRect
                .DORotate(new Vector3(0f, 0f, -360f), _shineRotationDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental)
                .SetUpdate(true);
        }

        private void ResetToHidden()
        {
            _contentGroup.alpha = 0f;

            _titleRect.localScale = Vector3.one * _titleStartScale;

            Vector2 crownPos = _crownRect.anchoredPosition;
            crownPos.y = _crownBaseY + _crownDropOffset;
            _crownRect.anchoredPosition = crownPos;

            Vector2 subtitlePos = _subtitleRect.anchoredPosition;
            subtitlePos.y = _subtitleBaseY - _subtitleSlideOffset;
            _subtitleRect.anchoredPosition = subtitlePos;

            _shineGroup.alpha = 0f;
            _shineRect.localScale = Vector3.zero;
            _shineRect.localRotation = Quaternion.identity;
        }

        private void KillAllTweens()
        {
            _contentGroup.DOKill();
            _titleRect.DOKill();
            _crownRect.DOKill();
            _subtitleRect.DOKill();
            _shineRect.DOKill();
            _shineGroup.DOKill();
        }
    }
}
