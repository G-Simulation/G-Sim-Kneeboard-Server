using System;

namespace Kneeboard_Server.Navigraph.BGL
{
    /// <summary>
    /// BGL Record Types - ported from Little Navmap atools/src/fs/bgl/recordtypes.h
    /// </summary>
    public static class BglRecordTypes
    {
        // Top level record types
        public const ushort AIRPORT = 0x003c;           // MSFS 2020 airport record
        public const ushort AIRPORT_MSFS2024 = 0x0113;  // MSFS 2024 airport record (Navigraph)
        public const ushort WAYPOINT = 0x0022;
        public const ushort ILS_VOR = 0x0013;
        public const ushort NDB = 0x0017;

        // Airport sub-record types
        public const ushort MSFS_SID = 0x0042;
        public const ushort MSFS_STAR = 0x0048;
        public const ushort APPROACH = 0x0024;
        public const ushort MSFS_APPROACH_NEW = 0x00fa;

        // SID/STAR sub-record types
        public const ushort COMMON_ROUTE_LEGS_MSFS = 0x00e5;
        public const ushort COMMON_ROUTE_LEGS_MSFS_116 = 0x00f0;
        public const ushort COMMON_ROUTE_LEGS_MSFS_118 = 0x00f8;
        public const ushort RUNWAY_TRANSITIONS_MSFS = 0x0046;
        public const ushort ENROUTE_TRANSITIONS_MSFS = 0x0047;
        public const ushort ENROUTE_TRANSITIONS_MSFS_116 = 0x004a;
        public const ushort RUNWAY_TRANSITION_LEGS_MSFS = 0x00e4;
        public const ushort RUNWAY_TRANSITION_LEGS_MSFS_116 = 0x00ef;
        public const ushort RUNWAY_TRANSITION_LEGS_MSFS_118 = 0x00f7;
        public const ushort ENROUTE_TRANSITION_LEGS_MSFS = 0x00e6;
        public const ushort ENROUTE_TRANSITION_LEGS_MSFS_116 = 0x00f1;
        public const ushort ENROUTE_TRANSITION_LEGS_MSFS_118 = 0x00f9;

        // Approach leg types
        public const ushort LEGS_MSFS = 0x00e1;
        public const ushort LEGS_MSFS_116 = 0x00ec;
        public const ushort LEGS_MSFS_118 = 0x00f4;

        /// <summary>
        /// Check if record type is an airport record (MSFS 2020 or MSFS 2024)
        /// Based on atools approach: don't filter on record ID, trust section type
        /// </summary>
        public static bool IsAirportRecord(ushort type)
        {
            return type == AIRPORT || type == AIRPORT_MSFS2024;
        }

        /// <summary>
        /// Check if record type is MSFS specific
        /// </summary>
        public static bool IsMsfsType(ushort type)
        {
            return type >= 0x00e0;
        }

        /// <summary>
        /// Check if record type is MSFS 1.16+ specific
        /// </summary>
        public static bool IsMsfs116Type(ushort type)
        {
            switch (type)
            {
                case COMMON_ROUTE_LEGS_MSFS_116:
                case RUNWAY_TRANSITION_LEGS_MSFS_116:
                case ENROUTE_TRANSITION_LEGS_MSFS_116:
                case LEGS_MSFS_116:
                case ENROUTE_TRANSITIONS_MSFS_116:
                    return true;
                default:
                    return IsMsfs118Type(type);
            }
        }

