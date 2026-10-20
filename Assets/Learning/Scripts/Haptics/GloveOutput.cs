using System.Collections.Generic;
using HaptGlove;
using HexR;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>Where glove commands go: the real HexR glove, or nowhere (editor / simulator).</summary>
    public interface IGloveOutput
    {
        bool IsConnected { get; }
        void Press(GloveChannels channels, float intensity, float speed);
        void Release(GloveChannels channels, float speed);
        /// <summary>Device-side pulse train (0.1–2 Hz), then holds endIntensity.</summary>
        void Pulses(GloveChannels channels, float rateHz, float intensity, int count, float endIntensity);
        /// <summary>Continuous vibration, 0.1–40 Hz.</summary>
        void Vibrate(GloveChannels channels, float frequencyHz, float intensity);
        void StopVibration(GloveChannels channels);
        /// <summary>Raw air-pressure readback (may be null).</summary>
        int[] ReadAirPressure();
    }

    /// <summary>Drops every command. Used when there is no HexRManager or no glove connected.</summary>
    public class NullGloveOutput : IGloveOutput
    {
        public bool IsConnected => false;
        public void Press(GloveChannels channels, float intensity, float speed) { }
        public void Release(GloveChannels channels, float speed) { }
        public void Pulses(GloveChannels channels, float rateHz, float intensity, int count, float endIntensity) { }
        public void Vibrate(GloveChannels channels, float frequencyHz, float intensity) { }
        public void StopVibration(GloveChannels channels) { }
        public int[] ReadAirPressure() { return null; }
    }

    /// <summary>
    /// Sends HEXR commands straight to one hand's HaptGloveHandler. This bypasses the SDK's
    /// "hand near an object" gate on purpose: in the learning phase the touch is played to the
    /// patient, the hand does not have to touch anything.
    /// On Quest the SDK batches writes and sends at most every 100 ms, so timing finer than
    /// that is not reliable from here.
    /// </summary>
    public class HexRGloveOutput : IGloveOutput
    {
        private readonly HandSide side;

        public HexRGloveOutput(HandSide side)
        {
            this.side = side;
        }

        private HaptGloveHandler Handler
        {
            get
            {
                HexRManager manager = HexRManager.Instance;
                if (manager == null) return null;
                return side == HandSide.Left ? manager.leftHand : manager.rightHand;
            }
        }

        public bool IsConnected
        {
            get
            {
                HaptGloveHandler h = Handler;
                return h != null && h.bleConnected && h.haptics != null;
            }
        }

        public void Press(GloveChannels channels, float intensity, float speed)
        {
            SendPressure(channels, true, intensity, speed);
        }

        public void Release(GloveChannels channels, float speed)
        {
            SendPressure(channels, false, 0.1f, speed);
        }

        private void SendPressure(GloveChannels channels, bool on, float intensity, float speed)
        {
            if (!IsConnected || channels == GloveChannels.None) return;
            Build(channels, out Haptics.Finger[] fingers, out int n);
            var states = new bool[n];
            var intens = new float[n];
            var speeds = new float[n];
            for (int i = 0; i < n; i++)
            {
                states[i] = on;
                intens[i] = Mathf.Clamp(intensity, 0.1f, 1f);
                speeds[i] = Mathf.Clamp(speed, 0.1f, 1f);
            }
            HaptGloveHandler h = Handler;
            h.BTSend(h.haptics.HEXRPressure(fingers, states, intens, speeds));
        }

        public void Pulses(GloveChannels channels, float rateHz, float intensity, int count, float endIntensity)
        {
            if (!IsConnected || channels == GloveChannels.None) return;
            Build(channels, out Haptics.Finger[] fingers, out int n);
            var states = new bool[n];
            var freqs = new float[n];
            var intens = new float[n];
            var peaks = new float[n];
            var speeds = new float[n];
            var counts = new ushort[n];
            var ends = new float[n];
            for (int i = 0; i < n; i++)
            {
                states[i] = true;
                freqs[i] = Mathf.Clamp(rateHz, 0.1f, 2f);
                // The multi-channel overload is documented as 0.1–0.7 for intensity.
                intens[i] = Mathf.Clamp(intensity, 0.1f, 0.7f);
                peaks[i] = 0.5f;
                speeds[i] = 1f;
                counts[i] = (ushort)Mathf.Clamp(count, 1, 1000);
                ends[i] = Mathf.Clamp(endIntensity, 0.1f, 1f);
            }
            HaptGloveHandler h = Handler;
            h.BTSend(h.haptics.HEXRPulse(fingers, states, freqs, intens, peaks, speeds, counts, ends));
        }

        public void Vibrate(GloveChannels channels, float frequencyHz, float intensity)
        {
            SendVibration(channels, true, frequencyHz, intensity);
        }

        public void StopVibration(GloveChannels channels)
        {
            SendVibration(channels, false, 1f, 0.1f);
        }

        private void SendVibration(GloveChannels channels, bool on, float frequencyHz, float intensity)
        {
            if (!IsConnected || channels == GloveChannels.None) return;
            Build(channels, out Haptics.Finger[] fingers, out int n);
            var states = new bool[n];
            var freqs = new float[n];
            var intens = new float[n];
            for (int i = 0; i < n; i++)
            {
                states[i] = on;
                freqs[i] = Mathf.Clamp(frequencyHz, 0.1f, 40f);
                intens[i] = Mathf.Clamp(intensity, 0.1f, 1f);
            }
            HaptGloveHandler h = Handler;
            h.BTSend(h.haptics.HEXRVibration(fingers, states, freqs, intens));
        }

        public int[] ReadAirPressure()
        {
            HaptGloveHandler h = Handler;
            return h != null && h.bleConnected ? h.GetAirPressure() : null;
        }

        private static void Build(GloveChannels channels, out Haptics.Finger[] fingers, out int n)
        {
            var list = new List<Haptics.Finger>(6);
            foreach (GloveChannels c in GloveChannelUtil.Split(channels))
                list.Add(GloveChannelUtil.ToFinger(c));
            fingers = list.ToArray();
            n = fingers.Length;
        }
    }
}
