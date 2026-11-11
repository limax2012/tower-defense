using Microsoft.Xna.Framework;

namespace MaximalBastion.Audio;

/// <summary>Simple, stable signal shapes for short interface gestures and activation hums.</summary>
internal static class ProceduralSignalReferences
{
    private const int SampleRate = ProceduralSoundBank.SampleRate;

    internal static short[] Tone(float startFrequency, float endFrequency, float seconds,
        bool triangle = false, float sourceGain = .30f)
    {
        var samples = new short[Math.Max(1, (int)(SampleRate * seconds))];
        var phase = 0f;
        for (var index = 0; index < samples.Length; index++)
        {
            var t = index / (float)Math.Max(1, samples.Length - 1);
            var frequency = MathHelper.Lerp(startFrequency, endFrequency, t);
            phase += MathHelper.TwoPi * frequency / SampleRate;
            var wave = triangle ? 2f / MathF.PI * MathF.Asin(MathF.Sin(phase)) : MathF.Sin(phase);
            samples[index] = ToSample(wave * Envelope(t) * sourceGain);
        }
        return samples;
    }

    internal static short[] Chord(float firstFrequency, float secondFrequency, float seconds,
        float sourceGain = .28f)
    {
        var samples = new short[Math.Max(1, (int)(SampleRate * seconds))];
        for (var index = 0; index < samples.Length; index++)
        {
            var t = index / (float)Math.Max(1, samples.Length - 1);
            var time = index / (float)SampleRate;
            var wave = MathF.Sin(MathHelper.TwoPi * firstFrequency * time) * .55f +
                       MathF.Sin(MathHelper.TwoPi * secondFrequency * time) * .45f;
            samples[index] = ToSample(wave * Envelope(t) * sourceGain);
        }
        return samples;
    }

    internal static short[] Triad(float firstFrequency, float secondFrequency, float thirdFrequency,
        float seconds, float sourceGain = .27f)
    {
        var samples = new short[Math.Max(1, (int)(SampleRate * seconds))];
        for (var index = 0; index < samples.Length; index++)
        {
            var t = index / (float)Math.Max(1, samples.Length - 1);
            var time = index / (float)SampleRate;
            var wave = MathF.Sin(MathHelper.TwoPi * firstFrequency * time) * .38f +
                       MathF.Sin(MathHelper.TwoPi * secondFrequency * time) * .34f +
                       MathF.Sin(MathHelper.TwoPi * thirdFrequency * time) * .28f;
            samples[index] = ToSample(wave * Envelope(t) * sourceGain);
        }
        return samples;
    }

    internal static short[] TwoNote(float firstFrequency, float secondFrequency, float seconds,
        float sourceGain = .18f)
    {
        var samples = new short[Math.Max(2, (int)(SampleRate * seconds))];
        var split = samples.Length / 2;
        for (var index = 0; index < samples.Length; index++)
        {
            var firstNote = index < split;
            var noteStart = firstNote ? 0 : split;
            var noteLength = firstNote ? split : samples.Length - split;
            var noteTime = (index - noteStart) / (float)Math.Max(1, noteLength - 1);
            var frequency = firstNote ? firstFrequency : secondFrequency;
            var time = (index - noteStart) / (float)SampleRate;
            var envelope = MathF.Sin(MathF.PI * noteTime);
            var wave = MathF.Sin(MathHelper.TwoPi * frequency * time);
            samples[index] = ToSample(wave * envelope * envelope * sourceGain);
        }
        return samples;
    }

    private static float Envelope(float t) =>
        MathHelper.Clamp(t / .08f, 0, 1) * MathHelper.Clamp((1 - t) / .32f, 0, 1);

    private static short ToSample(float value) => (short)(MathHelper.Clamp(value, -1, 1) * short.MaxValue);
}
