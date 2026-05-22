using DG.Tweening;
using UnityEngine;

namespace UI
{
    public class EndScreenView : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private RectTransform _background;
        [SerializeField] private RectTransform _skin;
        [SerializeField] private CanvasGroup _textGroup;
        [SerializeField] private RectTransform _textRect;
        [SerializeField] private CanvasGroup _buttonGroup;
        [SerializeField] private RectTransform _buttonRect;

        [Header("Timing")]
        [SerializeField] private float _backgroundDuration = 0.40f;
        [SerializeField] private float _skinPunchStrength = 0.20f;
        [SerializeField] private float _skinPunchDuration = 0.35f;
        [SerializeField] private float _skinPunchDelay = 0.20f;
        [SerializeField] private float _textDuration = 0.55f;
        [SerializeField] private float _buttonDuration = 0.65f;
        [SerializeField] private float _slideOffset = 40f;

        [Header("Base Positions")]
        [SerializeField] private float _textBaseY;
        [SerializeField] private float _buttonBaseY;

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
            _background
                .DOScale(1f, _backgroundDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);

            _skin
                .DOPunchScale(Vector3.one * _skinPunchStrength, _skinPunchDuration, 4, 0.6f)
                .SetDelay(_skinPunchDelay)
                .SetUpdate(true);

            _textGroup.DOFade(1f, _textDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            _textRect
                .DOAnchorPosY(_textBaseY, _textDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

            _buttonGroup.DOFade(1f, _buttonDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            _buttonRect
                .DOAnchorPosY(_buttonBaseY, _buttonDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        private void ResetToHidden()
        {
            _background.localScale = Vector3.zero;

            _skin.localScale = Vector3.one;

            _textGroup.alpha = 0f;
            Vector2 textPos = _textRect.anchoredPosition;
            textPos.y = _textBaseY - _slideOffset;
            _textRect.anchoredPosition = textPos;

            _buttonGroup.alpha = 0f;
            Vector2 buttonPos = _buttonRect.anchoredPosition;
            buttonPos.y = _buttonBaseY - _slideOffset;
            _buttonRect.anchoredPosition = buttonPos;
        }

        private void KillAllTweens()
        {
            _background.DOKill();
            _skin.DOKill();
            _textGroup.DOKill();
            _textRect.DOKill();
            _buttonGroup.DOKill();
            _buttonRect.DOKill();
        }
    }
}
