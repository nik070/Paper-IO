using System.Threading;
using DG.Tweening;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using Canvas = UnityEngine.Canvas;

namespace UI
{
    public class LevelProgressView : MonoBehaviour
    {
        private const float MaxFillWidth = 868;

        [SerializeField] private PercentageTextView _percentageTextViewRight;
        [SerializeField] private RectTransform _backgroundRect;
        [SerializeField] private RectTransform _fillRect;
        [SerializeField] private RectTransform _particlePosition;
        [SerializeField] private Image _fillBackImage;
        [SerializeField] private Image _fillTopImage;
        [SerializeField] private Image _fillOverlayImage;
        [SerializeField] private Vector2Int _bestMinMaxVisibleRange = new(5, 100);
        [SerializeField] private RectTransform _maxTextRect;

        [SerializeField] private RectTransform _content;
        [SerializeField] private RectTransform _outSocket;
        [SerializeField] private Canvas _canvas;

        private float _startAnchoredY;
        private RectTransform _rectTransform;
        private bool _isShowing = true;
        private float _currentValue;
        private Color _fillBackOriginalColor;
        private Color _fillTopOriginalColor;
        private Color _fillOverlayOriginalColor;
        private Vector2 _percentageTextStartAnchoredPos;

        public Vector3 PostFillPosition => _fillRect.transform.TransformPoint(Vector2.right * _fillRect.sizeDelta.x);
        public Vector3 PostTextPosition => _particlePosition.position;
        public Vector2Int BestMinMaxVisibleRange => _bestMinMaxVisibleRange;
        public float GetValue() => _currentValue;
        
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _startAnchoredY = _rectTransform.anchoredPosition.y;

            _fillBackOriginalColor = _fillBackImage.color;
            _fillTopOriginalColor = _fillTopImage.color;
            _fillOverlayOriginalColor = _fillOverlayImage.color;
            _percentageTextStartAnchoredPos = _percentageTextViewRight.GetComponent<RectTransform>().anchoredPosition;

            Hide(true);
        }


        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F))
            {
                _fillTopImage.fillAmount = 1f;
            }
        }
        public void Show(bool instantly = false)
        {
            if (_isShowing)
            {
                return;
            }

            _rectTransform.DOKill();

            _canvas.enabled = true;

            if (instantly)
            {
                _rectTransform.anchoredPosition = _rectTransform.anchoredPosition.WithY(_startAnchoredY);
            }
            else
            {
                Hide(true);
                _rectTransform.DOAnchorPosY(_startAnchoredY, 0.2f);
            }

            _isShowing = true;
        }

        public void Hide(bool instantly = false)
        {
            if (!_isShowing) return;

            _rectTransform.DOKill();

            if (instantly)
            {
                _canvas.enabled = false;
            }
            else
            {
                _rectTransform.DOMove(_outSocket.position, 0.2f)
                .OnComplete(() =>
                {
                    _canvas.enabled = false;
                });
            }

            _isShowing = false;
        }

        public void SetValue(float f)
        {
            _fillRect.sizeDelta = _fillRect.sizeDelta.WithX(f * MaxFillWidth);
            _percentageTextViewRight.Value = Mathf.RoundToInt(f * 100);
            _currentValue = f;

            UpdateTextClamping();
        }

        private void UpdateTextClamping()
        {
            // First reset anchored position relative to the fill, in case the fill went below the maximum position again
            _percentageTextViewRight.GetComponent<RectTransform>().anchoredPosition = _percentageTextStartAnchoredPos;

            // Clamp the X position of the text so it doesn't go further past the right of the fillbar
            var x = _percentageTextViewRight.transform.position.x;
            x = Mathf.Min(x, _maxTextRect.transform.position.x);
            _percentageTextViewRight.transform.SetX(x);
        }

        public void SetValue(float f, float duration, Ease ease = Ease.Unset, float originalZonePercent = -1f)
        {
            _fillRect.DOKill();

            if (duration <= 0)
            {
                SetValue(f);
            }
            else
            {
                DOVirtual.Float(_currentValue, f, duration, SetValue).SetEase(ease);
            }
        }

        public void SetSkin(SkinConfig skinConfig)
        {
            var color = skinConfig.ZoneTextureColor;

            _fillBackImage.color = color + (_fillBackOriginalColor - _fillTopOriginalColor);

            _fillTopImage.color = color;

            if (skinConfig.OverrideHudSpecularColor)
            {
                _fillOverlayImage.color = skinConfig.HudSpecularColor;
            }
            else
            {
                _fillOverlayImage.color = color + (_fillOverlayOriginalColor - _fillBackOriginalColor);
            }
        }
    }
}
