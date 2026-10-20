using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// T1 guide: a glowing ghost of the patient's hand in the handshake pose at the waiting pad, and
    /// a floating arrow above it pointing down. Shown while the model hand waits to be held.
    /// The ghost breathes (alpha) and the arrow bobs, both slowly.
    /// </summary>
    public class HandGuide : MonoBehaviour
    {
        [SerializeField] private Renderer[] ghostRenderers;
        [SerializeField] private Transform arrow;
        [SerializeField] private float period = 2.4f;
        [SerializeField] private float arrowBob = 0.015f;
        [Range(0f, 1f)] [SerializeField] private float ghostAlphaMin = 0.18f;
        [Range(0f, 1f)] [SerializeField] private float ghostAlphaMax = 0.45f;

        private MaterialPropertyBlock mpb;
        private Color ghostColor = Color.white;
        private Vector3 arrowHome;
        private float t;

        // where the ghost parts and the arrow were built, for FollowOffer
        private Transform[] parts;
        private Pose[] partHomes;
        private Vector3 arrowWorldHome;

        private void SaveHome()
        {
            if (parts != null) return;
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in transform)
                if (c != arrow) list.Add(c);
            parts = list.ToArray();
            partHomes = new Pose[parts.Length];
            for (int i = 0; i < parts.Length; i++) partHomes[i] = new Pose(parts[i].position, parts[i].rotation);
            if (arrow != null) arrowWorldHome = arrow.position;
        }

        /// <summary>
        /// The model hand's waiting marker moved from baseOffer (as built) to offer: the ghost hand
        /// moves rigidly with it; the arrow stays upright and follows the model palm (palmShift).
        /// </summary>
        public void FollowOffer(Pose baseOffer, Pose offer, Vector3 palmShift)
        {
            SaveHome();
            Quaternion d = offer.rotation * Quaternion.Inverse(baseOffer.rotation);
            for (int i = 0; i < parts.Length; i++)
                parts[i].SetPositionAndRotation(offer.position + d * (partHomes[i].position - baseOffer.position),
                                                d * partHomes[i].rotation);
            if (arrow != null)
            {
                arrow.position = arrowWorldHome + palmShift;
                arrowHome = arrow.localPosition;
            }
        }

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            if (ghostRenderers != null && ghostRenderers.Length > 0 && ghostRenderers[0] != null)
            {
                Material m = ghostRenderers[0].sharedMaterial;
                if (m != null && m.HasProperty("_BaseColor")) ghostColor = m.GetColor("_BaseColor");
            }
            if (arrow != null) arrowHome = arrow.localPosition;
        }

        private void OnEnable() { t = 0f; }

        public void Show(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        private void Update()
        {
            t += Time.deltaTime;
            float s = 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / Mathf.Max(0.5f, period));
            if (ghostRenderers != null)
            {
                Color c = ghostColor;
                c.a = Mathf.Lerp(ghostAlphaMin, ghostAlphaMax, s);
                mpb.SetColor("_BaseColor", c);
                foreach (Renderer r in ghostRenderers)
                    if (r != null) r.SetPropertyBlock(mpb);
            }
            if (arrow != null) arrow.localPosition = arrowHome + Vector3.up * (arrowBob * s);
        }

        /// <summary>A downward arrow (cone head + round shaft), tip at the origin, length 'length' up +Y.</summary>
        public static Mesh BuildArrowMesh(float length = 0.07f, float headLength = 0.03f, float headRadius = 0.018f,
                                          float shaftRadius = 0.007f, int sides = 24)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            void Ring(float y, float r)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
            }
            void Band(int a, int b)   // connect ring a to ring b (b above a)
            {
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    tris.Add(a + i); tris.Add(b + i); tris.Add(b + j);
                    tris.Add(a + i); tris.Add(b + j); tris.Add(a + j);
                }
            }
            void Cap(int ring, Vector3 centre, bool up)
            {
                int c = verts.Count;
                verts.Add(centre);
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    if (up) { tris.Add(c); tris.Add(ring + j); tris.Add(ring + i); }
                    else { tris.Add(c); tris.Add(ring + i); tris.Add(ring + j); }
                }
            }
            // tip (as a tiny ring so normals stay smooth), head base, shaft bottom, shaft top
            int tip = verts.Count; Ring(0f, 0.0005f);
            int head = verts.Count; Ring(headLength, headRadius);
            Band(tip, head);
            Cap(head, new Vector3(0f, headLength, 0f), true);
            int sb = verts.Count; Ring(headLength, shaftRadius);
            int st = verts.Count; Ring(length, shaftRadius);
            Band(sb, st);
            Cap(st, new Vector3(0f, length, 0f), true);
            var mesh = new Mesh { name = "GuideArrow" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
