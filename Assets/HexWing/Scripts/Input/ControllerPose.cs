using UnityEngine;
using UnityEngine.XR;

namespace HexWing
{
    /// <summary>
    /// A controller's pose in the rig's tracking space. OVRInput first; if it gives nothing (a zero
    /// position with an identity rotation, which is what it returns when its pose source isn't running),
    /// Unity's XR input devices from the OpenXR controller profiles, which don't go through OVRPlugin.
    /// Both are in the same tracking space, and the schemes only use movement from a grab or neutral pose.
    /// </summary>
    public static class ControllerPose
    {
        public enum Source { None, OVRInput, UnityXR }

        static readonly Source[] _last = new Source[2];

        /// <summary>Where the last pose of this hand came from (for the input readout and log).</summary>
        public static Source LastSource(OVRInput.Controller hand) => _last[Index(hand)];

        public static bool Get(OVRInput.Controller hand, out Vector3 position, out Quaternion rotation)
        {
            int i = Index(hand);
            position = OVRInput.GetLocalControllerPosition(hand);
            rotation = OVRInput.GetLocalControllerRotation(hand);
            if (position != Vector3.zero || rotation != Quaternion.identity)
            {
                _last[i] = Source.OVRInput;
                return true;
            }

            var device = InputDevices.GetDeviceAtXRNode(i == 0 ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid &&
                device.TryGetFeatureValue(CommonUsages.devicePosition, out var p) &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out var r))
            {
                position = p;
                rotation = r;
                _last[i] = Source.UnityXR;
                return true;
            }
            _last[i] = Source.None;
            return false;
        }

        static int Index(OVRInput.Controller hand) => hand == OVRInput.Controller.LTouch ? 0 : 1;

        public static string Label(Source s) => s switch
        {
            Source.OVRInput => "OVR",
            Source.UnityXR => "XR",
            _ => "NONE",
        };
    }
}
