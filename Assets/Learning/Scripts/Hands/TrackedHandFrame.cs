using HexR;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// The patient's own VR hand, read from the HexR rig: palm, knuckles and fingertips of the
    /// tracked hand (the same joints that carry the HexR fingertip colliders). Gives a hand frame:
    /// palm position, finger direction, palm normal.
    ///
    /// When the hand is not tracked in the Editor (no headset, no simulator hands) an optional
    /// stand-in StylizedHand is used so the task can still be tried on a desktop. On the headset
    /// the stand-in is never shown.
    /// </summary>
    public class TrackedHandFrame : MonoBehaviour
    {
        [SerializeField] private HandSide side = HandSide.Right;
        [Tooltip("Editor only: used while the real hand is not tracked.")]
        [SerializeField] private StylizedHand editorStandIn;

        private HexRTrackedHand tracked;
        private OVRHand ovrHand;
        private Transform palm, wrist;
        private readonly Transform[] knuckles = new Transform[5];
        private readonly Transform[] tips = new Transform[5];
        private float nextResolve;

        public HandSide Side => side;
        public bool UsingStandIn { get; private set; }
        public StylizedHand StandIn => editorStandIn;

        /// <summary>True when there is a usable hand (tracked, or the Editor stand-in).</summary>
        public bool IsValid => UsingStandIn || RealTracked;

        private bool RealTracked => palm != null && knuckles[2] != null && (ovrHand == null || ovrHand.IsTracked);

        public void SetSide(HandSide s)
        {
            side = s;
            tracked = null;
        }

        private void Update()
        {
            if ((tracked == null || palm == null) && Time.unscaledTime >= nextResolve)
            {
                nextResolve = Time.unscaledTime + 1f;
                Resolve();
            }
            bool standIn = Application.isEditor && editorStandIn != null && !RealTracked;
            if (standIn != UsingStandIn || (editorStandIn != null && editorStandIn.gameObject.activeSelf != standIn))
            {
                UsingStandIn = standIn;
                if (editorStandIn != null) editorStandIn.gameObject.SetActive(standIn);
            }
        }

        private void Resolve()
        {
            var wanted = side == HandSide.Left ? HexRTrackedHand.HandType.Left : HexRTrackedHand.HandType.Right;
            foreach (HexRTrackedHand h in FindObjectsByType<HexRTrackedHand>(FindObjectsInactive.Include))
                if (h.handType == wanted) { tracked = h; break; }
            if (tracked == null || tracked.handRoot == null) return;

            palm = tracked.ResolveRawPalmJoint();
            Transform w = HexRTrackedHand.FindChildRecursive(tracked.handRoot.gameObject, (side == HandSide.Left ? "L_" : "R_") + "Wrist")?.transform;
            if (w == null) w = HexRTrackedHand.FindChildRecursive(tracked.handRoot.gameObject, "XRHand_Wrist")?.transform;
            wrist = w;
            var names = new[]
            {
                HapticFingerTrigger.FingerType.Thumb, HapticFingerTrigger.FingerType.Index, HapticFingerTrigger.FingerType.Middle,
                HapticFingerTrigger.FingerType.Ring, HapticFingerTrigger.FingerType.Little,
            };
            for (int f = 0; f < 5; f++)
            {
                knuckles[f] = tracked.ResolveKnuckleJoint(names[f]);
                tips[f] = tracked.ResolveRawFingerJoint(names[f]);
            }

            string ovrName = side == HandSide.Left ? "Left" : "Right";
            foreach (OVRHand o in FindObjectsByType<OVRHand>(FindObjectsInactive.Include))
                if (o.name.Contains(ovrName)) { ovrHand = o; break; }
        }

        // ------------------------------------------------------------------ frame

        /// <summary>Palm centre.</summary>
        public Vector3 Palm
        {
            get
            {
                if (UsingStandIn) return Mid(editorStandIn.Contact(5).position, editorStandIn.transform.position, 0.1f);
                return palm != null ? palm.position : transform.position;
            }
        }

        /// <summary>Along the fingers (wrist → middle knuckle).</summary>
        public Vector3 FingerDir
        {
            get
            {
                if (UsingStandIn) return editorStandIn.transform.forward;
                Vector3 from = wrist != null ? wrist.position : Palm;
                Vector3 d = Knuckle(2) - from;
                return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
            }
        }

        /// <summary>Out of the palm.</summary>
        public Vector3 PalmNormal
        {
            get
            {
                if (UsingStandIn) return -editorStandIn.transform.up;
                Vector3 across = Knuckle(1) - Knuckle(4);           // little → index
                Vector3 back = Vector3.Cross(across, FingerDir);     // back of a right hand
                if (side == HandSide.Left) back = -back;
                Vector3 n = -back;
                return n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.down;
            }
        }

        /// <summary>Hand frame: +Z along the fingers, +Y out of the back of the hand.</summary>
        public Quaternion Rotation
        {
            get
            {
                Vector3 f = FingerDir;
                Vector3 up = -PalmNormal;
                up = Vector3.ProjectOnPlane(up, f);
                return up.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f, up) : Quaternion.LookRotation(f);
            }
        }

        public Vector3 Knuckle(int finger)
        {
            if (UsingStandIn) return editorStandIn.Joint((HandFinger)finger, 0).position;
            return knuckles[finger] != null ? knuckles[finger].position : Palm;
        }

        public Vector3 Tip(int finger)
        {
            if (UsingStandIn) return editorStandIn.Contact(finger).position;
            return tips[finger] != null ? tips[finger].position : Palm;
        }

        /// <summary>
        /// How closed the hand is: mean angle (deg) between knuckle→tip and the finger direction for
        /// index..little. About 0–20 open, 50+ when wrapped around a hand, 100+ in a fist.
        /// </summary>
        public float GripCurlDegrees
        {
            get
            {
                Vector3 dir = FingerDir;
                float sum = 0f;
                for (int f = 1; f < 5; f++) sum += Vector3.Angle(Tip(f) - Knuckle(f), dir);
                return sum / 4f;
            }
        }

        /// <summary>A point along a finger, 0 = knuckle, 1 = tip.</summary>
        public Vector3 FingerPoint(int finger, float t) { return Vector3.Lerp(Knuckle(finger), Tip(finger), t); }

        /// <summary>Where a glow dot for glove channel i should sit (fingertip pads, then palm).</summary>
        public Transform Anchor(int channelIndex)
        {
            if (UsingStandIn) return editorStandIn.Contact(channelIndex);
            if (channelIndex == 5) return palm;
            return channelIndex >= 0 && channelIndex < 5 ? tips[channelIndex] : null;
        }

        private static Vector3 Mid(Vector3 a, Vector3 b, float t) { return Vector3.Lerp(a, b, t); }
    }
}
