using System.Collections;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Start-up and pausing: the view starts black, the rig recenters to the eye point, then the cockpit
    /// fades in. The game pauses (time scale 0) while the app has no input focus (system menu open,
    /// headset taken off) and resumes when it comes back. M8 adds the hangar menu in front of this.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
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
            Time.timeScale = 1f;
        }

        IEnumerator Start()
        {
            // let the recenter (SeatedRecenter, after its short delay) happen while the view is still black
            yield return new WaitForSecondsRealtime(GameConfig.Instance.comfort.autoRecenterDelay + 0.1f);
            var fader = ScreenFader.Instance;
            if (fader != null) fader.FadeIn(GameConfig.Instance.comfort.fadeSeconds);
            Debug.Log("[HexWing] M2 flight scene running.");
        }

        static void Pause()
        {
            Time.timeScale = 0f;
            Haptics.StopAll();
            AudioListener.pause = true;
            Debug.Log("[HexWing] Paused (no input focus or headset off).");
        }

        static void Resume()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Debug.Log("[HexWing] Resumed.");
        }
    }
}
