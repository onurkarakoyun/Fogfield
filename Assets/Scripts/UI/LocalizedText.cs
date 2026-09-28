using UnityEngine;
using TMPro;
using UnityEngine.UI;
using AncientTree.Core;
using AncientTree.Settings;

namespace AncientTree.UI
{
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _localizationKey;

        private TextMeshProUGUI _tmpText;
        private Text _legacyText;

        public string LocalizationKey
        {
            get => _localizationKey;
            set
            {
                _localizationKey = value;
                UpdateText();
            }
        }

        private void Awake()
        {
            _tmpText = GetComponent<TextMeshProUGUI>();
            _legacyText = GetComponent<Text>();
        }

        private void Start()
        {
            UpdateText();
        }

        private void OnEnable()
        {
            GameEvents.OnLanguageChanged += HandleLanguageChanged;
            UpdateText();
        }

        private void OnDisable()
        {
            GameEvents.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged(string lang)
        {
            UpdateText();
        }

        public void UpdateText()
        {
            if (string.IsNullOrEmpty(_localizationKey)) return;
            string text = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(_localizationKey) : _localizationKey;

            if (_tmpText != null) _tmpText.text = text;
            else if (_legacyText != null) _legacyText.text = text;
        }
    }
}
