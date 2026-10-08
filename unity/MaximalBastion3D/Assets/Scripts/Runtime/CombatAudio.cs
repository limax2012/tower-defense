using System.Collections.Generic;
using UnityEngine;

namespace Bastion3D
{
    public sealed class CombatAudio : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly AudioSource[] _voices = new AudioSource[24];
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private int _voice;
        public void Initialize()
        {
            string[] ids = { "needle_turret", "shard_fan", "watchtower", "frost_spire", "ember_coil", "breaker_cannon", "arc_relay", "siege_mortar", "prism_beam", "impact", "upgrade" };
            for (int i = 0; i < ids.Length; i++) _clips[ids[i]] = Synthesize(ids[i], i);
            for (int i = 0; i < _voices.Length; i++)
            {
                var source = new GameObject("Combat voice " + i).AddComponent<AudioSource>(); source.transform.SetParent(transform);
                source.spatialBlend = .65f; source.minDistance = 5; source.maxDistance = 80; source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0; source.playOnAwake = false; _voices[i] = source;
            }
            AudioListener.volume = PlayerPrefs.GetFloat("Volume", .65f);
        }
        public void Play(string id, Vector3 position)
        {
            if (!_clips.TryGetValue(id, out var clip)) return;
            if (_lastPlayed.TryGetValue(id, out float last) && Time.unscaledTime - last < .045f) return;
            _lastPlayed[id] = Time.unscaledTime;
            var voice = _voices[_voice++ % _voices.Length];
            voice.transform.position = position; voice.clip = clip; voice.volume = id == "impact" ? .16f : .22f;
            voice.pitch = 1; voice.Play();
        }
        private static AudioClip Synthesize(string id, int index)
        {
            const int rate = 24000;
            bool heavy = id == "siege_mortar" || id == "breaker_cannon" || id == "impact";
            float duration = heavy ? .42f : id == "upgrade" ? .65f : .22f;
            var samples = new float[(int)(rate * duration)];
            uint seed = (uint)(index + 1234);
            float phase = 0, filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, p = t / duration;
                seed = seed * 1664525 + 1013904223;
                float noise = (seed & 65535) / 32767.5f - 1;
                filtered += (noise - filtered) * (heavy ? .12f : .6f);
                float start = heavy ? 150 : 1100 + index * 120;
                float frequency = id == "upgrade" ? 550 + p * 450 : Mathf.Lerp(start, heavy ? 45 : 150 + index * 35, Mathf.Sqrt(p));
                phase += frequency * Mathf.PI * 2 / rate;
                float body = Mathf.Sin(phase) * .55f + Mathf.Sin(phase * 1.997f) * .14f;
                float envelope = Mathf.Min(1, t * 350) * Mathf.Pow(1 - p, heavy ? 2 : 3);
                samples[i] = (float)System.Math.Tanh((body + filtered * (heavy ? .8f : .23f)) * 1.3f) * envelope * .65f;
            }
            var clip = AudioClip.Create(id, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
    }
}
