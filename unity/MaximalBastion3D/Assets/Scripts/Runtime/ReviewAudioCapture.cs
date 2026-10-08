using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace Bastion3D
{
    public sealed class ReviewAudioCapture : IDisposable
    {
        private readonly BinaryWriter _writer;
        private NativeArray<float> _samples;
        private float[] _managed;
        private byte[] _bytes;
        private readonly float _previousVolume;
        private readonly bool _previousPause;
        private bool _recording;
        private double _squares;
        private long _values;
        public int SampleRate { get; }
        public int Channels { get; }
        public int Frames { get; private set; }
        public long SampleFrames { get; private set; }
        public float Peak { get; private set; }
        public double Rms => _values == 0 ? 0 : Math.Sqrt(_squares / _values);
        public double Duration => SampleFrames / (double)SampleRate;

        public ReviewAudioCapture(string path)
        {
            if (Time.captureFramerate <= 0) throw new InvalidOperationException("Audio recording requires a fixed capture frame rate.");
            SampleRate = AudioSettings.outputSampleRate;
            Channels = ChannelCount(AudioSettings.speakerMode);
            if (SampleRate <= 0) throw new InvalidOperationException("The player audio device has no sample rate.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            _writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
            _previousVolume = AudioListener.volume;
            _previousPause = AudioListener.pause;
            WriteHeader(0);
            _recording = AudioRenderer.Start();
            if (!_recording) { _writer.Dispose(); throw new InvalidOperationException("Unity audio output is already being recorded."); }
            AudioListener.pause = false;
            AudioListener.volume = .7f;
        }

        public void CaptureFrame()
        {
            if (!_recording) throw new ObjectDisposedException(nameof(ReviewAudioCapture));
            int sampleFrames = AudioRenderer.GetSampleCountForCaptureFrame();
            if (sampleFrames <= 0 || sampleFrames > SampleRate)
                throw new InvalidOperationException("Unexpected Unity audio capture block: " + sampleFrames);
            int count = checked(sampleFrames * Channels);
            if (!_samples.IsCreated || _samples.Length != count)
            {
                if (_samples.IsCreated) _samples.Dispose();
                _samples = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _managed = new float[count];
                _bytes = new byte[checked(count * sizeof(float))];
            }
            if (!AudioRenderer.Render(_samples)) throw new InvalidOperationException("Unity audio rendering failed.");
            _samples.CopyTo(_managed);
            for (int i = 0; i < count; i++)
            {
                float value = _managed[i];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Audio contains a non-finite sample.");
                Peak = Mathf.Max(Peak, Mathf.Abs(value));
                _squares += (double)value * value;
            }
            Buffer.BlockCopy(_managed, 0, _bytes, 0, _bytes.Length);
            _writer.Write(_bytes);
            _values += count;
            SampleFrames += sampleFrames;
            Frames++;
        }

        public void VerifyAndWriteReport(string path, int expectedFrames, int framesPerSecond)
        {
            double expectedSeconds = expectedFrames / (double)framesPerSecond;
            bool passed = Frames == expectedFrames && Math.Abs(Duration - expectedSeconds) <= 1.0 / framesPerSecond && Rms > .00001;
            File.WriteAllText(path, string.Format(CultureInfo.InvariantCulture,
                "Actual Unity mixed output; no microphone capture.\nPassed: {0}\nFrames: {1}\nSample rate: {2}\nChannels: {3}\nSample frames: {4}\nDuration: {5:F6}\nExpected duration: {6:F6}\nRMS: {7:F8}\nPeak: {8:F6}\n",
                passed, Frames, SampleRate, Channels, SampleFrames, Duration, expectedSeconds, Rms, Peak));
            if (!passed) throw new InvalidOperationException("Recorded game audio is silent or out of sync; inspect " + path);
        }

        public void Dispose()
        {
            if (!_recording) return;
            _recording = false;
            AudioRenderer.Stop();
            AudioListener.volume = _previousVolume;
            AudioListener.pause = _previousPause;
            if (_samples.IsCreated) _samples.Dispose();
            try
            {
                uint dataSize = checked((uint)(SampleFrames * Channels * sizeof(float)));
                _writer.Seek(0, SeekOrigin.Begin);
                WriteHeader(dataSize);
                _writer.Flush();
            }
            finally { _writer.Dispose(); }
        }

        private void WriteHeader(uint dataSize)
        {
            _writer.Write(Encoding.ASCII.GetBytes("RIFF")); _writer.Write(checked(36u + dataSize));
            _writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); _writer.Write(16u);
            _writer.Write((ushort)3); _writer.Write((ushort)Channels);
            _writer.Write((uint)SampleRate); _writer.Write((uint)(SampleRate * Channels * sizeof(float)));
            _writer.Write((ushort)(Channels * sizeof(float))); _writer.Write((ushort)32);
            _writer.Write(Encoding.ASCII.GetBytes("data")); _writer.Write(dataSize);
        }

        private static int ChannelCount(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Stereo:
                case AudioSpeakerMode.Prologic: return 2;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                default: throw new InvalidOperationException("Unsupported review audio speaker layout: " + mode);
            }
        }

        public static void StartProbe(GameObject owner) => owner.AddComponent<ReviewAudioProbe>();
    }

    public sealed class ReviewAudioProbe : MonoBehaviour
    {
        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Application.Quit(1);
        }
        private IEnumerator Start()
        {
            var arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "--capture-directory");
            string directory = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(directory);
            var source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0;
            source.loop = true;
            source.volume = .2f;
            int rate = AudioSettings.outputSampleRate;
            var tone = new float[rate];
            for (int i = 0; i < rate; i++) tone[i] = Mathf.Sin(i * 440f * Mathf.PI * 2 / rate);
            source.clip = AudioClip.Create("Audio capture calibration tone", rate, 1, rate, false);
            source.clip.SetData(tone, 0);
            yield return null;
            Time.captureFramerate = 30;
            using (var audio = new ReviewAudioCapture(Path.Combine(directory, "capture-calibration.wav")))
            {
                source.Play();
                try
                {
                    for (int frame = 0; frame < 90; frame++)
                    {
                        yield return new WaitForEndOfFrame();
                        audio.CaptureFrame();
                    }
                    audio.VerifyAndWriteReport(Path.Combine(directory, "audio-verification.txt"), 90, 30);
                }
                finally { Time.captureFramerate = 0; }
            }
            Application.Quit(0);
        }
    }
}
