using System;
using System.Collections;
using HexR;
using Oculus.Interaction;
using UnityEngine;

namespace HexRMeta.Pressure
{
    /// <summary>
    /// One of the two identical objects. It carries the pressure the glove delivers while it is
    /// held, reports grabs and releases, and glides back to its pedestal when let go outside the
    /// answer zone. Replaces ForceBall / FruitItem.
    ///
    /// Needs on the same GameObject: Grabbable and HexRInteractableHaptics (as on the tutorial
    /// balls). The HandGrabInteractable child objects go into 'interactables' so they can be
    /// switched off while paused.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Grabbable))]
    [RequireComponent(typeof(HexRInteractableHaptics))]
    public class PressureObject : MonoBehaviour
    {
        [SerializeField] private Side side;
        [Tooltip("The HandGrabInteractable child objects. Disabled while the object is locked.")]
        [SerializeField] private GameObject[] interactables;
        [Tooltip("Optional glow shown on the correct object after a wrong answer.")]
        [SerializeField] private GameObject highlightVisual;
        [SerializeField] private float returnDuration = 0.35f;
        [SerializeField] private float homeTolerance = 0.02f;
        [SerializeField] private float fallBelowHomeY = 0.8f;

        public Side Side => side;
        public bool IsHeld => heldCount > 0;
        public bool InAnswerZone { get; set; }
        public float CurrentStrength => haptics.strength;

        // Per-trial stats, cleared by ResetStats().
        public int GrabCount { get; private set; }
        public float HoldSeconds { get; private set; }
        public int AutoReturnCount { get; private set; }

        public event Action<PressureObject> Grabbed;
        public event Action<PressureObject> Released;

        private Grabbable grabbable;
        private HexRInteractableHaptics haptics;
        private Rigidbody body;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private int heldCount;
        private float returnAt = -1f;
        private Coroutine returning;
        private bool bodyWasKinematic;
        private bool locked = true;

        private void Awake()
        {
            grabbable = GetComponent<Grabbable>();
            haptics = GetComponent<HexRInteractableHaptics>();
            body = GetComponent<Rigidbody>();
            homePosition = transform.position;
            homeRotation = transform.rotation;

            // Whole hand feels it; the object, not the grip, decides the pressure.
            haptics.fireOn = HexRInteractableHaptics.FireOn.Select;
            haptics.followGrip = false;
            haptics.thumb = haptics.index = haptics.middle = haptics.ring = haptics.pinky = haptics.palm = true;
        }

        private void OnEnable()
        {
            grabbable.WhenPointerEventRaised += OnPointerEvent;
        }

        private void OnDisable()
        {
            grabbable.WhenPointerEventRaised -= OnPointerEvent;
        }

        private void Update()
        {
            if (locked) return;

            if (IsHeld)
            {
                HoldSeconds += Time.unscaledDeltaTime;
                return;
            }

            // Fell off the table: back to the pedestal at once.
            if (transform.position.y < homePosition.y - fallBelowHomeY)
            {
                SnapHome();
                AutoReturnCount++;
                return;
            }

            if (returning == null && !InAnswerZone && returnAt >= 0f && Time.unscaledTime >= returnAt)
            {
                returnAt = -1f;
                if (AwayFromHome())
                {
                    AutoReturnCount++;
                    returning = StartCoroutine(GlideHome());
                }
            }
        }

        // ---------- public API used by the controller ----------

        /// <summary>The pressure this object delivers when grabbed (HexR strength, 10 to 60).</summary>
        public void SetStrength(float strength)
        {
            haptics.strength = strength;
        }

        public void ResetStats()
        {
            GrabCount = 0;
            HoldSeconds = 0f;
            AutoReturnCount = 0;
        }

        /// <summary>
        /// Locked = cannot be grabbed and the glove is silent (pause, feedback, menu).
        /// Locking also sends the object home.
        /// </summary>
        public void SetLocked(bool value)
        {
            SetLockedKeepPose(value);
            if (value)
            {
                InAnswerZone = false;
                SnapHome();
            }
        }

        /// <summary>Same as SetLocked, but the object stays where it is (feedback moment).</summary>
        public void SetLockedKeepPose(bool value)
        {
            locked = value;

            // Order matters. Unlocking: switch the grab interactables on first, so that when the
            // haptics component re-binds in OnEnable it can find them. Locking: haptics off first
            // so the glove pressure is released before the interactables disappear.
            if (value) haptics.enabled = false;     // OnDisable releases the glove pressure
            if (interactables != null)
                foreach (GameObject go in interactables)
                    if (go != null) go.SetActive(!value);
            if (!value) haptics.enabled = true;

            if (value)
                heldCount = 0;
        }

        public void SetHighlight(bool on)
        {
            if (highlightVisual != null) highlightVisual.SetActive(on);
        }

        public void SnapHome()
        {
            if (returning != null)
            {
                StopCoroutine(returning);
                returning = null;
            }
            returnAt = -1f;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        // ---------- grab handling ----------

        private void OnPointerEvent(PointerEvent evt)
        {
            if (locked) return;

            switch (evt.Type)
            {
                case PointerEventType.Select:
                    if (returning != null) CancelGlide();
                    heldCount++;
                    returnAt = -1f;
                    if (heldCount == 1)
                    {
                        GrabCount++;
                        Grabbed?.Invoke(this);
                    }
                    break;

                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    if (heldCount > 0) heldCount--;
                    if (heldCount == 0)
                    {
                        returnAt = Time.unscaledTime + ReturnDelay;
                        Released?.Invoke(this);
                    }
                    break;
            }
        }

        /// <summary>Set by the controller from PressureSettings.returnDelay.</summary>
        public float ReturnDelay { get; set; } = 0.5f;

        private bool AwayFromHome()
        {
            return (transform.position - homePosition).sqrMagnitude > homeTolerance * homeTolerance;
        }

        private IEnumerator GlideHome()
        {
            bodyWasKinematic = body != null && body.isKinematic;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, returnDuration);
                float k = Mathf.SmoothStep(0f, 1f, t);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(fromPos, homePosition, k),
                    Quaternion.Slerp(fromRot, homeRotation, k));
                yield return null;
            }

            transform.SetPositionAndRotation(homePosition, homeRotation);
            if (body != null) body.isKinematic = bodyWasKinematic;
            returning = null;
        }

        private void CancelGlide()
        {
            StopCoroutine(returning);
            returning = null;
            if (body != null) body.isKinematic = bodyWasKinematic;
        }
    }
}
