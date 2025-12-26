using System;
using System.IO;

namespace Kneeboard_Server.Navigraph.BGL
{
    /// <summary>
    /// Represents a procedure leg parsed from BGL file
    /// Ported from Little Navmap atools/src/fs/bgl/ap/approachleg.cpp
    /// </summary>
    public class BglLeg
    {
        #region Properties

        /// <summary>
        /// ARINC 424 leg type (TF, CF, DF, RF, etc.)
        /// </summary>
        public LegType Type { get; set; }

        /// <summary>
        /// Altitude constraint type
        /// </summary>
        public AltitudeDescriptor AltitudeDescriptor { get; set; }

        /// <summary>
        /// Turn direction requirement
        /// </summary>
        public TurnDirection TurnDirection { get; set; }

        /// <summary>
        /// Fix type (VOR, NDB, Waypoint, etc.)
        /// </summary>
        public FixType FixType { get; set; }

        /// <summary>
        /// Fix identifier (waypoint name)
        /// </summary>
        public string FixIdent { get; set; }

        /// <summary>
        /// Fix region (2-letter ICAO region)
        /// </summary>
        public string FixRegion { get; set; }

        /// <summary>
        /// Fix airport identifier
        /// </summary>
        public string FixAirportIdent { get; set; }

        /// <summary>
        /// Recommended fix type
        /// </summary>
        public FixType RecommendedFixType { get; set; }

        /// <summary>
        /// Recommended fix identifier
        /// </summary>
        public string RecommendedFixIdent { get; set; }

        /// <summary>
        /// Recommended fix region
        /// </summary>
        public string RecommendedFixRegion { get; set; }

        /// <summary>
        /// Theta - heading/bearing from fix
        /// </summary>
        public float Theta { get; set; }

        /// <summary>
        /// Rho - distance from fix (NM)
        /// </summary>
        public float Rho { get; set; }

        /// <summary>
        /// Magnetic course
        /// </summary>
        public float Course { get; set; }

        /// <summary>
        /// Distance or time value
        /// </summary>
        public float DistanceOrTime { get; set; }

        /// <summary>
        /// Primary altitude constraint (feet)
        /// </summary>
        public float Altitude1 { get; set; }

        /// <summary>
        /// Secondary altitude constraint (feet) - for "between" constraints
        /// </summary>
        public float Altitude2 { get; set; }

        /// <summary>
        /// Speed limit (knots)
        /// </summary>
        public float SpeedLimit { get; set; }

        /// <summary>
        /// Vertical angle (degrees)
        /// </summary>
        public float VerticalAngle { get; set; }

        /// <summary>
        /// True if course is true north referenced
        /// </summary>
        public bool TrueCourse { get; set; }

        /// <summary>
        /// True if DistanceOrTime is time (minutes) instead of distance
        /// </summary>
        public bool IsTime { get; set; }

        /// <summary>
        /// True if this is a fly-over waypoint
        /// </summary>
        public bool IsFlyOver { get; set; }

        /// <summary>
        /// True if this is a missed approach leg
        /// </summary>
        public bool IsMissed { get; set; }

        #endregion

        #region Parsing

