using UnityEngine;
using AncientTree.Core;

namespace AncientTree.Settings
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string PrefsKeySfxVolume = "Settings_SfxVolume";
        private const string PrefsKeySfxMute = "Settings_SfxMute";
        private const string PrefsKeyMusicVolume = "Settings_MusicVolume";
        private const string PrefsKeyMusicMute = "Settings_MusicMute";

        [Header("Audio Settings (0-100)")]
        [Range(0f, 100f)] [SerializeField] private float _sfxVolume = 80f;
        [SerializeField] private bool _sfxMuted = false;
        [Range(0f, 100f)] [SerializeField] private float _musicVolume = 70f;
        [SerializeField] private bool _musicMuted = false;

        public float SfxVolume => _sfxVolume;
        public bool SfxMuted => _sfxMuted;
        public float MusicVolume => _musicVolume;
        public bool MusicMuted => _musicMuted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadSettings();
        }

        private void Start()
        {
            ApplyAudioSettings();
        }

        public void LoadSettings()
        {
            _sfxVolume = PlayerPrefs.GetFloat(PrefsKeySfxVolume, 80f);
            _sfxMuted = PlayerPrefs.GetInt(PrefsKeySfxMute, 0) == 1;
            _musicVolume = PlayerPrefs.GetFloat(PrefsKeyMusicVolume, 70f);
            _musicMuted = PlayerPrefs.GetInt(PrefsKeyMusicMute, 0) == 1;
        }

        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp(volume, 0f, 100f);
            PlayerPrefs.SetFloat(PrefsKeySfxVolume, _sfxVolume);
            PlayerPrefs.Save();
            ApplyAudioSettings();
        }

        public void SetSfxMute(bool isMuted)
        {
            _sfxMuted = isMuted;
            PlayerPrefs.SetInt(PrefsKeySfxMute, _sfxMuted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyAudioSettings();
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp(volume, 0f, 100f);
            PlayerPrefs.SetFloat(PrefsKeyMusicVolume, _musicVolume);
            PlayerPrefs.Save();
            ApplyAudioSettings();
        }

        public void SetMusicMute(bool isMuted)
        {
            _musicMuted = isMuted;
            PlayerPrefs.SetInt(PrefsKeyMusicMute, _musicMuted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyAudioSettings();
        }

        public void SetLanguage(string lang)
        {
            LocalizationManager.Instance?.SetLanguage(lang);
        }

        private void ApplyAudioSettings()
        {
            GameEvents.TriggerAudioSettingsUpdated(_sfxVolume, _sfxMuted, _musicVolume, _musicMuted);
        }
    }
}
