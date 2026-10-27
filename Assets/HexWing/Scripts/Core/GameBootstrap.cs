using System.Collections;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Start-up and pausing: loads the player's settings and progress hooks, the view starts black, the
    /// rig recenters to the eye point, then the cockpit fades in (the hangar menu is already open). The
    /// game pauses while the app has no input focus (system menu open, headset taken off) and resumes when
    /// it comes back (unless a menu keeps it paused).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameBootstrap : MonoBehaviour
    {
        void Awake() => UserSettings.Load();   // before anything reads the config

        void OnEnable()
        {
            OVRManager.InputFocusLost += Pause;
            OVRManager.InputFocusAcquired += Resume;
            OVRManager.HMDUnmounted += Pause;
            OVRManager.HMDMounted += Resume;
        }

        void OnDisable()
        {
            OVRManager.InputFocusLost -= Pause;
            OVRManager.InputFocusAcquired -= Resume;
            OVRManager.HMDUnmounted -= Pause;
            OVRManager.HMDMounted -= Resume;
            GamePause.Set(GamePause.Reason.Focus, false);
        }

        IEnumerator Start()
        {
            Progression.Hook();
            // let the recenter (SeatedRecenter, after its short delay) happen while the view is still black
            yield return new WaitForSecondsRealtime(GameConfig.Instance.comfort.autoRecenterDelay + 0.1f);
            var fader = ScreenFader.Instance;
            if (fader != null) fader.FadeIn(GameConfig.Instance.comfort.fadeSeconds);
            Debug.Log("[HexWing] Flight scene running.");
        }

        static void Pause()
        {
            GamePause.Set(GamePause.Reason.Focus, true);
            AudioListener.pause = true;
            Debug.Log("[HexWing] Paused (no input focus or headset off).");
        }

        static void Resume()
        {
            GamePause.Set(GamePause.Reason.Focus, false);
            AudioListener.pause = false;
            Debug.Log("[HexWing] Resumed.");
        }
    }
}
