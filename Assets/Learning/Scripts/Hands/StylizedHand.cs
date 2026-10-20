using UnityEngine;

namespace HexRMeta.Learning
{
    public enum HandFinger { Thumb = 0, Index = 1, Middle = 2, Ring = 3, Little = 4 }

    /// <summary>
    /// Grey-box hand built from primitives, with three joints per finger that curl toward the
    /// palm. Local frame: wrist at the origin, fingers along +Z, back of the hand +Y, palm -Y.
    ///
    /// Placeholder for the Blender hand: an imported model can use this same component by
    /// filling 'joints' (3 per finger, thumb first) and 'contacts' (5 fingertip pads + palm)
    /// by hand instead of pressing Build. Joints must flex toward the palm about their local X.
    /// </summary>
    public class StylizedHand : MonoBehaviour
    {
        [SerializeField] private HandSide side = HandSide.Right;
        [SerializeField] private Material material;
        [Tooltip("Thumb x3, index x3, middle x3, ring x3, little x3 (proximal to distal).")]
        [SerializeField] private Transform[] joints = new Transform[15];
        [Tooltip("Fingertip pads thumb..little, then palm. Highlights and contact anchors sit here.")]
        [SerializeField] private Transform[] contacts = new Transform[6];
        [Tooltip("Curl when nothing else is asked, 0 flat to 1 fist.")]
        [Range(0f, 1f)] [SerializeField] private float restCurl = 0.2f;
        [SerializeField] private float curlSpeed = 6f;
        [Tooltip("Thumb curl is multiplied by this (a real handshake barely moves the thumb).")]
        [Range(0f, 1f)] [SerializeField] private float thumbCurlScale = 1f;

        // Max flexion per joint (deg) at curl = 1.
        private static readonly float[][] MaxAngles =
        {
            new[] { 35f, 40f, 50f },    // thumb
            new[] { 75f, 95f, 65f },
            new[] { 75f, 95f, 65f },
            new[] { 75f, 95f, 65f },
            new[] { 75f, 95f, 65f },
        };

        // Joint rotations with the fingers straight (curl 0). Saved at Build time, or captured
        // with "Capture straight pose" for an imported model.
        [SerializeField, HideInInspector] private Quaternion[] baseRotations;

        // Per-joint flex axis in the joint's local frame (imported rigs). Empty = local X (grey box).
        [SerializeField, HideInInspector] private Vector3[] flexAxes;

        private readonly float[] curl = new float[5];
        private readonly float[] target = new float[5];

        public HandSide Side => side;
        public bool IsBuilt => joints != null && joints.Length == 15 && joints[14] != null;

        /// <summary>Fingertip pad (0–4) or palm (5) in GloveChannels order.</summary>
        public Transform Contact(int channelIndex)
        {
            return contacts != null && channelIndex >= 0 && channelIndex < contacts.Length ? contacts[channelIndex] : null;
        }

        /// <summary>Joint j (0 = knuckle … 2 = last) of a finger.</summary>
        public Transform Joint(HandFinger finger, int j)
        {
            int i = (int)finger * 3 + Mathf.Clamp(j, 0, 2);
            return joints != null && i < joints.Length ? joints[i] : null;
        }

        private void OnEnable()
        {
            if (baseRotations == null || baseRotations.Length != 15) CaptureStraightPose();
            for (int f = 0; f < 5; f++) { curl[f] = restCurl; target[f] = restCurl; }
            Apply();
        }

        private void Update()
        {
            if (baseRotations == null || baseRotations.Length != 15) return;
            float k = 1f - Mathf.Exp(-curlSpeed * Time.deltaTime);
            for (int f = 0; f < 5; f++) curl[f] = Mathf.Lerp(curl[f], target[f], k);
            Apply();
        }

        public void SetCurl(HandFinger finger, float value) { target[(int)finger] = Mathf.Clamp01(value); }

        public void SetCurlAll(float value)
        {
            for (int f = 0; f < 5; f++) target[f] = Mathf.Clamp01(value);
        }

        public void SetCurlImmediate(HandFinger finger, float value)
        {
            target[(int)finger] = curl[(int)finger] = Mathf.Clamp01(value);
            Apply();
        }

        public void SetCurlAllImmediate(float value)
        {
            for (int f = 0; f < 5; f++) target[f] = curl[f] = Mathf.Clamp01(value);
            Apply();
        }

        public void ResetToRest() { SetCurlAll(restCurl); }

        public float ThumbCurlScale { get => thumbCurlScale; set => thumbCurlScale = Mathf.Clamp01(value); }

        /// <summary>Swing the whole thumb sideways (deg, about the hand's up axis). Builder use.</summary>
        public void AdjustThumbYaw(float degrees)
        {
            if (baseRotations == null || baseRotations.Length != 15) return;
            baseRotations[0] = Quaternion.AngleAxis(degrees, Vector3.up) * baseRotations[0];
            Apply();
        }

        public void SetMaterial(Material m)
        {
            material = m;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.StartsWith("Mesh_")) r.sharedMaterial = m;
        }

