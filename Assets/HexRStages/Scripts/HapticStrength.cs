using System.Collections.Generic;
using HaptGlove;
using HexR;
using Oculus.Interaction;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// Glove pressure while the object is held, by design value: 10 = barely there, 60 = firm.
    ///
    /// Sent to the glove here instead of through HexRInteractableHaptics. In HexR 0.9.3 that
    /// component sends 0–60, but the glove encoder (Haptics.HEXRPressure) takes an intensity of
    /// 0.1–1 and clamps everything above 1, so every strength from 10 up arrived as full pressure
    /// and the balls all felt the same (2026-10-22, on Quest). The glove maps intensity 0.1–1 onto
    /// 15–50 kPa, so the design value is converted with that: 10 → 15 kPa (lowest), 35 → 35 kPa,
    /// 60 → 50 kPa (highest). HexRInteractableHaptics stays on the object only for its finger
    /// choice and is switched off.
    /// </summary>
    [DisallowMultipleComponent]
    public class HapticStrength : MonoBehaviour
    {
        private const int Left = 0, Right = 1;

        [SerializeField, Range(0f, 60f)] private float strength = 30f;
        [Tooltip("How fast the pressure builds, 0.1–1.")]
        [SerializeField, Range(0.1f, 1f)] private float speed = 1f;

        private Grabbable grabbable;
        private Haptics.Finger[] fingers;
        private readonly Dictionary<int, int> sideOfPointer = new Dictionary<int, int>();
        private readonly int[] holding = new int[2];

        public float Strength
        {
            get => strength;
            set
            {
                strength = value;
                for (int side = Left; side <= Right; side++)
                    if (holding[side] > 0)
                        Send(side, true);
            }
        }

        /// <summary>Glove intensity (0.1–1) for a pressure in kPa; the glove maps 0.1–1 onto 15–50 kPa.</summary>
        public static float ToIntensity(float kPa)
        {
            return Mathf.Clamp(0.1f + (kPa - 15f) / 35f * 0.9f, 0.1f, 1f);
        }

        private void Awake()
        {
            grabbable = GetComponentInChildren<Grabbable>(true);

            var hexr = GetComponent<HexRInteractableHaptics>();
            var list = new List<Haptics.Finger>();
            if (hexr == null || hexr.thumb) list.Add(Haptics.Finger.Thumb);
            if (hexr == null || hexr.index) list.Add(Haptics.Finger.Index);
            if (hexr == null || hexr.middle) list.Add(Haptics.Finger.Middle);
            if (hexr != null && hexr.ring) list.Add(Haptics.Finger.Ring);
            if (hexr != null && hexr.pinky) list.Add(Haptics.Finger.Pinky);
            if (hexr == null || hexr.palm) list.Add(Haptics.Finger.Palm);
            fingers = list.ToArray();

            if (hexr != null)
                hexr.enabled = false;
        }

        private void OnEnable()
        {
            if (grabbable != null)
                grabbable.WhenPointerEventRaised += OnPointerEvent;
            else
                Debug.LogError("[HEXR] No Grabbable on " + name);
        }

        private void OnDisable()
        {
            if (grabbable != null)
                grabbable.WhenPointerEventRaised -= OnPointerEvent;

            for (int side = Left; side <= Right; side++)
            {
                if (holding[side] > 0)
                    Send(side, false);
                holding[side] = 0;
            }
            sideOfPointer.Clear();
        }

        private void OnPointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    int side = SideOf(evt);
                    if (side < 0 || sideOfPointer.ContainsKey(evt.Identifier))
                        return;
                    sideOfPointer[evt.Identifier] = side;
                    if (++holding[side] == 1)
                        Send(side, true);
                    break;

                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    if (!sideOfPointer.TryGetValue(evt.Identifier, out int held))
                        return;
                    sideOfPointer.Remove(evt.Identifier);
                    if (--holding[held] <= 0)
                    {
                        holding[held] = 0;
                        Send(held, false);
                    }
                    break;
            }
        }

        /// <summary>Which glove: the Pressure Controller whose HandGrabInteractor raised the event.</summary>
        private static int SideOf(PointerEvent evt)
        {
            foreach (PressureTrackerMain tracker in FindObjectsByType<PressureTrackerMain>(FindObjectsSortMode.None))
                if (tracker.handGrabInteractor is IInteractorView view && view.Identifier == evt.Identifier)
                    return tracker.handType == PressureTrackerMain.HandType.Left ? Left : Right;

            // Not one of the hand-grab interactors (e.g. a controller): use whichever glove is there.
            if (Handler(Right) != null) return Right;
            if (Handler(Left) != null) return Left;
            return -1;
        }

        private static HaptGloveHandler Handler(int side)
        {
            HexRManager manager = HexRManager.Instance;
            if (manager == null)
                return null;
            HaptGloveHandler h = side == Left ? manager.leftHand : manager.rightHand;
            return h != null && h.bleConnected && h.haptics != null ? h : null;
        }

        private void Send(int side, bool on)
        {
            HaptGloveHandler h = Handler(side);
            float intensity = ToIntensity(strength);
            Debug.Log($"[HEXR] {name} {(side == Left ? "L" : "R")} {(on ? "press" : "release")} " +
                      $"{strength:0} -> intensity {intensity:0.00}{(h == null ? " (no glove)" : "")}");
            if (h == null)
                return;

            int n = fingers.Length;
            var states = new bool[n];
            var intensities = new float[n];
            var speeds = new float[n];
            for (int i = 0; i < n; i++)
            {
                states[i] = on;
                intensities[i] = on ? intensity : 0.1f;
                speeds[i] = speed;
            }
            h.BTSend(h.haptics.HEXRPressure(fingers, states, intensities, speeds));
        }
    }
}
