using UnityEngine;

namespace AncientTree.Audio
{
    /// <summary>
    /// Vertical-layering adaptive score. Three stems of identical length are baked procedurally and started
    /// on the same dspTime, so they stay sample-locked forever. Intensity only changes stem volumes:
    ///   0 = Calm (menu)      : pad
    ///   1 = Explore (match)  : pad + plucked arpeggio
    ///   2 = Tension (endgame): pad + arpeggio + pulse (kick, bass, hats)
    /// Progression Am – F – Dm – E (harmonic minor) at 80 BPM, 4 bars = 12 s loop.
    /// </summary>
    public class AdaptiveMusic
    {
        private const float Bpm = 80f;
        private const int BeatsPerBar = 4;
        private const int Bars = 4;
        private const float FadeSeconds = 1.4f;

        // root (MIDI), and chord tones relative to root
        private static readonly int[] Roots = { 57, 53, 50, 52 };          // A3, F3, D3, E3
        private static readonly int[][] Chords =
        {
            new[] { 0, 3, 7 },   // Am
            new[] { 0, 4, 7 },   // F
            new[] { 0, 3, 7 },   // Dm
            new[] { 0, 4, 7 },   // E (major: G# leading tone gives the "ancient" pull back to Am)
        };

        private readonly AudioSource[] _stems = new AudioSource[3];
        private readonly float[] _current = new float[3];
        private readonly float[] _target = new float[3];
        private static readonly float[][] LayerMix =
        {
            new[] { 1f, 0f,   0f },
            new[] { 1f, 0.85f, 0f },
            new[] { 0.9f, 1f, 0.9f },
        };

        public int Intensity { get; private set; } = -1;

        public AdaptiveMusic(GameObject host)
        {
            float secPerBeat = 60f / Bpm;
            float loopLen = secPerBeat * BeatsPerBar * Bars;

            var clips = new[]
            {
                ProceduralSynth.ToClip("Music_Pad", BakePad(loopLen, secPerBeat), ProceduralSynth.MusicRate, 0.55f),
                ProceduralSynth.ToClip("Music_Arp", BakeArp(loopLen, secPerBeat), ProceduralSynth.MusicRate, 0.45f),
                ProceduralSynth.ToClip("Music_Pulse", BakePulse(loopLen, secPerBeat), ProceduralSynth.MusicRate, 0.5f),
            };

            double startDsp = AudioSettings.dspTime + 0.2;
            for (int i = 0; i < _stems.Length; i++)
            {
                var src = host.AddComponent<AudioSource>();
                src.clip = clips[i];
                src.loop = true;
                src.playOnAwake = false;
                src.volume = 0f;
                src.spatialBlend = 0f;
                src.priority = 0;
                src.PlayScheduled(startDsp); // same dspTime => locked together
                _stems[i] = src;
            }
            SetIntensity(0);
        }

        public void SetIntensity(int level)
        {
            level = Mathf.Clamp(level, 0, LayerMix.Length - 1);
            if (level == Intensity) return;
            Intensity = level;
            for (int i = 0; i < _target.Length; i++) _target[i] = LayerMix[level][i];
        }

        /// <summary>Call every frame with UNSCALED delta (music keeps fading while paused / during hit-stop).</summary>
        public void Tick(float unscaledDt, float busGain)
        {
            float step = unscaledDt / FadeSeconds;
            for (int i = 0; i < _stems.Length; i++)
            {
                _current[i] = Mathf.MoveTowards(_current[i], _target[i], step);
                if (_stems[i] != null)
                {
                    // equal-power-ish curve so a fade doesn't sound like it "drops out" at the end
                    float shaped = Mathf.Sin(_current[i] * Mathf.PI * 0.5f);
                    _stems[i].volume = shaped * busGain;
                }
            }
        }

        // ---------------------------------------------------------------- baking

