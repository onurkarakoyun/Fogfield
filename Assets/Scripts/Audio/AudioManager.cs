using System;
using UnityEngine;
using AncientTree.Core;

namespace AncientTree.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource _sfxSource;
        private AudioSource _musicSource;

        private float _sfxVolume = 0.8f;
        private bool _sfxMuted = false;
        private float _musicVolume = 0.7f;
        private bool _musicMuted = false;

        private AudioClip _clipClick;
        private AudioClip _clipDiceRoll;
        private AudioClip _clipDiceLand;
        private AudioClip _clipJump;
        private AudioClip _clipFlip;
        private AudioClip _clipMountain;
        private AudioClip _clipBuild;
        private AudioClip _clipVictory;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;

            GenerateProceduralAudioClips();
        }

        private void OnEnable()
        {
            GameEvents.OnAudioSettingsUpdated += HandleAudioSettingsUpdated;
            GameEvents.OnDiceRolled += HandleDiceRolled;
            GameEvents.OnMountainHit += HandleMountainHit;
            GameEvents.OnSettlementBuilt += HandleSettlementBuilt;
            GameEvents.OnVictory += HandleVictory;
        }

        private void OnDisable()
        {
            GameEvents.OnAudioSettingsUpdated -= HandleAudioSettingsUpdated;
            GameEvents.OnDiceRolled -= HandleDiceRolled;
            GameEvents.OnMountainHit -= HandleMountainHit;
            GameEvents.OnSettlementBuilt -= HandleSettlementBuilt;
            GameEvents.OnVictory -= HandleVictory;
        }

        private void Start()
        {
            if (Settings.SettingsManager.Instance != null)
            {
                HandleAudioSettingsUpdated(
                    Settings.SettingsManager.Instance.SfxVolume,
                    Settings.SettingsManager.Instance.SfxMuted,
                    Settings.SettingsManager.Instance.MusicVolume,
                    Settings.SettingsManager.Instance.MusicMuted
                );
            }
            StartAmbientMusic();
        }

        private void HandleAudioSettingsUpdated(float sfxVol, bool sfxMute, float musicVol, bool musicMute)
        {
            _sfxVolume = Mathf.Clamp01(sfxVol / 100f);
            _sfxMuted = sfxMute;
            _musicVolume = Mathf.Clamp01(musicVol / 100f);
            _musicMuted = musicMute;

            if (_sfxSource != null) _sfxSource.volume = _sfxMuted ? 0f : _sfxVolume;
            if (_musicSource != null) _musicSource.volume = _musicMuted ? 0f : _musicVolume * 0.35f;
        }

        private void HandleDiceRolled(int val) => PlayDiceRoll();
        private void HandleMountainHit(Vector2Int coords, int player) => PlayMountainBonk();
        private void HandleSettlementBuilt(Vector2Int coords, int player, int points) => PlayBuildSettlement();
        private void HandleVictory(int p, string name, Color c, int s) => PlayVictoryFanfare();

        public void PlayClick() => PlaySfx(_clipClick, 0.6f);
        public void PlayDiceRoll() => PlaySfx(_clipDiceRoll, 0.85f);
        public void PlayDiceLand() => PlaySfx(_clipDiceLand, UnityEngine.Random.Range(0.95f, 1.05f));
        public void PlayJump() => PlaySfx(_clipJump, 0.7f);
        public void PlayTileFlip() => PlaySfx(_clipFlip, 0.9f);
        public void PlayMountainBonk() => PlaySfx(_clipMountain, 1.0f);
        public void PlayBuildSettlement() => PlaySfx(_clipBuild, 0.9f);
        public void PlayVictoryFanfare() => PlaySfx(_clipVictory, 1.0f);
        public void PlayCountdownTick(int second) => PlaySfx(_clipClick, 0.75f + (4 - Mathf.Clamp(second, 1, 3)) * 0.25f);
        public void PlayCountdownStart() => PlaySfx(_clipVictory, 1.25f);
        public void PlayScoreGain() => PlaySfx(_clipBuild, 1.35f);

        private void PlaySfx(AudioClip clip, float pitch = 1f)
        {
            if (_sfxMuted || clip == null) return;
            _sfxSource.pitch = pitch;
            _sfxSource.PlayOneShot(clip, _sfxVolume);
        }

        private void StartAmbientMusic()
        {
            if (_musicSource == null) return;
            // Generate soothing ambient synth drone
            int sampleRate = 44100;
            float duration = 4.0f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Soft triad: A3 (220Hz), C#4 (277.18Hz), E4 (329.63Hz)
                float wave = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.2f
                           + Mathf.Sin(2f * Mathf.PI * 277.18f * t) * 0.15f
                           + Mathf.Sin(2f * Mathf.PI * 329.63f * t) * 0.15f;
                // Gentle tremolo
                wave *= 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                samples[i] = wave;
            }

            AudioClip musicClip = AudioClip.Create("AmbientTheme", totalSamples, 1, sampleRate, false);
            musicClip.SetData(samples, 0);

            _musicSource.clip = musicClip;
            _musicSource.volume = _musicMuted ? 0f : _musicVolume * 0.25f;
            _musicSource.Play();
        }

        private void GenerateProceduralAudioClips()
        {
            _clipClick = CreateToneClip("Click", 0.05f, 600f, 300f);
            _clipDiceRoll = CreateDiceRollClip("DiceRoll", 0.3f);
            _clipDiceLand = CreateDiceLandClip("DiceLand", 0.18f);
            _clipJump = CreateToneClip("Jump", 0.12f, 320f, 540f);
            _clipFlip = CreateToneClip("Flip", 0.2f, 400f, 750f);
            _clipMountain = CreateToneClip("MountainBonk", 0.25f, 150f, 80f);
            _clipBuild = CreateFanfareClip("Build", 0.35f, new float[] { 440f, 554f, 659f });
            _clipVictory = CreateFanfareClip("Victory", 1.2f, new float[] { 392f, 523f, 659f, 784f });
        }

        private AudioClip CreateDiceLandClip(string name, float duration)
        {
            int sampleRate = 44100;
            int samplesCount = (int)(sampleRate * duration);
            float[] data = new float[samplesCount];
            System.Random rand = new System.Random(1337);
            for (int i = 0; i < samplesCount; i++)
            {
                float t = (float)i / samplesCount;
                float decay = Mathf.Exp(-18f * t);
                float thud = Mathf.Sin(2f * Mathf.PI * (160f * (1f - t * 0.5f)) * ((float)i / sampleRate));
                float clatter = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-28f * t);
                data[i] = (thud * 0.7f + clatter * 0.4f) * decay * 0.8f;
            }
            AudioClip clip = AudioClip.Create(name, samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateToneClip(string name, float duration, float startFreq, float endFreq)
        {
            int sampleRate = 44100;
            int samplesCount = (int)(sampleRate * duration);
            float[] data = new float[samplesCount];

            for (int i = 0; i < samplesCount; i++)
            {
                float t = (float)i / samplesCount;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                float envelope = 1f - t; // Linear decay
                data[i] = Mathf.Sin(2f * Mathf.PI * currentFreq * ((float)i / sampleRate)) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create(name, samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateDiceRollClip(string name, float duration)
        {
            int sampleRate = 44100;
            int samplesCount = (int)(sampleRate * duration);
            float[] data = new float[samplesCount];
            System.Random rand = new System.Random(42);

            for (int i = 0; i < samplesCount; i++)
            {
                float t = (float)i / samplesCount;
                float noise = ((float)rand.NextDouble() * 2f - 1f);
                float clatter = Mathf.Sin(2f * Mathf.PI * 45f * t) > 0.3f ? 1f : 0.2f;
                float envelope = Mathf.Sin(t * Mathf.PI);
                data[i] = noise * clatter * envelope * 0.4f;
            }

            AudioClip clip = AudioClip.Create(name, samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateFanfareClip(string name, float duration, float[] notes)
        {
            int sampleRate = 44100;
            int samplesCount = (int)(sampleRate * duration);
            float[] data = new float[samplesCount];
            int noteCount = notes.Length;
            int samplesPerNote = samplesCount / noteCount;

            for (int i = 0; i < samplesCount; i++)
            {
                int noteIndex = Mathf.Clamp(i / samplesPerNote, 0, noteCount - 1);
                float noteFreq = notes[noteIndex];
                int noteSampleOffset = i % samplesPerNote;
                float noteT = (float)noteSampleOffset / samplesPerNote;
                float envelope = Mathf.Sin(noteT * Mathf.PI) * (1f - (float)i / samplesCount * 0.3f);
                data[i] = Mathf.Sin(2f * Mathf.PI * noteFreq * ((float)i / sampleRate)) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create(name, samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
