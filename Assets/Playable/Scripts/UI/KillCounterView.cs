using Core;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace UI
{
    public class KillCounterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _killCounterText;
        [SerializeField] private Transform _killIconTransform;

        public void UpdateCounter()
        {
            _killCounterText.text = $"{GameManager.Instance.KillCount}";
            ShakeAnimation();
        }

        private void ShakeAnimation()
        {
            _killIconTransform.DOKill();

            _killIconTransform.DOScale(Vector3.one * 1.2f, 0.4f).SetEase(Ease.OutSine);
            _killIconTransform.DOPunchRotation(Vector3.forward * 20, .4f).SetEase(Ease.OutSine);
            _killIconTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetDelay(0.4f);
        }
    }
}
