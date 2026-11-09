using System.Collections.Generic;
using UnityEngine;
using AncientTree.Core;

namespace AncientTree.Audio
{
    /// <summary>
    /// Game audio hub.
    ///  • Buses: Music / SFX / UI. Settings sliders (0-100) map to dB (-30..0) so the slider feels even.
    ///  • Voice pool: every one-shot gets its own AudioSource, so per-sound pitch never re-pitches other sounds.
    ///  • Variation: several baked takes per sound + random pitch/volume, and per-sound cooldowns (no double clicks).
    ///  • Ducking: important stingers dip the music bus and let it recover smoothly.
    ///  • Adaptive music: calm (menu) → explore (match) → tension (someone near the target score), with hysteresis.
    /// Public Play* API is unchanged so existing callers keep working.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private enum Sfx
        {
            UiClick, UiHover, DiceRoll, DiceLand, Jump, Land, Flip, Mountain, Build, Relocate,
            Score, TurnStart, Round, Victory, Tick
        }

        private enum Bus { Sfx, Ui }

        private class SoundDef
        {
            public AudioClip[] Takes;
            public Bus Bus;
            public float Volume = 1f;
            public float PitchJitter = 0.04f;
            public float VolumeJitter = 0.08f;
            public float Cooldown = 0.03f;
            public float LastPlayed = -10f;
            public int LastTake = -1;
        }

        [Header("Mix (dB)")]
        [SerializeField] private float _masterHeadroomDb = -2f;
        [SerializeField] private float _musicTrimDb = -9f;
        [SerializeField] private float _uiTrimDb = -5f;
        [SerializeField] private float _sliderFloorDb = -30f;

        [Header("Voices")]
        [SerializeField] private int _voiceCount = 16;

        [Header("Adaptive Music")]
        [Tooltip("Enter tension when the leader reaches this fraction of the target score")]
        [SerializeField] private float _tensionEnter = 0.75f;
        [Tooltip("Leave tension when the leader drops below this fraction (hysteresis)")]
        [SerializeField] private float _tensionExit = 0.6f;

        private readonly Dictionary<Sfx, SoundDef> _sounds = new Dictionary<Sfx, SoundDef>();
        private AudioSource[] _voices;
        private float[] _voiceStart;
        private AdaptiveMusic _music;

        private float _sfxGain = 0.5f, _musicGain = 0.3f;
        private bool _sfxMuted, _musicMuted;

        // ducking state (dB, <= 0)
        private float _duckDb, _duckTargetDb;
        private float _duckHoldUntil;
        private const float DuckAttackDbPerSec = 80f;
        private const float DuckReleaseDbPerSec = 14f;

        private bool _inTension;
        private bool _inGameplay;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _voices = new AudioSource[_voiceCount];
            _voiceStart = new float[_voiceCount];
            for (int i = 0; i < _voiceCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _voices[i] = src;
            }

