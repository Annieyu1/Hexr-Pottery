using System;
using System.Collections.Generic;
using HaptGlove;

namespace HexRMeta.Learning
{
    /// <summary>The six HexR actuators. Flags, so one value can name several at once.</summary>
    [Flags]
    public enum GloveChannels
    {
        None = 0,
        Thumb = 1 << 0,
        Index = 1 << 1,
        Middle = 1 << 2,
        Ring = 1 << 3,
        Little = 1 << 4,
        Palm = 1 << 5,
        AllFingers = Thumb | Index | Middle | Ring | Little,
        WholeHand = AllFingers | Palm,
    }

    public enum HandSide { Left, Right }

    public static class GloveChannelUtil
    {
        /// <summary>Channel order used for arrays everywhere: thumb, index, middle, ring, little, palm.</summary>
        public static readonly GloveChannels[] Order =
        {
            GloveChannels.Thumb, GloveChannels.Index, GloveChannels.Middle,
            GloveChannels.Ring, GloveChannels.Little, GloveChannels.Palm,
        };

        public const int Count = 6;

        public static int IndexOf(GloveChannels single)
        {
            for (int i = 0; i < Order.Length; i++)
                if (Order[i] == single) return i;
            return -1;
        }

        public static Haptics.Finger ToFinger(GloveChannels single)
        {
            switch (single)
            {
                case GloveChannels.Thumb: return Haptics.Finger.Thumb;
                case GloveChannels.Index: return Haptics.Finger.Index;
                case GloveChannels.Middle: return Haptics.Finger.Middle;
                case GloveChannels.Ring: return Haptics.Finger.Ring;
                case GloveChannels.Little: return Haptics.Finger.Pinky;
                default: return Haptics.Finger.Palm;
            }
        }

        public static IEnumerable<GloveChannels> Split(GloveChannels mask)
        {
            foreach (GloveChannels c in Order)
                if ((mask & c) != 0) yield return c;
        }

        /// <summary>"Index+Middle" style text for logs.</summary>
        public static string ToLabel(GloveChannels mask)
        {
            if (mask == GloveChannels.None) return "None";
            if (mask == GloveChannels.WholeHand) return "WholeHand";
            var parts = new List<string>();
            foreach (GloveChannels c in Split(mask)) parts.Add(c.ToString());
            return string.Join("+", parts);
        }
    }
}
