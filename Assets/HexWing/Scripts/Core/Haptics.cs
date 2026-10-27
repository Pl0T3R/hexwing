using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Controller vibration: short pulses (ring passed, collision, grab) on top of an optional constant
    /// rumble (boost). Touch Plus vibrations need an explicit stop, so a small updater ends each pulse
    /// on real time. Strength is scaled by <see cref="HapticSettings.master"/>.
    /// </summary>
    public static class Haptics
    {
        struct Channel
        {
            public float Until, Frequency, Amplitude, Rumble;
            public bool Active;
        }

        static readonly Channel[] Channels = new Channel[2];   // 0 = left, 1 = right
        static Updater _updater;

        public static void Play(OVRInput.Controller hand, HapticPulse pulse)
        {
            if (pulse == null || pulse.amplitude <= 0f) return;
            Ensure();
            float master = GameConfig.Instance.haptics.master;
            ForEach(hand, i =>
            {
                Channels[i].Until = Time.unscaledTime + pulse.seconds;
                Channels[i].Frequency = pulse.frequency;
                Channels[i].Amplitude = pulse.amplitude * master;
            });
        }

        public static void PlayBoth(HapticPulse pulse) => Play(OVRInput.Controller.Touch, pulse);

        /// <summary>A constant light vibration under the pulses (0 = off). Call every frame it should change.</summary>
        public static void SetRumble(OVRInput.Controller hand, float amplitude)
        {
            Ensure();
            float a = amplitude * GameConfig.Instance.haptics.master;
            if ((hand & OVRInput.Controller.LTouch) != 0) Channels[0].Rumble = a;   // no lambda: called every frame
            if ((hand & OVRInput.Controller.RTouch) != 0) Channels[1].Rumble = a;
        }

        public static void StopAll()
        {
            for (int i = 0; i < 2; i++) Channels[i] = default;
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
        }

        static void ForEach(OVRInput.Controller hand, System.Action<int> action)
        {
            if ((hand & OVRInput.Controller.LTouch) != 0) action(0);
            if ((hand & OVRInput.Controller.RTouch) != 0) action(1);
        }

        static void Ensure()
        {
            if (_updater != null) return;
            var go = new GameObject("Haptics");
            Object.DontDestroyOnLoad(go);
            _updater = go.AddComponent<Updater>();
        }

        class Updater : MonoBehaviour
        {
            void LateUpdate()
            {
                for (int i = 0; i < 2; i++)
                {
                    var hand = i == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
                    ref var ch = ref Channels[i];
                    bool pulse = Time.unscaledTime < ch.Until;
                    float freq = pulse ? ch.Frequency : 0.2f;
                    float amp = pulse ? Mathf.Max(ch.Amplitude, ch.Rumble) : ch.Rumble;
                    bool on = amp > 0.001f;
                    // the controller keeps its last command for about 2 s, so refresh while on, stop once when off
                    if (on) OVRInput.SetControllerVibration(freq, amp, hand);
                    else if (ch.Active) OVRInput.SetControllerVibration(0f, 0f, hand);
                    ch.Active = on;
                }
            }

            void OnDisable() => OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
            void OnApplicationPause(bool paused) { if (paused) StopAll(); }
        }
    }
}
