using UnityEngine;
using TMPro;
using AncientTree.Core;
using AncientTree.Settings;

namespace AncientTree.UI
{
    /// <summary>
    /// Attach to any TMP_Text to auto-update text when language changes.
    /// Set LocalizationKey to the translation dictionary key.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Translation dictionary key (e.g. 'BTN_ROLL', 'TILE_SCORES_TITLE')")]
        [SerializeField] private string _key;

        [Tooltip("Optional format args — leave empty for plain text")]
        [SerializeField] private string[] _args;

        private TMP_Text _text;

        /// <summary>
        /// Public property for setting the key at editor/runtime — used by SceneSetupBuilder.
        /// Setting this also triggers a Refresh.
        /// </summary>
        public string LocalizationKey
        {
            get => _key;
            set
            {
                _key = value;
                if (isActiveAndEnabled) Refresh();
            }
        }

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            GameEvents.OnLanguageChanged += OnLanguageChanged;
            LocalizationManager.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnLanguageChanged -= OnLanguageChanged;
            LocalizationManager.OnLanguageChanged -= Refresh;
        }

        private void OnLanguageChanged(string lang) => Refresh();

        public void Refresh()
        {
            if (string.IsNullOrEmpty(_key) || LocalizationManager.Instance == null) return;
            object[] castArgs = _args != null && _args.Length > 0 ? (object[])_args : null;
            _text.text = LocalizationManager.Instance.Get(_key, castArgs);
        }

        public void SetKey(string key, params string[] args)
        {
            _key  = key;
            _args = args;
            Refresh();
        }
    }
}
