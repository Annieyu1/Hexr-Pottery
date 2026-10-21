using System.Collections;
using System.Collections.Generic;
using HexR;
using Oculus.Interaction;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// The ball's physics outside of grabbing:
    /// - puts it back where it started when it drops below a safety height (never while it is held),
    /// - ResetNow() for Replay / Menu,
    /// - SetHeld() for scripted moves (the Stage 1.2 split),
    /// - while the stage is paused: frozen in place, not grabbable, no glove pressure.
    /// Kinematic changes go through Meta's reference-counted LockKinematic so they never fight the Grabbable.
    /// </summary>
    public class BallSafetyReset : MonoBehaviour
    {
        [Header("Reset")]
        [Tooltip("Optional. Empty = the pose the object had when the scene started.")]
        [SerializeField] private Transform resetPoint;

        [Header("Safety Height")]
        [Tooltip("Table height - 0.3 m.")]
        [SerializeField] private float minimumY;

        [Header("Reset Settings")]
        [SerializeField] private float resetCooldown = 1f;

        private Rigidbody rb;
        private GrabNotifier grab;
        private HexRInteractableHaptics haptics;
        private readonly List<MonoBehaviour> interactables = new List<MonoBehaviour>();

        private bool initialized;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private bool isResetting;

        // One kinematic lock per reason, each taken and given back exactly once.
        private bool resetLock;
        private bool holdLock;
        private bool pauseLock;
        private Vector3 pausedVelocity;
        private Vector3 pausedAngularVelocity;

        private void Awake()
        {
            Init();
        }

        /// <summary>Also called from ResetNow, which can run before the ball was ever active.</summary>
        private void Init()
        {
            if (initialized)
                return;
            initialized = true;

            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = GetComponentInChildren<Rigidbody>(true);
            grab = GetComponent<GrabNotifier>();
            haptics = GetComponentInChildren<HexRInteractableHaptics>(true);
            foreach (MonoBehaviour mb in GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is IInteractable)
                    interactables.Add(mb);

            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void OnEnable()
        {
            StagePause.PausedChanged += OnPausedChanged;
            OnPausedChanged(StagePause.IsPausedNow);
        }

        private void OnDisable()
        {
            StagePause.PausedChanged -= OnPausedChanged;

            // A reset coroutine stopped by the deactivation cannot give its lock back.
            isResetting = false;
            SetLock(ref resetLock, false);
            RefreshInteraction();
        }

        private void Update()
        {
            if (isResetting || pauseLock || holdLock)
                return;

            if (grab != null && grab.IsGrabbed)
                return;

            if (transform.position.y < minimumY)
            {
                Debug.Log("[BallSafetyReset] " + name + " fell below " + minimumY.ToString("0.00") + ", reset");
                StartCoroutine(ResetRoutine());
            }
        }

        /// <summary>Back to the start pose now, used by Replay and Menu. Drops a hand that holds it.</summary>
        public void ResetNow()
        {
            Init();
            StopAllCoroutines();
            isResetting = false;
            SetLock(ref resetLock, false);
            SetLock(ref holdLock, false);   // Menu during the Stage 1.2 split

            if (isActiveAndEnabled)
            {
                StartCoroutine(ResetRoutine());
            }
            else
            {
                StopMotion();
                MoveToResetPose();
                RefreshInteraction();
            }
        }

        /// <summary>Scripted move (Stage 1.2 split): kinematic, not grabbable, no safety reset.</summary>
        public void SetHeld(bool held)
        {
            Init();
            if (held)
                StopMotion();

            SetLock(ref holdLock, held);
            RefreshInteraction();

            if (!held && rb != null && !rb.isKinematic)
                rb.WakeUp();
        }

        private IEnumerator ResetRoutine()
        {
            isResetting = true;

            StopMotion();
            SetLock(ref resetLock, true);
            RefreshInteraction();   // lets go of a hand that still holds it
            MoveToResetPose();

            yield return new WaitForFixedUpdate();

            SetLock(ref resetLock, false);
            RefreshInteraction();
            if (rb != null && !rb.isKinematic)
                rb.WakeUp();

            yield return StagePause.Wait(resetCooldown);
            isResetting = false;
        }

        private void OnPausedChanged(bool paused)
        {
            if (paused == pauseLock)
                return;

            if (paused)
            {
                bool moving = rb != null && !rb.isKinematic;
                pausedVelocity = moving ? rb.linearVelocity : Vector3.zero;
                pausedAngularVelocity = moving ? rb.angularVelocity : Vector3.zero;

                // Lock before dropping the hands, so the Grabbable's release cannot throw it.
                SetLock(ref pauseLock, true);
                RefreshInteraction();
            }
            else
            {
                SetLock(ref pauseLock, false);
                if (rb != null && !rb.isKinematic)
                {
                    rb.linearVelocity = pausedVelocity;
                    rb.angularVelocity = pausedAngularVelocity;
                    rb.WakeUp();
                }
                RefreshInteraction();
            }
        }

        /// <summary>Grabbing is on unless paused, held by a script or being reset. Glove pressure is off while paused.</summary>
        private void RefreshInteraction()
        {
            bool on = !pauseLock && !holdLock && !resetLock;

            // Mute before switching off (the hands are cancelled right away), un-mute after switching on.
            if (!on && grab != null)
                grab.Muted = true;

            foreach (MonoBehaviour mb in interactables)
                if (mb != null)
                    mb.enabled = on;

            if (haptics != null)
                haptics.enabled = !pauseLock;

            if (on && grab != null)
                grab.Muted = false;
        }

        private void SetLock(ref bool locked, bool value)
        {
            if (locked == value || rb == null)
                return;

            locked = value;
            if (value)
                rb.LockKinematic();
            else
                rb.UnlockKinematic();
        }

        private void StopMotion()
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            pausedVelocity = Vector3.zero;
            pausedAngularVelocity = Vector3.zero;
        }

        private void MoveToResetPose()
        {
            Vector3 position = resetPoint != null ? resetPoint.position : startPosition;
            Quaternion rotation = resetPoint != null ? resetPoint.rotation : startRotation;

            transform.SetPositionAndRotation(position, rotation);
            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
            }
        }
    }
}
