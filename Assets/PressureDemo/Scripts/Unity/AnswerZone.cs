using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexRMeta.Pressure
{
    /// <summary>
    /// The single "Stronger" pad. An object counts as the answer once it has been RELEASED inside
    /// the zone for confirmDwell seconds. Picking it back up before then is a change of mind.
    /// While one object is the candidate, the other is ignored.
    ///
    /// The zone needs a Collider with Is Trigger on. Objects have Rigidbodies, so no extra
    /// Rigidbody is needed here.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AnswerZone : MonoBehaviour
    {
        [SerializeField] private float confirmDwell = 0.5f;
        [Tooltip("Optional: shown while an object is waiting to be confirmed.")]
        [SerializeField] private GameObject dwellIndicator;

        public bool Active { get; private set; }
        public int ChangedMindCount { get; private set; }

        public event Action<PressureObject> Answered;

        private readonly Dictionary<PressureObject, int> inside = new Dictionary<PressureObject, int>();
        private PressureObject candidate;
        private float dwellTimer;

        public void Configure(float dwellSeconds)
        {
            confirmDwell = dwellSeconds;
        }

        public void SetActive(bool value)
        {
            Active = value;
            ClearCandidate(false);
            if (!value)
            {
                foreach (var kv in inside)
                    if (kv.Key != null) kv.Key.InAnswerZone = false;
            }
            else
            {
                foreach (var kv in inside)
                    if (kv.Key != null) kv.Key.InAnswerZone = true;
            }
        }

        public void ResetStats()
        {
            ChangedMindCount = 0;
        }

        private void OnTriggerEnter(Collider other)
        {
            PressureObject obj = other.GetComponentInParent<PressureObject>();
            if (obj == null) return;

            inside.TryGetValue(obj, out int count);
            inside[obj] = count + 1;
            if (Active) obj.InAnswerZone = true;
        }

        private void OnTriggerExit(Collider other)
        {
            PressureObject obj = other.GetComponentInParent<PressureObject>();
            if (obj == null) return;

            if (!inside.TryGetValue(obj, out int count)) return;
            count--;
            if (count <= 0)
            {
                inside.Remove(obj);
                obj.InAnswerZone = false;
            }
            else
            {
                inside[obj] = count;
            }
        }

        private void Update()
        {
            if (!Active) return;

            // Picked up again before the dwell finished = a change of mind. Leaving the zone
            // without being held (e.g. teleported home between trials) is not.
            if (candidate != null)
            {
                if (candidate.IsHeld) ClearCandidate(true);
                else if (!inside.ContainsKey(candidate)) ClearCandidate(false);
            }

            if (candidate == null)
            {
                foreach (var kv in inside)
                {
                    if (kv.Key != null && !kv.Key.IsHeld)
                    {
                        candidate = kv.Key;
                        dwellTimer = 0f;
                        if (dwellIndicator != null) dwellIndicator.SetActive(true);
                        break;
                    }
                }
                return;
            }

            dwellTimer += Time.unscaledDeltaTime;
            if (dwellTimer >= confirmDwell)
            {
                PressureObject answer = candidate;
                ClearCandidate(false);
                Active = false;                 // one answer per trial
                Answered?.Invoke(answer);
            }
        }

        private void ClearCandidate(bool countAsChangedMind)
        {
            if (candidate != null && countAsChangedMind)
                ChangedMindCount++;
            candidate = null;
            dwellTimer = 0f;
            if (dwellIndicator != null) dwellIndicator.SetActive(false);
        }
    }
}
