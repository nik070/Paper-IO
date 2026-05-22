using TMPro;
using UnityEngine;

namespace UI
{
    public class PercentageTextView : MonoBehaviour
    {
        #region Properties

        public string Format
        {
            get => _format;
            set
            {
                _format = value;
                UpdateText();
            }
        }

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                UpdateText();
            }
        }

        public TextMeshProUGUI Text => _textMeshPro;

        #endregion

        #region Fields

        [Tooltip("Example: \"Best: {0}\"")]
        [SerializeField]
        private string _format = "{0}";

        [Tooltip("Value between 0-100")]
        [SerializeField]
        private int _value;

        private TextMeshProUGUI _textMeshPro;
        private bool _isInitialized;

        #endregion

        private void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _textMeshPro = GetComponent<TextMeshProUGUI>();
            UpdateText();
        }

        private void Awake()
        {
            Initialize();
        }

        private void UpdateText()
        {
            Initialize();

            var valueString = $"{_value}<size=80%>%</size>";

            _textMeshPro.text = string.Format(_format, valueString);
        }
    }
}
