using System.Text;

namespace MaximalBastion.Audio;

/// <summary>Loads authored mono PCM embedded in both desktop and browser builds.</summary>
internal static class RenderedSoundBank
{
    internal static short[] Load(string family, WeaponSoundLayer layer, int stage, int variant)
    {
        var name = $"MaximalBastion.Audio.Samples.{family}-{layer.ToString().ToLowerInvariant()}-{stage}-{variant}.wav";
        using var stream = typeof(RenderedSoundBank).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException("Missing authored sound: " + name);
        using var reader = new BinaryReader(stream, Encoding.ASCII);
        if (ReadTag(reader) != "RIFF") throw new InvalidDataException("Expected RIFF audio.");
        var length = reader.ReadUInt32();
        if (length + 8L != stream.Length || ReadTag(reader) != "WAVE")
            throw new InvalidDataException("Invalid WAV container.");
        var validFormat = false;
        while (stream.Position + 8 <= stream.Length)
        {
            var tag = ReadTag(reader);
            var size = reader.ReadUInt32();
            var end = stream.Position + size;
            if (end > stream.Length) throw new InvalidDataException("Truncated WAV chunk.");
            if (tag == "fmt ")
            {
                if (size < 16 || reader.ReadUInt16() != 1 || reader.ReadUInt16() != 1 ||
                    reader.ReadUInt32() != ProceduralSoundBank.SampleRate ||
                    reader.ReadUInt32() != ProceduralSoundBank.SampleRate * 2 ||
                    reader.ReadUInt16() != 2 || reader.ReadUInt16() != 16)
                    throw new InvalidDataException("Expected 44.1 kHz mono 16-bit PCM.");
                validFormat = true;
            }
            else if (tag == "data")
            {
                if (!validFormat || size < 4 || size % 2 != 0 || size > ProceduralSoundBank.SampleRate * 4)
                    throw new InvalidDataException("Invalid authored sound data.");
                var samples = new short[size / 2];
                for (var index = 0; index < samples.Length; index++) samples[index] = reader.ReadInt16();
                return samples;
            }
            stream.Position = end + (size & 1);
        }
        throw new InvalidDataException("WAV contains no PCM data.");
    }

    private static string ReadTag(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}
