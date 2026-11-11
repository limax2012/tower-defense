using MaximalBastion.Audio;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class ProtocolReferenceTests
{
    public static void Run()
    {
        // Activation is a fixed 200 ms, 3:2 hum with linear attack and release.
        // Compare the final bank PCM so filtering, normalization and variant drift are covered.
        const int sampleRate = 44100;
        var reference = new short[(int)(sampleRate * .20f)];
        for (var index = 0; index < reference.Length; index++)
        {
            var t = index / (float)(reference.Length - 1);
            var time = index / (float)sampleRate;
            var wave = MathF.Sin(MathHelper.TwoPi * 390 * time) * .55f +
                       MathF.Sin(MathHelper.TwoPi * 585 * time) * .45f;
            var attack = MathHelper.Clamp(t / .08f, 0, 1);
            var release = MathHelper.Clamp((1f - t) / .32f, 0, 1);
            reference[index] = (short)(MathHelper.Clamp(wave * (attack * release) * .28f, -1, 1) * short.MaxValue);
        }

        for (var variant = 0; variant < ProceduralSoundBank.VariantCount; variant++)
        {
            var actual = ProceduralSoundBank.CreateCue(SoundCue.Protocol, variant);
            Check.True(reference.SequenceEqual(actual), "Protocol retains its exact activation-hum PCM for every variation");
        }
    }
}