        /// <summary>
        /// Parse a leg from binary reader
        /// Based on atools approachleg.cpp ApproachLeg constructor
        /// </summary>
        /// <param name="reader">Binary reader positioned at leg start</param>
        /// <param name="recordType">The parent record type for version detection</param>
        public static BglLeg Parse(BinaryReader reader, ushort recordType)
        {
            var leg = new BglLeg();

            // Determine MSFS version from record type
            bool isMsfs = BglRecordTypes.IsMsfsType(recordType);
            bool isMsfs116 = BglRecordTypes.IsMsfs116Type(recordType);
            bool isMsfs118 = BglRecordTypes.IsMsfs118Type(recordType);

            // Byte 0: Leg type
            leg.Type = (LegType)reader.ReadByte();

            // Byte 1: Altitude descriptor
            leg.AltitudeDescriptor = (AltitudeDescriptor)reader.ReadByte();

            // Bytes 2-3: Flags
            ushort flags = reader.ReadUInt16();
            leg.TurnDirection = (TurnDirection)(flags & 0x03);
            leg.TrueCourse = (flags & (1 << 8)) != 0;
            leg.IsTime = (flags & (1 << 9)) != 0;
            leg.IsFlyOver = (flags & (1 << 10)) != 0;

            // Bytes 4-7: Fix flags (fix type in bits 0-3, ident in bits 5-31)
            uint fixFlags = reader.ReadUInt32();
            leg.FixType = (FixType)(fixFlags & 0x0F);
            leg.FixIdent = BglConverter.IntToIcao((fixFlags >> 5) & 0x0FFFFFFF, true);

            // Bytes 8-11: Fix ident flags (region in bits 0-10, airport in bits 11-31)
            uint fixIdentFlags = reader.ReadUInt32();
            leg.FixRegion = BglConverter.IntToIcao(fixIdentFlags & 0x7FF, true);
            leg.FixAirportIdent = BglConverter.IntToIcao((fixIdentFlags >> 11) & 0x1FFFFF, true);

            // Bytes 12-15: Recommended fix flags
            uint recFixFlags = reader.ReadUInt32();
            leg.RecommendedFixType = (FixType)(recFixFlags & 0x0F);
            leg.RecommendedFixIdent = BglConverter.IntToIcao((recFixFlags >> 5) & 0x0FFFFFFF, true);

            // Bytes 16-19: Recommended fix region
            uint recFixRegion = reader.ReadUInt32();
            leg.RecommendedFixRegion = BglConverter.IntToIcao(recFixRegion & 0x7FF, true);

            // Bytes 20-23: Theta (heading/bearing)
            leg.Theta = reader.ReadSingle();

            // Bytes 24-27: Rho (distance)
            leg.Rho = reader.ReadSingle();

            // Bytes 28-31: Course
            leg.Course = reader.ReadSingle();

            // Bytes 32-35: Distance or time
            leg.DistanceOrTime = reader.ReadSingle();

            // Bytes 36-39: Altitude1
            leg.Altitude1 = reader.ReadSingle();

            // Bytes 40-43: Altitude2
            leg.Altitude2 = reader.ReadSingle();

            // MSFS extensions
            if (isMsfs)
            {
                // Bytes 44-47: Speed limit
                leg.SpeedLimit = reader.ReadSingle();

                // Bytes 48-51: Vertical angle
                leg.VerticalAngle = reader.ReadSingle();

                // Bytes 52-59: Unknown (skip)
                reader.ReadBytes(8);

                // MSFS 1.16+ has additional data for RF legs
                if (isMsfs116 || isMsfs118)
                {
                    if (leg.Type == LegType.RF)
                    {
                        // RF legs have center fix data in recommended fix fields
                        recFixFlags = reader.ReadUInt32();
                        leg.RecommendedFixType = (FixType)(recFixFlags & 0x0F);
                        leg.RecommendedFixIdent = BglConverter.IntToIcao((recFixFlags >> 5) & 0x0FFFFFFF, true);
                        leg.RecommendedFixRegion = BglConverter.IntToIcao(reader.ReadUInt32() & 0x7FF, true);
                    }
                    else
                    {
                        // Skip center fix data
                        reader.ReadBytes(8);
                    }
                }

                // MSFS 1.18+ has additional unknown data
                if (isMsfs118)
                {
                    reader.ReadBytes(4);
                }
            }

            return leg;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Check if leg has valid values
        /// Based on atools approachleg.cpp isValid()
        /// </summary>
        public bool IsValid()
        {
            return Theta >= 0 && Theta <= 360 &&
                   Course >= 0 && Course <= 360 &&
                   Rho >= 0 &&
                   Altitude1 >= 0 && Altitude1 <= 60000 &&
                   Altitude2 >= 0 && Altitude2 <= 60000;
        }

        #endregion

        #region Conversion to ProcedureWaypoint

        /// <summary>
        /// Convert to ProcedureWaypoint model
        /// </summary>
        public ProcedureWaypoint ToProcedureWaypoint(int sequence)
        {
            return new ProcedureWaypoint
            {
                Sequence = sequence,
                Identifier = FixIdent,
                PathTermination = Type.ToArinc424(),
                TurnDirection = TurnDirection != TurnDirection.None ? TurnDirection.ToSymbol() : null,
                IsFlyOver = IsFlyOver,
                AltitudeDescription = AltitudeDescriptor != AltitudeDescriptor.Unknown
                    ? AltitudeDescriptor.ToSymbol() : null,
                Altitude1 = Altitude1 > 0 ? (int?)Altitude1 : null,
                Altitude2 = Altitude2 > 0 ? (int?)Altitude2 : null,
                SpeedLimit = SpeedLimit > 0 ? (int?)SpeedLimit : null,
                MagneticCourse = Course > 0 ? (double?)Course : null,
                RouteDistance = !IsTime && DistanceOrTime > 0 ? (double?)DistanceOrTime : null,
                VerticalAngle = VerticalAngle != 0 ? (double?)VerticalAngle : null,
                RecommendedNavaid = !string.IsNullOrEmpty(RecommendedFixIdent) ? RecommendedFixIdent : null
            };
        }

        #endregion

        public override string ToString()
        {
            return $"{Type} {FixIdent} ALT:{Altitude1:F0} CRS:{Course:F0}";
        }
    }
}