            BakeSounds();
            _music = new AdaptiveMusic(gameObject);
        }

        private void OnEnable()
        {
            GameEvents.OnAudioSettingsUpdated += HandleAudioSettingsUpdated;
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            GameEvents.OnPlayerTurnStarted += HandleTurnStarted;
            GameEvents.OnSettlementBuilt += HandleSettlementBuilt;
            GameEvents.OnSettlementRelocated += HandleSettlementRelocated;
            GameEvents.OnPlayerScoreUpdated += HandleScoreUpdated;
            GameEvents.OnRoundChanged += HandleRoundChanged;
            GameEvents.OnVictory += HandleVictory;
            GameEvents.OnBoardEvent += HandleBoardEvent;
        }

        private void OnDisable()
        {
            GameEvents.OnAudioSettingsUpdated -= HandleAudioSettingsUpdated;
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
            GameEvents.OnPlayerTurnStarted -= HandleTurnStarted;
            GameEvents.OnSettlementBuilt -= HandleSettlementBuilt;
            GameEvents.OnSettlementRelocated -= HandleSettlementRelocated;
            GameEvents.OnPlayerScoreUpdated -= HandleScoreUpdated;
            GameEvents.OnRoundChanged -= HandleRoundChanged;
            GameEvents.OnVictory -= HandleVictory;
            GameEvents.OnBoardEvent -= HandleBoardEvent;
        }

        private void Start()
        {
            var s = Settings.SettingsManager.Instance;
            if (s != null) HandleAudioSettingsUpdated(s.SfxVolume, s.SfxMuted, s.MusicVolume, s.MusicMuted);
            else HandleAudioSettingsUpdated(80f, false, 70f, false);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // ducking: fast attack, hold, slow release (avoids audible "pumping")
            float target = Time.unscaledTime < _duckHoldUntil ? _duckTargetDb : 0f;
            float rate = target < _duckDb ? DuckAttackDbPerSec : DuckReleaseDbPerSec;
            _duckDb = Mathf.MoveTowards(_duckDb, target, rate * dt);

            float musicBus = _musicMuted ? 0f : _musicGain * DbToGain(_duckDb);
            _music?.Tick(dt, musicBus);
        }

        // ------------------------------------------------------------------ settings → buses

        private void HandleAudioSettingsUpdated(float sfxVol, bool sfxMute, float musicVol, bool musicMute)
        {
            _sfxMuted = sfxMute;
            _musicMuted = musicMute;
            _sfxGain = SliderToGain(sfxVol) * DbToGain(_masterHeadroomDb);
            _musicGain = SliderToGain(musicVol) * DbToGain(_masterHeadroomDb + _musicTrimDb);
        }

        /// <summary>0..100 slider → dB on a linear scale (-30..0) → linear gain. 0 = silence.</summary>
        private float SliderToGain(float slider0To100)
        {
            float s = Mathf.Clamp01(slider0To100 / 100f);
            if (s <= 0.001f) return 0f;
            return DbToGain(Mathf.Lerp(_sliderFloorDb, 0f, s));
        }

        private static float DbToGain(float db) => Mathf.Pow(10f, db / 20f);

        /// <summary>Dip the music bus by <paramref name="db"/> (negative) for <paramref name="hold"/> seconds.</summary>
        public void DuckMusic(float db, float hold)
        {
            _duckTargetDb = Mathf.Min(db, Time.unscaledTime < _duckHoldUntil ? _duckTargetDb : 0f);
            _duckHoldUntil = Mathf.Max(_duckHoldUntil, Time.unscaledTime + hold);
        }

        public void SetMusicIntensity(int level) => _music?.SetIntensity(level);

        // ------------------------------------------------------------------ game events

        private void HandleGameStateChanged(GameState state)
        {
            _inGameplay = state == GameState.Gameplay;
            if (_inGameplay)
            {
                _inTension = false;
                SetMusicIntensity(1);
            }
            else if (state == GameState.MainMenu)
            {
                _inTension = false;
                SetMusicIntensity(0);
            }
        }

        private void HandleTurnStarted(int idx, string name, Color c) => Play(Sfx.TurnStart);
        private void HandleSettlementBuilt(Vector2Int coords, int player, int points)
        {
            Play(Sfx.Build);
            DuckMusic(-4f, 0.35f);
        }
        private void HandleSettlementRelocated(Vector2Int from, Vector2Int to, int player) => Play(Sfx.Relocate);

        private void HandleRoundChanged(int round)
        {
            if (round <= 1) return;
            Play(Sfx.Round);
            DuckMusic(-5f, 0.6f);
        }

        private void HandleScoreUpdated(int playerIndex, int score)
        {
            if (!_inGameplay) return;
            float target = MatchConfig.Current != null ? MatchConfig.Current.TargetScore : PlayerData.TargetScoreToWin;
            if (target <= 0f) return;

            int best = score;
            var players = Board.TurnManager.Instance?.GetAllPlayers();
            if (players != null)
                foreach (var p in players) best = Mathf.Max(best, p.TotalScore);

            float progress = best / target;
            if (!_inTension && progress >= _tensionEnter) _inTension = true;
            else if (_inTension && progress < _tensionExit) _inTension = false;
            SetMusicIntensity(_inTension ? 2 : 1);
        }

        private void HandleBoardEvent(int player, Vector2Int c, BoardEventType type, int amount)
        {
            switch (type)
            {
                case BoardEventType.Treasure: Play(Sfx.Score); Play(Sfx.Build, 1.2f); DuckMusic(-5f, 0.4f); break;
                case BoardEventType.Wisp:     Play(Sfx.Flip, 1.3f); break;
                case BoardEventType.Storm:    PlayMountainBonk(); break;
                case BoardEventType.Portal:   Play(Sfx.Relocate, amount == 0 ? 1.2f : 0.9f); break;
                case BoardEventType.Relic:    Play(Sfx.Round, 1.15f); DuckMusic(-8f, 0.8f); break;
                case BoardEventType.Bloom:    Play(Sfx.Round); DuckMusic(-8f, 0.8f); break;
                case BoardEventType.Upgrade:  Play(Sfx.Build, 1.1f); break;
                case BoardEventType.Siege:    Play(Sfx.Mountain, 1.25f); Play(Sfx.Score, 0.8f); break;
            }
        }

        private void HandleVictory(int p, string name, Color c, int s)
        {
            _inGameplay = false;
            Play(Sfx.Victory);
            DuckMusic(-14f, 2.6f);
            SetMusicIntensity(0);
        }

        // ------------------------------------------------------------------ public API (kept stable)

        public void PlayClick() => Play(Sfx.UiClick);
        public void PlayHover() => Play(Sfx.UiHover);
        public void PlayDiceRoll() => Play(Sfx.DiceRoll);
        public void PlayDiceLand() => Play(Sfx.DiceLand);
        public void PlayJump() => Play(Sfx.Jump);
        public void PlayLand() => Play(Sfx.Land);
        public void PlayTileFlip() => Play(Sfx.Flip);
        public void PlayMountainBonk()
        {
            Play(Sfx.Mountain);
            DuckMusic(-6f, 0.25f);
        }
        public void PlayBuildSettlement() => Play(Sfx.Build);
        public void PlayVictoryFanfare() => Play(Sfx.Victory);
        public void PlayScoreGain() => Play(Sfx.Score);
        public void PlayCountdownTick(int second) => Play(Sfx.Tick, 1f + (3 - Mathf.Clamp(second, 1, 3)) * 0.12f);
        public void PlayCountdownStart() => Play(Sfx.Round, 1.12f);

        // ------------------------------------------------------------------ voice playback

        private void Play(Sfx id, float pitchMul = 1f)
        {
            if (_sfxMuted || !_sounds.TryGetValue(id, out var def) || def.Takes.Length == 0) return;

            float now = Time.unscaledTime;
            if (now - def.LastPlayed < def.Cooldown) return; // dedupe (e.g. pointer-down + onClick)
            def.LastPlayed = now;

            // pick a different take than last time
            int take = def.Takes.Length == 1 ? 0 : Random.Range(0, def.Takes.Length - 1);
            if (def.Takes.Length > 1 && take >= def.LastTake) take++;
            def.LastTake = take;

            float busGain = def.Bus == Bus.Ui ? _sfxGain * DbToGain(_uiTrimDb) : _sfxGain;
            var voice = GrabVoice();
            voice.clip = def.Takes[take];
            voice.pitch = pitchMul * (1f + Random.Range(-def.PitchJitter, def.PitchJitter));
            voice.volume = busGain * def.Volume * (1f - Random.Range(0f, def.VolumeJitter));
            voice.Play();
        }

        private AudioSource GrabVoice()
        {
            int oldest = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].isPlaying)
                {
                    _voiceStart[i] = Time.unscaledTime;
                    return _voices[i];
                }
                if (_voiceStart[i] < _voiceStart[oldest]) oldest = i;
            }
            _voiceStart[oldest] = Time.unscaledTime; // steal the oldest voice
            return _voices[oldest];
        }

        // ------------------------------------------------------------------ sound design

        private void Register(Sfx id, Bus bus, float volume, float pitchJitter, float cooldown, params AudioClip[] takes)
        {
            _sounds[id] = new SoundDef { Takes = takes, Bus = bus, Volume = volume, PitchJitter = pitchJitter, Cooldown = cooldown };
        }

        private void BakeSounds()
        {
            const int R = ProceduralSynth.SfxRate;
            var rng = new System.Random(2026);

            // --- UI: short, bright, unobtrusive
            Register(Sfx.UiClick, Bus.Ui, 1f, 0.03f, 0.07f,
                MakeClick(rng, 1750f), MakeClick(rng, 1900f), MakeClick(rng, 1650f));
            {
                var b = ProceduralSynth.Buffer(0.04f);
                ProceduralSynth.AddTone(b, R, 0f, 0.04f, 2600f, 2900f, 0.4f, 0.002f, 0.01f);
                Register(Sfx.UiHover, Bus.Ui, 0.35f, 0.05f, 0.05f, ProceduralSynth.ToClip("UiHover", b));
            }
            {
                var b = ProceduralSynth.Buffer(0.06f);
                ProceduralSynth.AddTone(b, R, 0f, 0.06f, 1100f, 1000f, 0.6f, 0.001f, 0.02f, ProceduralSynth.Pluck);
                Register(Sfx.Tick, Bus.Ui, 0.9f, 0f, 0.1f, ProceduralSynth.ToClip("Tick", b));
            }

            // --- Dice: rattle (accelerating clacks) and a woody landing
            Register(Sfx.DiceRoll, Bus.Sfx, 0.8f, 0.05f, 0.15f, MakeRattle(rng, 0), MakeRattle(rng, 1), MakeRattle(rng, 2));
            Register(Sfx.DiceLand, Bus.Sfx, 1f, 0.05f, 0.08f, MakeKnock(rng, 150f, 520f), MakeKnock(rng, 165f, 610f));

            // --- Pawn: soft hop + felt landing, several takes so stepping isn't machine-gun
            Register(Sfx.Jump, Bus.Sfx, 0.45f, 0.06f, 0.04f, MakeHop(rng, 300f), MakeHop(rng, 340f), MakeHop(rng, 280f));
            Register(Sfx.Land, Bus.Sfx, 0.7f, 0.06f, 0.04f, MakeKnock(rng, 120f, 380f, 0.5f), MakeKnock(rng, 135f, 420f, 0.5f));

            // --- Tile reveal: fog whoosh + two-note shimmer
            Register(Sfx.Flip, Bus.Sfx, 0.75f, 0.03f, 0.05f, MakeReveal(rng, 88), MakeReveal(rng, 91));

            // --- Mountain: heavy rock impact (low body + crunchy noise)
            {
                var b = ProceduralSynth.Buffer(0.6f);
                ProceduralSynth.AddTone(b, R, 0f, 0.6f, 120f, 38f, 1f, 0.002f, 0.18f, ProceduralSynth.Warm);
                ProceduralSynth.AddNoise(b, R, 0f, 0.45f, 0.7f, 0.001f, 0.12f, 0.08f, rng);
                ProceduralSynth.AddNoise(b, R, 0f, 0.08f, 0.35f, 0.001f, 0.02f, 0.5f, rng, highpass: true);
                ProceduralSynth.AddTone(b, R, 0.01f, 0.15f, 340f, 260f, 0.25f, 0.001f, 0.04f, ProceduralSynth.Pluck);
                Register(Sfx.Mountain, Bus.Sfx, 1f, 0.04f, 0.1f, ProceduralSynth.ToClip("Mountain", b, R, 0.9f));
            }

            // --- Settlement: rising major arpeggio + bell = "claimed!"
            {
                var b = ProceduralSynth.Buffer(1.1f);
                int[] notes = { 69, 73, 76, 81 };
                for (int i = 0; i < notes.Length; i++)
                {
                    float hz = ProceduralSynth.MidiToHz(notes[i]);
                    ProceduralSynth.AddTone(b, R, i * 0.065f, 0.8f, hz, hz, 0.5f, 0.003f, 0.22f, ProceduralSynth.Pluck);
                }
                ProceduralSynth.AddTone(b, R, 0.2f, 0.9f, ProceduralSynth.MidiToHz(88), ProceduralSynth.MidiToHz(88), 0.18f, 0.003f, 0.4f, ProceduralSynth.Bell);
                ProceduralSynth.AddTone(b, R, 0f, 0.25f, 110f, 70f, 0.5f, 0.002f, 0.07f); // weight
                Register(Sfx.Build, Bus.Sfx, 0.9f, 0.015f, 0.15f, ProceduralSynth.ToClip("Build", b));
            }

            // --- Relocate: downward whoosh + two notes
            {
                var b = ProceduralSynth.Buffer(0.7f);
                ProceduralSynth.AddNoise(b, R, 0f, 0.45f, 0.5f, 0.12f, 0.15f, 0.25f, rng, lowpassEnd: 0.03f);
                ProceduralSynth.AddTone(b, R, 0.15f, 0.5f, ProceduralSynth.MidiToHz(76), ProceduralSynth.MidiToHz(76), 0.35f, 0.003f, 0.18f, ProceduralSynth.Pluck);
                ProceduralSynth.AddTone(b, R, 0.27f, 0.5f, ProceduralSynth.MidiToHz(69), ProceduralSynth.MidiToHz(69), 0.35f, 0.003f, 0.2f, ProceduralSynth.Pluck);
                Register(Sfx.Relocate, Bus.Sfx, 0.8f, 0.02f, 0.15f, ProceduralSynth.ToClip("Relocate", b));
            }

            // --- Score: bright coin-like double blip
            {
                var b = ProceduralSynth.Buffer(0.4f);
                ProceduralSynth.AddTone(b, R, 0f, 0.12f, ProceduralSynth.MidiToHz(83), ProceduralSynth.MidiToHz(83), 0.45f, 0.002f, 0.05f, ProceduralSynth.Bell);
                ProceduralSynth.AddTone(b, R, 0.07f, 0.33f, ProceduralSynth.MidiToHz(88), ProceduralSynth.MidiToHz(88), 0.5f, 0.002f, 0.12f, ProceduralSynth.Bell);
                Register(Sfx.Score, Bus.Sfx, 0.7f, 0.02f, 0.06f, ProceduralSynth.ToClip("Score", b));
            }

            // --- Turn start: soft two-note bell (a "your move" cue, not an alarm)
            {
                var b = ProceduralSynth.Buffer(1.0f);
                ProceduralSynth.AddTone(b, R, 0f, 1f, ProceduralSynth.MidiToHz(76), ProceduralSynth.MidiToHz(76), 0.35f, 0.004f, 0.35f, ProceduralSynth.Bell);
                ProceduralSynth.AddTone(b, R, 0.09f, 0.9f, ProceduralSynth.MidiToHz(81), ProceduralSynth.MidiToHz(81), 0.3f, 0.004f, 0.35f, ProceduralSynth.Bell);
                Register(Sfx.TurnStart, Bus.Sfx, 0.5f, 0f, 0.3f, ProceduralSynth.ToClip("TurnStart", b));
            }

            // --- Round complete stinger
            {
                var b = ProceduralSynth.Buffer(1.4f);
                int[] notes = { 69, 72, 76, 81, 84 };
                for (int i = 0; i < notes.Length; i++)
                {
                    float hz = ProceduralSynth.MidiToHz(notes[i]);
                    ProceduralSynth.AddTone(b, R, i * 0.08f, 1.0f, hz, hz, 0.35f, 0.004f, 0.35f, ProceduralSynth.Bell);
                }
                Register(Sfx.Round, Bus.Sfx, 0.75f, 0f, 0.3f, ProceduralSynth.ToClip("Round", b));
            }

            // --- Victory fanfare: brass arpeggio into a held vibrato chord
            {
                var b = ProceduralSynth.Buffer(2.6f);
                int[] run = { 60, 64, 67, 72 };
                for (int i = 0; i < run.Length; i++)
                {
                    float hz = ProceduralSynth.MidiToHz(run[i]);
                    ProceduralSynth.AddTone(b, R, i * 0.13f, 0.3f, hz, hz, 0.4f, 0.01f, 0.12f, ProceduralSynth.Brass);
                }
                int[] chord = { 60, 64, 67, 72, 76 };
                foreach (int n in chord)
                {
                    float hz = ProceduralSynth.MidiToHz(n);
                    ProceduralSynth.AddTone(b, R, 0.55f, 2.0f, hz, hz, 0.22f, 0.04f, 0.9f, ProceduralSynth.Brass, vibratoHz: 5.5f, vibratoDepth: 0.004f);
                }
                ProceduralSynth.AddTone(b, R, 0.55f, 0.5f, 90f, 45f, 0.6f, 0.002f, 0.15f); // timpani-ish hit
                ProceduralSynth.AddNoise(b, R, 0.55f, 1.2f, 0.12f, 0.002f, 0.5f, 0.6f, rng, highpass: true); // cymbal wash
                Register(Sfx.Victory, Bus.Sfx, 1f, 0f, 1f, ProceduralSynth.ToClip("Victory", b, R, 0.85f));
            }
        }

        private static AudioClip MakeClick(System.Random rng, float hz)
        {
            var b = ProceduralSynth.Buffer(0.05f);
            ProceduralSynth.AddTone(b, ProceduralSynth.SfxRate, 0f, 0.05f, hz, hz * 0.7f, 0.6f, 0.001f, 0.012f, ProceduralSynth.Pluck);
            ProceduralSynth.AddNoise(b, ProceduralSynth.SfxRate, 0f, 0.015f, 0.3f, 0.0005f, 0.004f, 0.6f, rng, highpass: true);
            return ProceduralSynth.ToClip("UiClick", b);
        }

        private static AudioClip MakeRattle(System.Random rng, int variant)
        {
            const int R = ProceduralSynth.SfxRate;
            var b = ProceduralSynth.Buffer(0.55f);
            float t = 0f;
            float gap = 0.028f;
            int hits = 9 + variant;
            for (int i = 0; i < hits && t < 0.5f; i++)
            {
                float amp = Mathf.Lerp(0.9f, 0.45f, i / (float)hits) * (0.7f + (float)rng.NextDouble() * 0.3f);
                float hz = 700f + (float)rng.NextDouble() * 900f;
                ProceduralSynth.AddTone(b, R, t, 0.05f, hz, hz * 0.9f, amp * 0.5f, 0.0005f, 0.012f, ProceduralSynth.Pluck);
                ProceduralSynth.AddNoise(b, R, t, 0.03f, amp * 0.4f, 0.0005f, 0.008f, 0.4f, rng, highpass: true);
                t += gap * (0.8f + (float)rng.NextDouble() * 0.5f);
                gap *= 1.12f; // dice slow down
            }
            return ProceduralSynth.ToClip("DiceRoll" + variant, b, R, 0.75f);
        }

        private static AudioClip MakeKnock(System.Random rng, float bodyHz, float knockHz, float size = 1f)
        {
            const int R = ProceduralSynth.SfxRate;
            var b = ProceduralSynth.Buffer(0.3f);
            ProceduralSynth.AddTone(b, R, 0f, 0.3f, bodyHz, bodyHz * 0.55f, 1f, 0.001f, 0.06f * size + 0.02f);
            ProceduralSynth.AddTone(b, R, 0f, 0.12f, knockHz, knockHz * 0.92f, 0.45f, 0.0005f, 0.025f, ProceduralSynth.Pluck);
            ProceduralSynth.AddNoise(b, R, 0f, 0.06f, 0.35f, 0.0005f, 0.012f, 0.3f, rng);
            return ProceduralSynth.ToClip("Knock", b);
        }

        private static AudioClip MakeHop(System.Random rng, float hz)
        {
            const int R = ProceduralSynth.SfxRate;
            var b = ProceduralSynth.Buffer(0.14f);
            ProceduralSynth.AddTone(b, R, 0f, 0.14f, hz, hz * 1.8f, 0.6f, 0.004f, 0.05f, ProceduralSynth.Warm);
            ProceduralSynth.AddNoise(b, R, 0f, 0.1f, 0.12f, 0.01f, 0.03f, 0.15f, rng, lowpassEnd: 0.4f);
            return ProceduralSynth.ToClip("Hop", b, R, 0.6f);
        }

        private static AudioClip MakeReveal(System.Random rng, int topNote)
        {
            const int R = ProceduralSynth.SfxRate;
            var b = ProceduralSynth.Buffer(0.8f);
            ProceduralSynth.AddNoise(b, R, 0f, 0.4f, 0.55f, 0.08f, 0.1f, 0.02f, rng, lowpassEnd: 0.35f); // fog lifts
            float a = ProceduralSynth.MidiToHz(topNote - 5), c = ProceduralSynth.MidiToHz(topNote);
            ProceduralSynth.AddTone(b, R, 0.16f, 0.6f, a, a, 0.25f, 0.003f, 0.2f, ProceduralSynth.Bell);
            ProceduralSynth.AddTone(b, R, 0.24f, 0.55f, c, c, 0.22f, 0.003f, 0.22f, ProceduralSynth.Bell);
            return ProceduralSynth.ToClip("Reveal", b);
        }
    }
}
