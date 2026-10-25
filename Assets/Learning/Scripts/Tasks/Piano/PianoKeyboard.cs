using TMPro;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// Five playable keys (C..G). The keyboard's origin is the fingertip line at key-top height
    /// (+X to the patient's right, +Z away from the patient). Each key mesh sits under a pivot at
    /// the key's back edge; a press tips the pivot so the front goes down.
    ///
    /// Place() moves the keyboard under the patient's resting fingertips (so the keys line up with
    /// the real table) and spaces the keys to the patient's own finger spacing.
    /// Model mode (modelRoot set, e.g. Assets/Art/Props/T2/T2_Keyboard.fbx): the whole model is
    /// scaled in X to spacing / modelPitch and only the five key pivots tip; nothing else moves.
    /// The scale is capped so the keyboard stays on the desk (maxModelWidth, 2026-10-25); with a wide
    /// hand the five keys are then a little narrower than the fingers, which does not matter for the
    /// judgment (key = finger). The model's black keys are one merged mesh, so they stretch with it.
    /// </summary>
    public class PianoKeyboard : MonoBehaviour
    {
        [Tooltip("Pivots at the back edge of the C, D, E, F, G keys.")]
        [SerializeField] private Transform[] keyPivots = new Transform[5];
        [SerializeField] private Renderer[] keyRenderers = new Renderer[5];
        [Tooltip("Decorative black keys between C-D, D-E, F-G.")]
        [SerializeField] private Transform[] blackKeys = new Transform[3];
        [SerializeField] private Transform keyBase;
        [SerializeField] private TMP_Text[] labels = new TMP_Text[5];
        [SerializeField] private Material keyMaterial;
        [SerializeField] private Material keyLitMaterial;
        [SerializeField] private float pressDegrees = 4f;
        [SerializeField] private float keyLength = 0.13f;
        [SerializeField] private float keyHeight = 0.018f;
        [Tooltip("How far the key front reaches toward the patient past the fingertips (m).")]
        [SerializeField] private float frontOverhang = 0.035f;
        [SerializeField] private float spacing = 0.022f;
        [Tooltip("Resize the grey-box key meshes in Layout (turn off for a modelled keyboard).")]
        [SerializeField] private bool resizeMeshes = true;
        [Tooltip("Model mode: the imported keyboard (wrapper under this object). Empty = grey-box keys.")]
        [SerializeField] private Transform modelRoot;
        [Tooltip("White-key pitch the model was built with (m).")]
        [SerializeField] private float modelPitch = 0.022f;
        [Tooltip("Model width at scale 1 (m): T2_Keyboard.fbx, 29 white keys plus the case.")]
        [SerializeField] private float modelWidth = 0.668f;
        [Tooltip("The keyboard is never scaled wider than this (m); the study-room desk is 0.96 m.")]
        [SerializeField] private float maxModelWidth = 0.75f;
        [Tooltip("Grey-box black key width (m); it keeps this width at any key spacing.")]
        [SerializeField] private float blackKeyWidth = 0.011f;

        private readonly bool[] down = new bool[5];
        private Quaternion[] rest;
        private float[] tipSign;

        public bool IsModel => modelRoot != null;

        private void Awake() { CaptureRest(); }

        /// <summary>Rest rotation of each key, and which way round its local X is (model keys can be mirrored).</summary>
        private void CaptureRest()
        {
            rest = new Quaternion[keyPivots.Length];
            tipSign = new float[keyPivots.Length];
            for (int i = 0; i < keyPivots.Length; i++)
            {
                if (keyPivots[i] == null) continue;
                rest[i] = IsModel ? keyPivots[i].localRotation : Quaternion.identity;
                tipSign[i] = Vector3.Dot(keyPivots[i].rotation * Vector3.right, transform.right) >= 0f ? 1f : -1f;
            }
        }

        private Quaternion PressedRotation(int k, bool isDown)
        {
            if (rest == null || rest.Length != keyPivots.Length) CaptureRest();
            return isDown ? rest[k] * Quaternion.Euler(-pressDegrees * tipSign[k], 0f, 0f) : rest[k];
        }

        /// <summary>Key spacing from the patient's fingertips (m).</summary>
        public float Spacing => spacing;
        /// <summary>Key spacing as drawn (m): the same as Spacing unless the model hit maxModelWidth.</summary>
        public float VisualSpacing => IsModel ? modelPitch * ModelScale() : spacing;

        private float ModelScale()
        {
            float s = spacing / Mathf.Max(0.001f, modelPitch);
            return Mathf.Min(s, maxModelWidth / Mathf.Max(0.01f, modelWidth));
        }

        /// <summary>
        /// Key k sits under tipsByKey[k]; key tops end padOffset below the fingertip joints (mean height).
        /// The keys run square to fingerDir (the way the hand points, flattened), so a hand pointing
        /// straight ahead gets a straight keyboard even though the fingertips form an arc. Spacing and the
        /// position of key E come from the fingertips projected on the key line, leaving out thumbKey
        /// (the thumb tip sits further back). Without a usable fingerDir the line through the tips is used.
        /// </summary>
        public void Place(Vector3[] tipsByKey, float padOffset, int thumbKey = -1, Vector3 fingerDir = default)
        {
            float y = 0f;
            for (int k = 0; k < 5; k++) y += tipsByKey[k].y;
            y /= 5f;
            int first = thumbKey == 0 ? 1 : 0, last = thumbKey == 4 ? 3 : 4;

            Vector3 forward = new Vector3(fingerDir.x, 0f, fingerDir.z);
            Vector3 acrossDir;
            if (forward.magnitude > 0.1f)
            {
                forward.Normalize();
                acrossDir = Vector3.Cross(Vector3.up, forward);
            }
            else
            {
                Vector3 line = tipsByKey[last] - tipsByKey[first];
                line.y = 0f;
                acrossDir = line.magnitude > 0.02f ? line.normalized : transform.right;
                forward = Vector3.Cross(acrossDir, Vector3.up);
            }
            float span = Vector3.Dot(tipsByKey[last] - tipsByKey[first], acrossDir);
            if (span > 0.02f) spacing = Mathf.Clamp(span / (last - first), 0.014f, 0.035f);

            // key E: mean of the fitted fingertips, slid along the key line to key 2
            Vector3 c = Vector3.zero;
            for (int k = first; k <= last; k++) c += tipsByKey[k];
            c /= last - first + 1;
            c += acrossDir * ((2f - (first + last) * 0.5f) * spacing);
            transform.SetPositionAndRotation(new Vector3(c.x, y - padOffset, c.z), Quaternion.LookRotation(forward, Vector3.up));
            Layout();
        }

        public void Layout()
        {
            float back = keyLength - frontOverhang;
            if (IsModel)
            {
                Vector3 sc = modelRoot.localScale;
                modelRoot.localScale = new Vector3(ModelScale(), sc.y, sc.z);
                float drawn = VisualSpacing;
                for (int k = 0; k < labels.Length; k++)
                    if (labels[k] != null) labels[k].transform.localPosition = new Vector3((k - 2) * drawn, 0.035f, back - 0.02f);
                return;
            }
            for (int k = 0; k < 5; k++)
            {
                if (keyPivots[k] != null)
                {
                    keyPivots[k].localPosition = new Vector3((k - 2) * spacing, 0f, back);
                    keyPivots[k].localRotation = PressedRotation(k, down[k]);
                }
                if (resizeMeshes && keyRenderers[k] != null)
                {
                    Transform m = keyRenderers[k].transform;
                    m.localPosition = new Vector3(0f, -keyHeight * 0.5f, -keyLength * 0.5f);
                    m.localRotation = Quaternion.identity;
                    m.localScale = new Vector3(spacing - 0.0015f, keyHeight, keyLength);
                }
                if (labels[k] != null) labels[k].transform.localPosition = new Vector3((k - 2) * spacing, 0.035f, back - 0.02f);
            }
            int[] between = { 0, 1, 3 };
            for (int j = 0; j < blackKeys.Length && j < between.Length; j++)
            {
                if (blackKeys[j] == null) continue;
                blackKeys[j].localPosition = new Vector3((between[j] - 1.5f) * spacing, 0.006f, back - 0.04f);
                blackKeys[j].localRotation = Quaternion.identity;
                if (resizeMeshes) blackKeys[j].localScale = new Vector3(Mathf.Min(blackKeyWidth, spacing * 0.6f), 0.014f, 0.08f);
            }
            if (keyBase != null && resizeMeshes)
            {
                keyBase.localPosition = new Vector3(0f, -keyHeight - 0.005f, keyLength * 0.5f - frontOverhang);
                keyBase.localRotation = Quaternion.identity;
                keyBase.localScale = new Vector3(5f * spacing + 0.03f, 0.01f, keyLength + 0.03f);
            }
        }

        public void SetPressed(int key, bool isDown)
        {
            if (key < 0 || key >= keyPivots.Length || keyPivots[key] == null) return;
            down[key] = isDown;
            keyPivots[key].localRotation = PressedRotation(key, isDown);
        }

        /// <summary>Visual cue: the key to play glows.</summary>
        public void SetLit(int key, bool on)
        {
            if (key < 0 || key >= keyRenderers.Length || keyRenderers[key] == null) return;
            keyRenderers[key].sharedMaterial = on && keyLitMaterial != null ? keyLitMaterial : keyMaterial;
        }

        public void SetLabels(string[] text)
        {
            for (int k = 0; k < labels.Length && k < text.Length; k++)
                if (labels[k] != null) labels[k].text = text[k];
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < keyPivots.Length; i++) SetPressed(i, false);
        }

        public void ClearLights()
        {
            for (int i = 0; i < keyRenderers.Length; i++) SetLit(i, false);
        }
    }
}
