using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CTAView : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private void Awake()
        {
            _button.onClick.AddListener(OnButtonClicked);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        public void OnButtonClicked()
        {
            Debug.Log("CTAView: Button clicked");
            Luna.Unity.Playable.InstallFullGame();
        }
    }
}
