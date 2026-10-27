using System.Collections;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Seated play: the camera rig sits at the cockpit's eye point and tracking uses eye level, so after a
    /// recenter your head is exactly at the pilot's eyes, facing forward. Recenters once shortly after
    /// start, and whenever you hold the left thumbstick click (the system's own recenter, holding the
    /// Meta button, works too). Each recenter hides the jump behind a very short fade.
    /// </summary>
    public class SeatedRecenter : MonoBehaviour
    {
        public static SeatedRecenter Instance { get; private set; }

        float _held;
        bool _busy;

        void Awake() => Instance = this;

        IEnumerator Start()
        {
            float delay = GameConfig.Instance.comfort.autoRecenterDelay;
            if (delay <= 0f) yield break;
            yield return new WaitForSecondsRealtime(delay);
            Recenter(withFade: false);   // still black from the start fade
        }

        void Update()
        {
            if (OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))
            {
                _held += Time.unscaledDeltaTime;
                if (_held >= GameConfig.Instance.comfort.recenterHoldSeconds && !_busy)
                {
                    _held = float.NegativeInfinity;   // once per press
                    StartCoroutine(RecenterWithFade());
                }
            }
            else _held = 0f;
        }

        IEnumerator RecenterWithFade()
        {
            _busy = true;
            float t = GameConfig.Instance.comfort.recenterFadeSeconds;
            var fader = ScreenFader.Instance;
            if (fader != null) yield return fader.FadeOut(t);
            Recenter(withFade: false);
            yield return null;   // let tracking apply the new origin before showing the view
            if (fader != null) yield return fader.FadeIn(t);
            _busy = false;
        }

        public void Recenter(bool withFade)
        {
            if (withFade) { if (!_busy) StartCoroutine(RecenterWithFade()); return; }
            if (OVRManager.display != null) OVRManager.display.RecenterPose();
            Debug.Log("[HexWing] Recentered to the cockpit eye point.");
        }
    }
}