        private static float[] BakePad(float loopLen, float spb)
        {
            int rate = ProceduralSynth.MusicRate;
            var buf = ProceduralSynth.Buffer(loopLen, rate);
            float barLen = spb * BeatsPerBar;
            for (int bar = 0; bar < Bars; bar++)
            {
                float start = bar * barLen;
                int root = Roots[bar];
                foreach (int interval in Chords[bar])
                {
                    float hz = ProceduralSynth.MidiToHz(root + interval);
                    // two slightly detuned voices = gentle chorus; long tail overlaps (wraps) into the next bar
                    ProceduralSynth.AddTone(buf, rate, start, barLen * 1.45f, hz * 0.9985f, hz * 0.9985f, 0.09f, 1.1f, 2.4f, ProceduralSynth.Warm, wrap: true);
                    ProceduralSynth.AddTone(buf, rate, start, barLen * 1.45f, hz * 1.0015f, hz * 1.0015f, 0.09f, 1.3f, 2.4f, ProceduralSynth.Warm, wrap: true);
                }
                float bassHz = ProceduralSynth.MidiToHz(root - 12);
                ProceduralSynth.AddTone(buf, rate, start, barLen * 1.2f, bassHz, bassHz, 0.16f, 0.6f, 2.2f, ProceduralSynth.Sine, wrap: true);
            }
            // soft airy noise bed = "fog"
            ProceduralSynth.AddNoise(buf, rate, 0f, loopLen, 0.012f, 0.01f, 999f, 0.04f, new System.Random(7), wrap: true);
            return buf;
        }

        private static float[] BakeArp(float loopLen, float spb)
        {
            int rate = ProceduralSynth.MusicRate;
            var buf = ProceduralSynth.Buffer(loopLen, rate);
            var rng = new System.Random(1234);
            int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 1 };
            float eighth = spb * 0.5f;
            for (int bar = 0; bar < Bars; bar++)
            {
                int root = Roots[bar] + 12;
                var c = Chords[bar];
                int[] tones = { root + c[0], root + c[1], root + c[2], root + 12 };
                for (int step = 0; step < 8; step++)
                {
                    if (step > 0 && rng.NextDouble() < 0.12) continue; // humanize: drop a few notes
                    float t = bar * spb * BeatsPerBar + step * eighth;
                    float vel = (step % 2 == 0 ? 0.11f : 0.075f) * (0.85f + (float)rng.NextDouble() * 0.3f);
                    float hz = ProceduralSynth.MidiToHz(tones[pattern[step]]);
                    ProceduralSynth.AddTone(buf, rate, t, 1.0f, hz, hz, vel, 0.004f, 0.28f, ProceduralSynth.Pluck, wrap: true);
                }
            }
            return buf;
        }

        private static float[] BakePulse(float loopLen, float spb)
        {
            int rate = ProceduralSynth.MusicRate;
            var buf = ProceduralSynth.Buffer(loopLen, rate);
            var rng = new System.Random(99);
            float eighth = spb * 0.5f;
            for (int bar = 0; bar < Bars; bar++)
            {
                float barStart = bar * spb * BeatsPerBar;
                float bassHz = ProceduralSynth.MidiToHz(Roots[bar] - 12);
                for (int beat = 0; beat < BeatsPerBar; beat++)
                {
                    float t = barStart + beat * spb;
                    // low drum on 1 and 3, softer on 2 and 4
                    float kick = beat % 2 == 0 ? 0.55f : 0.25f;
                    ProceduralSynth.AddTone(buf, rate, t, 0.45f, 130f, 42f, kick, 0.002f, 0.13f, ProceduralSynth.Sine, wrap: true);
                    // off-beat shaker
                    ProceduralSynth.AddNoise(buf, rate, t + eighth, 0.08f, 0.09f, 0.003f, 0.025f, 0.35f, rng, highpass: true, wrap: true);
                }
                // driving eighth-note bass
                for (int e = 0; e < 8; e++)
                {
                    float t = barStart + e * eighth;
                    ProceduralSynth.AddTone(buf, rate, t, eighth * 0.95f, bassHz, bassHz, e % 2 == 0 ? 0.16f : 0.1f, 0.005f, 0.14f, ProceduralSynth.Warm, wrap: true);
                }
            }
            return buf;
        }
    }
}
