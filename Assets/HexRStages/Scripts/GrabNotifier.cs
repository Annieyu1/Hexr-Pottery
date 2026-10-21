using System;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// Turns the Meta Grabbable's pointer events into simple Grabbed / Released events.
    /// Replaces the PICO XRI select events (OnPicoGrab / OnPicoRelease, HandleGrab / HandleRelease).
    /// Several hands can hold the object: Grabbed fires on the first select, Released on the last.
    /// </summary>
    public class GrabNotifier : MonoBehaviour
    {
        [SerializeField] private Grabbable grabbable;

        private readonly HashSet<int> selecting = new HashSet<int>();

        public bool IsGrabbed => selecting.Count > 0;

        /// <summary>
        /// Set by BallSafetyReset while grabbing is switched off (pause, scripted move, reset).
        /// Hands are still tracked but no events fire, so a hand cancelled by the pause is not a squeeze.
        /// </summary>
        public bool Muted { get; set; }

        public event Action<GrabNotifier> Grabbed;
        public event Action<GrabNotifier> Released;

        private void Reset()
        {
            grabbable = GetComponentInChildren<Grabbable>();
        }

        private void OnEnable()
        {
            if (grabbable == null)
                grabbable = GetComponentInChildren<Grabbable>();

            if (grabbable != null)
                grabbable.WhenPointerEventRaised += HandlePointerEvent;
            else
                Debug.LogError("[GrabNotifier] No Grabbable on " + name);
        }

        private void OnDisable()
        {
            if (grabbable != null)
                grabbable.WhenPointerEventRaised -= HandlePointerEvent;

            // Hidden while held (stage switch): forget the hands without firing Released.
            selecting.Clear();
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            bool silent = Muted || StagePause.IsPausedNow;

            switch (evt.Type)
            {
                case PointerEventType.Select:
                    if (selecting.Add(evt.Identifier) && selecting.Count == 1)
                    {
                        Debug.Log("[GrabNotifier] " + name + (silent ? " grabbed (ignored)" : " GRABBED"));
                        if (!silent)
                            Grabbed?.Invoke(this);
                    }
                    break;

                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    if (selecting.Remove(evt.Identifier) && selecting.Count == 0)
                    {
                        Debug.Log("[GrabNotifier] " + name + (silent ? " released (ignored)" : " RELEASED"));
                        if (!silent)
                            Released?.Invoke(this);
                    }
                    break;
            }
        }
    }
}
