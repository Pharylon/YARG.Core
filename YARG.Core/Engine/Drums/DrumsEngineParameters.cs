using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Engine.Drums
{
    public class DrumsEngineParameters : BaseEngineParameters
    {
        public enum DrumMode : byte
        {
            NonProFourLane,
            ProFourLane,
            FiveLane,
            Elite
        }

        /// <summary>
        /// What mode the inputs should be processed in.
        /// </summary>
        public readonly DrumMode Mode;

        //Ghost notes are below this threshold, Accent notes are above 1 - threshold
        public readonly float VelocityThreshold;

        // The maximum allowed time (seconds) between notes to use context-sensitive velocity scoring
        public readonly float SituationalVelocityWindow;

        // Whether or not we can earn more star power while its already active
        public readonly bool NoStarPowerOverlap;

        /// <summary>
        /// How much the Easy Kicks modifier widens the hit window of kick notes. Both ends of the
        /// window are scaled, so 1.5 adds a quarter of the full window ahead of and behind the note.
        /// </summary>
        public const double EASY_KICKS_HIT_WINDOW_MULTIPLIER = 1.5;

        /// <summary>
        /// The hit window of kick notes is this many times the size of the hit window of every other
        /// note. 1 means kicks are not treated specially; the value is never below 1.
        /// </summary>
        public readonly double KickHitWindowMultiplier;

        public DrumsEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds, float[] soloBonusStarMultiplierThresholds,
            DrumMode mode, bool noStarPowerOverlap, bool enableLanes, double kickHitWindowMultiplier = 1.0)
            : base(hitWindow, maxMultiplier, 0, 0, starMultiplierThresholds, soloBonusStarMultiplierThresholds, enableLanes)
        {
            Mode = mode;
            VelocityThreshold = 0.35f;
            SituationalVelocityWindow = 1.5f;
            NoStarPowerOverlap = noStarPowerOverlap;
            KickHitWindowMultiplier = Math.Max(1.0, kickHitWindowMultiplier);
        }

        public DrumsEngineParameters(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            Mode = (DrumMode) stream.ReadByte();
            VelocityThreshold = stream.Read<float>(Endianness.Little);
            SituationalVelocityWindow = stream.Read<float>(Endianness.Little);
            if (version >= 9) {
                NoStarPowerOverlap = stream.ReadBoolean();
            }

            KickHitWindowMultiplier = 1.0;
            if (version >= 20)
            {
                KickHitWindowMultiplier = Math.Max(1.0, stream.Read<double>(Endianness.Little));
            }
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write((byte) Mode);
            writer.Write(VelocityThreshold);
            writer.Write(SituationalVelocityWindow);
            writer.Write(NoStarPowerOverlap);
            writer.Write(KickHitWindowMultiplier);
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Velocity threshold: {VelocityThreshold}\n" +
                $"Situational velocity window: {SituationalVelocityWindow}\n" +
                $"No star power overlap: {NoStarPowerOverlap}\n" +
                $"Kick hit window multiplier: {KickHitWindowMultiplier}";
        }
    }
}