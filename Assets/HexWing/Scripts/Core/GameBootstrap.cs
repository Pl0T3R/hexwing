using System.Collections;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// M1 flow: the view starts black, the rig recenters to the eye point, then the cockpit fades in.
    /// Later milestones replace this with the menu and match flow; the start/end fades stay.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        IEnumerator Start()
        {
            // let the recenter (SeatedRecenter, after its short delay) happen while the view is still black
            yield return new WaitForSecondsRealtime(GameConfig.Instance.comfort.autoRecenterDelay + 0.1f);
            var fader = ScreenFader.Instance;
            if (fader != null) fader.FadeIn(GameConfig.Instance.comfort.fadeSeconds);
            Debug.Log("[HexWing] M1 cockpit scene running.");
        }
    }
}
