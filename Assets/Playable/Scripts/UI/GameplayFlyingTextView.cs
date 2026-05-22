using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace UI
{
    public enum GameplayFlyingTextType : int
    {
        PercentageGain = 0,
        Kill = 1
    }

    public class GameplayFlyingTextView : MonoBehaviour
    {
        // KillIcon sprite asset has a single sprite (KILL) at index 0.
        private const string KillText = "<cspace=-0.2em><size=70%>+</size><size=110%><sprite index=0></size>";
        private const string PercentageGainText = "<size=70%>+</size><size=90%>{0:F2}%</size>";

        private Sequence _moveSequence;
        private float _targetY = 200;

        [SerializeField] private TextMeshProUGUI _text;

        public bool IsRunning => _text.enabled;

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }

        public Sequence SetupAndAnimate(GameplayFlyingTextType type, float value, Color color, Vector3 origin)
        {
            transform.position = origin;
            _targetY = _text.rectTransform.anchoredPosition.y + 200;

            _text.color = color;
            _text.alpha = 0;
            _text.enabled = true;
            _text.transform.localScale = Vector3.zero;

            if (_moveSequence != null)
            {
                _moveSequence.Kill();
                _moveSequence = null;
            }

            gameObject.SetActive(true);
            // Newest toast draws on top of any older toasts still mid-animation.
            transform.SetAsLastSibling();

            switch (type)
            {
                case GameplayFlyingTextType.PercentageGain:
                    _text.text = string.Format(CultureInfo.InvariantCulture, PercentageGainText, value);
                    break;
                case GameplayFlyingTextType.Kill:
                    _text.text = KillText;
                    break;
            }

            _moveSequence = DOTween.Sequence()
                .Append(_text.rectTransform.DOAnchorPosY(_targetY, 1.5f))
                .Join(_text.transform.DOScale(Vector3.one, .3f).SetEase(Ease.OutBack))
                .Join(_text.DOFade(1, .2f))
                .Join(_text.DOFade(0, 1.3f).SetDelay(.2f))
                .OnComplete(HandleToastAnimCompleted);

            return _moveSequence;
        }

        public void Stop()
        {
            KillTweens();
            gameObject.SetActive(false);
        }

        private void HandleToastAnimCompleted()
        {
            _moveSequence = null;
            DisableText();
            gameObject.SetActive(false);
        }

        private void KillTweens()
        {
            if (_moveSequence != null)
            {
                _moveSequence.Kill();
                _moveSequence = null;
            }

            DisableText();
        }

        private void DisableText()
        {
            if (_text != null)
            {
                _text.enabled = false;
                _text.alpha = 0;
            }
        }
    }
}
