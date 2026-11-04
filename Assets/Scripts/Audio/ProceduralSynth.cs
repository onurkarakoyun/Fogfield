using UnityEngine;

namespace AncientTree.Audio
{
    /// <summary>
    /// Small additive/subtractive synth used to bake every SFX and music stem at startup.
    /// All writers mix INTO a buffer (overlap-add). With wrap=true, samples that run past the end
    /// wrap to the start, which makes music stems loop seamlessly (no click at the loop seam).
    /// Every voice has an attack ramp and a tail fade, so no clip starts or ends on a discontinuity.
    /// </summary>
    public static class ProceduralSynth
    {
        public const int SfxRate = 44100;
        public const int MusicRate = 22050;

        public static readonly float[] Sine = { 1f };
        public static readonly float[] Pluck = { 1f, 0.45f, 0.22f, 0.1f, 0.05f };
        public static readonly float[] Bell = { 1f, 0f, 0.35f, 0f, 0.18f, 0f, 0.08f };
        public static readonly float[] Brass = { 1f, 0.6f, 0.4f, 0.25f, 0.15f, 0.08f };
        public static readonly float[] Warm = { 1f, 0.3f, 0.12f };

        public static float MidiToHz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        public static float[] Buffer(float seconds, int rate = SfxRate) =>
            new float[Mathf.Max(1, Mathf.CeilToInt(seconds * rate))];

        /// <summary>Attack ramp then exponential decay. Reads naturally for plucks, knocks and bells.</summary>
        private static float Envelope(float t, float dur, float attack, float decay)
        {
            float env = t < attack ? t / attack : Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay));
            const float tail = 0.006f; // 6 ms anti-click fade at the end of the voice
            float remain = dur - t;
            if (remain < tail) env *= Mathf.Clamp01(remain / tail);
            return env;
        }

        /// <summary>
        /// Adds a pitched voice. Frequency glides exponentially from f0 to f1 (phase-accumulated, so no clicks).
        /// </summary>
        public static void AddTone(float[] buf, int rate, float start, float dur, float f0, float f1, float amp,
            float attack, float decay, float[] partials = null, bool wrap = false, float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            partials ??= Sine;
            int s0 = Mathf.RoundToInt(start * rate);
            int n = Mathf.RoundToInt(dur * rate);
            double phase = 0.0;
            float ratio = f1 / Mathf.Max(1f, f0);
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= buf.Length)
                {
                    if (!wrap) break;
                    idx %= buf.Length;
                }
                float t = (float)i / rate;
                float k = t / dur;
                float f = f0 * Mathf.Pow(ratio, k);
                if (vibratoDepth > 0f) f *= 1f + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * t);
                phase += f / rate;
                double p = phase * 2.0 * System.Math.PI;
                float v = 0f;
                for (int h = 0; h < partials.Length; h++)
                {
                    if (partials[h] == 0f) continue;
                    v += partials[h] * (float)System.Math.Sin(p * (h + 1));
                }
                buf[idx] += v * amp * Envelope(t, dur, attack, decay);
            }
        }

        /// <summary>
        /// Adds filtered noise. lowpass in (0..1]: smaller = darker. highpass=true keeps only the bright part (hats, ticks).
        /// lowpassEnd lets the filter sweep over the voice (whooshes).
        /// </summary>
        public static void AddNoise(float[] buf, int rate, float start, float dur, float amp, float attack, float decay,
            float lowpass, System.Random rng, bool highpass = false, bool wrap = false, float lowpassEnd = -1f)
        {
            int s0 = Mathf.RoundToInt(start * rate);
            int n = Mathf.RoundToInt(dur * rate);
            float y = 0f;
            if (lowpassEnd < 0f) lowpassEnd = lowpass;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= buf.Length)
                {
                    if (!wrap) break;
                    idx %= buf.Length;
                }
                float t = (float)i / rate;
                float a = Mathf.Lerp(lowpass, lowpassEnd, t / dur);
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                y += a * (x - y);
                float v = highpass ? x - y : y;
                buf[idx] += v * amp * Envelope(t, dur, attack, decay);
            }
        }

        /// <summary>Peak-normalizes (leaving headroom) and soft-clips, then wraps into an AudioClip.</summary>
        public static AudioClip ToClip(string name, float[] data, int rate = SfxRate, float peak = 0.8f)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            float gain = max > 0.0001f ? peak / max : 1f;
            for (int i = 0; i < data.Length; i++)
            {
                // gentle tanh-style saturation keeps transients round instead of hard clipping
                float x = data[i] * gain;
                data[i] = x / (1f + Mathf.Abs(x) * 0.25f) * 1.25f;
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
