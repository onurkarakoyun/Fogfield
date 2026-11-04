using System;
using System.Collections.Generic;
using UnityEngine;
using AncientTree.Core;

namespace AncientTree.Settings
{
    /// <summary>
    /// Manages audio, language, and display (resolution, fullscreen) settings.
    /// Persists settings in PlayerPrefs and applies them on startup and at runtime.
    /// Supports PC Standalone resolution switching while remaining mobile-friendly.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // --- PlayerPrefs Keys ---
        private const string PrefsKeySfxVolume = "Settings_SfxVolume";
        private const string PrefsKeySfxMute = "Settings_SfxMute";
        private const string PrefsKeyMusicVolume = "Settings_MusicVolume";
        private const string PrefsKeyMusicMute = "Settings_MusicMute";
        private const string PrefsKeyResolutionWidth = "Settings_ResolutionWidth";
        private const string PrefsKeyResolutionHeight = "Settings_ResolutionHeight";
        private const string PrefsKeyFullscreen = "Settings_Fullscreen";
        private const string PrefsKeyScreenShake = "Settings_ScreenShake";
        private const string PrefsKeyReduceMotion = "Settings_ReduceMotion";

        [Header("Audio Settings (0-100)")]
        [Range(0f, 100f)] [SerializeField] private float _sfxVolume = 80f;
        [SerializeField] private bool _sfxMuted = false;
        [Range(0f, 100f)] [SerializeField] private float _musicVolume = 70f;
        [SerializeField] private bool _musicMuted = false;

        [Header("Display Settings (PC Standalone)")]
        [SerializeField] private bool _isFullscreen = true;

        public float SfxVolume => _sfxVolume;
        public bool SfxMuted => _sfxMuted;
        public float MusicVolume => _musicVolume;
        public bool MusicMuted => _musicMuted;

        public Resolution[] UniqueResolutions { get; private set; }
        public int CurrentResolutionIndex { get; private set; } = 0;
        public bool IsFullscreen => _isFullscreen;

        // --- Feel / accessibility ---
        [Header("Feel & Accessibility")]
        [SerializeField] private bool _screenShake = true;
        [SerializeField] private bool _reduceMotion = false;

        /// <summary>Camera shake on impacts. Off = no shake at all.</summary>
        public bool ScreenShake => _screenShake;
        /// <summary>Removes hit-stop and flashes, softens squash/stretch (motion & photosensitivity).</summary>
        public bool ReduceMotion => _reduceMotion;
        public static event Action OnFeelSettingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureFullscreenOnStartup()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            bool isFs = PlayerPrefs.GetInt(PrefsKeyFullscreen, 1) == 1;
            int w = PlayerPrefs.GetInt(PrefsKeyResolutionWidth, Screen.currentResolution.width);
            int h = PlayerPrefs.GetInt(PrefsKeyResolutionHeight, Screen.currentResolution.height);
            FullScreenMode mode = isFs ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(w, h, mode);
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeResolutions();
            LoadSettings();
            ApplyDisplaySettings();
        }

        private void Start()
        {
            ApplyAudioSettings();
        }

        public void InitializeResolutions()
        {
            Resolution[] all = Screen.resolutions;
            var uniqueList = new List<Resolution>();
            var seen = new HashSet<string>();

            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r.width < 800 || r.height < 600) continue;

                string key = $"{r.width}x{r.height}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniqueList.Add(r);
                }
            }

            if (uniqueList.Count == 0)
            {
                uniqueList.Add(Screen.currentResolution);
            }

            // Sort ascending by width, then height
            uniqueList.Sort((a, b) =>
            {
                int comp = a.width.CompareTo(b.width);
                return comp != 0 ? comp : a.height.CompareTo(b.height);
            });

            UniqueResolutions = uniqueList.ToArray();
        }

        public void LoadSettings()
        {
            // Audio
            _sfxVolume = PlayerPrefs.GetFloat(PrefsKeySfxVolume, 80f);
            _sfxMuted = PlayerPrefs.GetInt(PrefsKeySfxMute, 0) == 1;
            _musicVolume = PlayerPrefs.GetFloat(PrefsKeyMusicVolume, 70f);
            _musicMuted = PlayerPrefs.GetInt(PrefsKeyMusicMute, 0) == 1;
            _screenShake = PlayerPrefs.GetInt(PrefsKeyScreenShake, 1) == 1;
            _reduceMotion = PlayerPrefs.GetInt(PrefsKeyReduceMotion, 0) == 1;

            // Display (Defaults: Fullscreen = true, Resolution = native screen resolution)
            _isFullscreen = PlayerPrefs.GetInt(PrefsKeyFullscreen, 1) == 1;

            int targetW = PlayerPrefs.GetInt(PrefsKeyResolutionWidth, Screen.currentResolution.width);
            int targetH = PlayerPrefs.GetInt(PrefsKeyResolutionHeight, Screen.currentResolution.height);

            CurrentResolutionIndex = FindResolutionIndex(targetW, targetH);
        }

        private int FindResolutionIndex(int width, int height)
        {
            if (UniqueResolutions == null || UniqueResolutions.Length == 0) return 0;

            for (int i = 0; i < UniqueResolutions.Length; i++)
            {
                if (UniqueResolutions[i].width == width && UniqueResolutions[i].height == height)
                {
                    return i;
                }
            }

            // Fallback: closest match or highest resolution
            return UniqueResolutions.Length - 1;
        }

        public void SetResolution(int index)
        {
            if (UniqueResolutions == null || index < 0 || index >= UniqueResolutions.Length) return;

            CurrentResolutionIndex = index;
            var res = UniqueResolutions[index];

            PlayerPrefs.SetInt(PrefsKeyResolutionWidth, res.width);
            PlayerPrefs.SetInt(PrefsKeyResolutionHeight, res.height);
            PlayerPrefs.Save();

            ApplyDisplaySettings();
        }

        public void SetFullscreen(bool isFullscreen)
        {
            _isFullscreen = isFullscreen;
            PlayerPrefs.SetInt(PrefsKeyFullscreen, _isFullscreen ? 1 : 0);
            PlayerPrefs.Save();

            ApplyDisplaySettings();
        }

        public void ApplyDisplaySettings()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            if (UniqueResolutions != null && CurrentResolutionIndex >= 0 && CurrentResolutionIndex < UniqueResolutions.Length)
            {
                var res = UniqueResolutions[CurrentResolutionIndex];
                FullScreenMode mode = _isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Screen.SetResolution(res.width, res.height, mode);
            }
            else
            {
                FullScreenMode mode = _isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, mode);
            }
#endif
        }

        // --- Audio Methods ---
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

        public void SetScreenShake(bool enabled)
        {
            _screenShake = enabled;
            PlayerPrefs.SetInt(PrefsKeyScreenShake, enabled ? 1 : 0);
            PlayerPrefs.Save();
            OnFeelSettingsChanged?.Invoke();
        }

        public void SetReduceMotion(bool enabled)
        {
            _reduceMotion = enabled;
            PlayerPrefs.SetInt(PrefsKeyReduceMotion, enabled ? 1 : 0);
            PlayerPrefs.Save();
            OnFeelSettingsChanged?.Invoke();
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
