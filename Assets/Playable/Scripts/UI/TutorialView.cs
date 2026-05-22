using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace UI
{
    public class TutorialView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _tutorialTitle;
        [SerializeField] private TMP_Text _tutorialSubtitle;
        [SerializeField] private RectTransform _tutorialHand;
        [SerializeField] private float _circleRadius = 105f;
        [SerializeField] private float _circleDuration = 4f;

        private Tweener _handTween;
        private Tweener _titleBounce;

        public void StartTutorialAnimations()
        {
            if (_tutorialHand != null)
            {
                Vector2 startPos = _tutorialHand.anchoredPosition;
                Vector2 circleCenter = new Vector2(startPos.x, startPos.y + _circleRadius);

                float angle = 0f;
                _handTween = DOTween.To(() => angle, x =>
                {
                    angle = x;
                    _tutorialHand.anchoredPosition = new Vector2(
                        circleCenter.x - _circleRadius * Mathf.Sin(angle),
                        circleCenter.y - _circleRadius * Mathf.Cos(angle));
                }, -Mathf.PI * 2f, _circleDuration)
                    .SetLoops(-1, LoopType.Restart)
                    .SetEase(Ease.Linear);
            }

            if (_tutorialTitle != null)
            {
                _titleBounce = _tutorialTitle.rectTransform
                    .DOScale(1.05f, 0.6f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        public void StopTutorialAnimations()
        {
            _handTween?.Kill();
            _titleBounce?.Kill();
            _handTween = null;
            _titleBounce = null;
        }

    }
}