        public void SetVisible(bool on)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.StartsWith("Mesh_")) r.enabled = on;
        }

        /// <summary>
        /// Use an imported, rigged model: 15 finger joints (thumb first, proximal to distal), 6 contacts
        /// (fingertip pads thumb..little, then palm) and the axis each joint flexes about toward the palm.
        /// The current pose becomes "fingers straight".
        /// </summary>
        public void ConfigureImported(Transform[] fingerJoints, Transform[] contactPoints, Vector3[] jointFlexAxes)
        {
            joints = fingerJoints;
            contacts = contactPoints;
            flexAxes = jointFlexAxes;
            CaptureStraightPose();
        }

        /// <summary>Use the current joint rotations as "fingers straight" (for an imported model).</summary>
        [ContextMenu("Capture straight pose")]
        public void CaptureStraightPose()
        {
            if (!IsBuilt) { baseRotations = null; return; }
            baseRotations = new Quaternion[15];
            for (int i = 0; i < 15; i++) baseRotations[i] = joints[i].localRotation;
        }

        private void Apply()
        {
            if (baseRotations == null || baseRotations.Length != 15) return;
            for (int f = 0; f < 5; f++)
                for (int j = 0; j < 3; j++)
                {
                    Transform t = joints[f * 3 + j];
                    if (t == null) continue;
                    int i = f * 3 + j;
                    Vector3 axis = flexAxes != null && flexAxes.Length == 15 ? flexAxes[i] : Vector3.right;
                    float c = f == 0 ? curl[f] * thumbCurlScale : curl[f];
                    t.localRotation = baseRotations[i] * Quaternion.AngleAxis(MaxAngles[f][j] * c, axis);
                }
        }

        // ------------------------------------------------------------------ grey-box build

        /// <summary>Rebuild the primitive hand under this object (editor or runtime).</summary>
        [ContextMenu("Build grey-box hand")]
        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            joints = new Transform[15];
            contacts = new Transform[6];
            flexAxes = null;
            float s = side == HandSide.Right ? 1f : -1f;   // right-hand thumb is on -X

            // palm
            MakeMesh(PrimitiveType.Sphere, "Mesh_Palm", transform,
                     new Vector3(0f, 0f, 0.047f), Quaternion.identity, new Vector3(0.086f, 0.03f, 0.104f));
            contacts[5] = MakeMarker("Contact_Palm", transform, new Vector3(0f, -0.016f, 0.05f));

            // four fingers: index, middle, ring, little
            float[] baseX = { -0.027f, -0.009f, 0.009f, 0.026f };
            float[] baseZ = { 0.088f, 0.093f, 0.089f, 0.082f };
            float[][] len =
            {
                new[] { 0.040f, 0.025f, 0.020f },
                new[] { 0.044f, 0.028f, 0.022f },
                new[] { 0.041f, 0.026f, 0.021f },
                new[] { 0.032f, 0.020f, 0.018f },
            };
            float[] radius = { 0.0085f, 0.0088f, 0.0083f, 0.0075f };
            string[] names = { "Index", "Middle", "Ring", "Little" };
            for (int f = 0; f < 4; f++)
            {
                Vector3 basePos = new Vector3(s * baseX[f], 0f, baseZ[f]);
                BuildChain(names[f], f + 1, basePos, Quaternion.identity, len[f], radius[f]);
            }

            // thumb: out to the side, rolled so it flexes across the palm
            BuildChain("Thumb", 0, new Vector3(s * -0.028f, -0.01f, 0.028f),
                       Quaternion.Euler(0f, s * -15f, s * 40f), new[] { 0.034f, 0.030f, 0.025f }, 0.0095f);

            CaptureStraightPose();
            for (int f = 0; f < 5; f++) curl[f] = target[f] = restCurl;
            Apply();
        }

        private void BuildChain(string name, int finger, Vector3 basePos, Quaternion baseRot, float[] lengths, float r)
        {
            Transform parent = transform;
            Vector3 pos = basePos;
            Quaternion rot = baseRot;
            for (int j = 0; j < 3; j++)
            {
                var pivot = new GameObject(name + "_" + (j + 1)).transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = pos;
                pivot.localRotation = rot;
                float L = lengths[j];
                float rr = r * (1f - 0.08f * j);
                MakeMesh(PrimitiveType.Capsule, "Mesh_" + name + "_" + (j + 1), pivot,
                         new Vector3(0f, 0f, L * 0.5f), Quaternion.Euler(90f, 0f, 0f),
                         new Vector3(rr * 2f, L * 0.5f + rr * 0.5f, rr * 2f));
                joints[finger * 3 + j] = pivot;
                parent = pivot;
                pos = new Vector3(0f, 0f, L);
                rot = Quaternion.identity;
                if (j == 2) contacts[finger] = MakeMarker("Contact_" + name, pivot, new Vector3(0f, -rr * 0.7f, L * 0.7f));
            }
        }

        private GameObject MakeMesh(PrimitiveType type, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Transform MakeMarker(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }
    }
}