        /// <summary>
        /// Check if record type is MSFS 1.18+ specific
        /// </summary>
        public static bool IsMsfs118Type(ushort type)
        {
            switch (type)
            {
                case COMMON_ROUTE_LEGS_MSFS_118:
                case RUNWAY_TRANSITION_LEGS_MSFS_118:
                case ENROUTE_TRANSITION_LEGS_MSFS_118:
                case LEGS_MSFS_118:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// ARINC 424 Leg Types - from atools approachleg.h
    /// </summary>
    public enum LegType : byte
    {
        AF = 0x01,  // Arc To a Fix
        CA = 0x02,  // Course To Altitude
        CD = 0x03,  // Course To a DME
        CF = 0x04,  // Course To a Fix
        CI = 0x05,  // Course To Next Leg Intercept
        CR = 0x06,  // Course To a Radial
        DF = 0x07,  // Direct To a Fix
        FA = 0x08,  // Fix To Altitude
        FC = 0x09,  // Track from Fix to Distance
        FD = 0x0A,  // Track from Fix to DME Distance
        FM = 0x0B,  // From Fix to Manual Termination
        HA = 0x0C,  // Racetrack to Altitude (Hold)
        HF = 0x0D,  // Racetrack to Fix (Hold)
        HM = 0x0E,  // Racetrack to Manual Termination (Hold)
        IF = 0x0F,  // Initial Fix
        PI = 0x10,  // Procedure Turn
        RF = 0x11,  // Radius to Fix (constant radius arc)
        TF = 0x12,  // Track To a Fix
        VA = 0x13,  // Heading To Altitude
        VD = 0x14,  // Heading To DME
        VI = 0x15,  // Heading To Next Leg Intercept
        VM = 0x16,  // Heading To Manual Termination
        VR = 0x17   // Heading To a Radial
    }

    /// <summary>
    /// Altitude descriptor - from atools approachleg.h
    /// </summary>
    public enum AltitudeDescriptor : byte
    {
        Unknown = 0x00,
        At = 0x01,           // @ - At altitude
        AtOrAbove = 0x02,    // + - At or above
        AtOrBelow = 0x03,    // - - At or below
        Between = 0x04       // B - Between alt1 and alt2
    }

    /// <summary>
    /// Turn direction - from atools approachleg.h
    /// </summary>
    public enum TurnDirection : byte
    {
        None = 0x00,
        Left = 0x01,
        Right = 0x02,
        Both = 0x03
    }

    /// <summary>
    /// Fix type - from atools approachtypes.h
    /// </summary>
    public enum FixType : byte
    {
        None = 0,
        Airport = 1,
        VOR = 2,
        NDB = 3,
        TerminalNDB = 4,
        Waypoint = 5,
        TerminalWaypoint = 6,
        Runway = 7
    }

    /// <summary>
    /// Helper extensions for LegType
    /// </summary>
    public static class LegTypeExtensions
    {
        public static string ToArinc424(this LegType type)
        {
            return type.ToString();
        }

        public static string GetDescription(this LegType type)
        {
            switch (type)
            {
                case LegType.AF: return "Arc To a Fix";
                case LegType.CA: return "Course To Altitude";
                case LegType.CD: return "Course To DME";
                case LegType.CF: return "Course To a Fix";
                case LegType.CI: return "Course To Intercept";
                case LegType.CR: return "Course To Radial";
                case LegType.DF: return "Direct To a Fix";
                case LegType.FA: return "Fix To Altitude";
                case LegType.FC: return "Track from Fix to Distance";
                case LegType.FD: return "Track from Fix to DME";
                case LegType.FM: return "From Fix to Manual";
                case LegType.HA: return "Hold to Altitude";
                case LegType.HF: return "Hold to Fix";
                case LegType.HM: return "Hold to Manual";
                case LegType.IF: return "Initial Fix";
                case LegType.PI: return "Procedure Turn";
                case LegType.RF: return "Radius to Fix";
                case LegType.TF: return "Track To a Fix";
                case LegType.VA: return "Heading To Altitude";
                case LegType.VD: return "Heading To DME";
                case LegType.VI: return "Heading To Intercept";
                case LegType.VM: return "Heading To Manual";
                case LegType.VR: return "Heading To Radial";
                default: return "Unknown";
            }
        }
    }

    /// <summary>
    /// Helper extensions for AltitudeDescriptor
    /// </summary>
    public static class AltitudeDescriptorExtensions
    {
        public static string ToSymbol(this AltitudeDescriptor desc)
        {
            switch (desc)
            {
                case AltitudeDescriptor.At: return "@";
                case AltitudeDescriptor.AtOrAbove: return "+";
                case AltitudeDescriptor.AtOrBelow: return "-";
                case AltitudeDescriptor.Between: return "B";
                default: return "";
            }
        }
    }

    /// <summary>
    /// Helper extensions for TurnDirection
    /// </summary>
    public static class TurnDirectionExtensions
    {
        public static string ToSymbol(this TurnDirection dir)
        {
            switch (dir)
            {
                case TurnDirection.Left: return "L";
                case TurnDirection.Right: return "R";
                case TurnDirection.Both: return "B";
                default: return "";
            }
        }
    }
}
