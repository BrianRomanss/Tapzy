using System.Collections.Generic;
using UnityEngine;

namespace Tapzy.UI
{
    /// <summary>Procedural beeps so every game has sound without any audio assets.</summary>
    public static class Sfx
    {
        const string PrefKey = "tapzy_sound";
        static bool loaded, enabled = true;

        /// <summary>The player's sound setting. Saved on the device, shared by the hub and every game.</summary>
        public static bool Enabled
        {
            get
            {
                if (!loaded)
                {
                    enabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
                    loaded = true;
                }
                return enabled;
            }
            set
            {
                enabled = value;
                loaded = true;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            }
        }

        static AudioSource source;
        static readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();

        public static void Click() => Blip(520f, 0.05f, 0.2f);
        public static void Good() => Blip(880f, 0.08f);
        public static void Great() => Blip(1175f, 0.12f);
        public static void Bad() => Blip(160f, 0.18f, 0.35f);
        public static void Coin() => Blip(1320f, 0.1f);

        public static void Blip(float frequency, float duration = 0.08f, float volume = 0.3f)
        {
            if (!Enabled) return;
            if (source == null)
            {
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }
            var key = Mathf.RoundToInt(frequency) * 1000 + Mathf.RoundToInt(duration * 1000f);
            if (!clips.TryGetValue(key, out var clip) || clip == null)
            {
                const int rate = 44100;
                var n = Mathf.Max(1, Mathf.RoundToInt(rate * duration));
                var data = new float[n];
                for (var i = 0; i < n; i++)
                {
                    var env = 1f - (float)i / n;
                    data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / rate) * env * env;
                }
                clip = AudioClip.Create("blip" + key, n, 1, rate, false);
                clip.SetData(data, 0);
                clips[key] = clip;
            }
            source.PlayOneShot(clip, volume);
        }
    }
}
