using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Kneeboard_Server.Navigraph.BGL
{
    /// <summary>
    /// Represents a SID or STAR procedure parsed from BGL file
    /// Ported from Little Navmap atools/src/fs/bgl/ap/sidstar.cpp
    /// </summary>
    public class BglSidStar
    {
        #region Properties

        /// <summary>
        /// Procedure identifier (e.g., "GIVMI1N")
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// Parent airport ICAO
        /// </summary>
        public string AirportIcao { get; set; }

        /// <summary>
        /// True if SID, false if STAR
        /// </summary>
        public bool IsSid { get; set; }

        /// <summary>
        /// Suffix character ('D' for SID, 'A' for STAR)
        /// </summary>
        public char Suffix => IsSid ? 'D' : 'A';

        /// <summary>
        /// Common route legs that apply to all transitions
        /// </summary>
        public List<BglLeg> CommonRouteLegs { get; set; } = new List<BglLeg>();

        /// <summary>
        /// Runway transitions - keyed by runway name (e.g., "08L")
        /// </summary>
        public Dictionary<string, List<BglLeg>> RunwayTransitions { get; set; }
            = new Dictionary<string, List<BglLeg>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Enroute transitions - keyed by transition name (fix identifier)
        /// </summary>
        public Dictionary<string, List<BglLeg>> EnrouteTransitions { get; set; }
            = new Dictionary<string, List<BglLeg>>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Parsing

        /// <summary>
        /// Parse SID/STAR from binary reader
        /// Based on atools sidstar.cpp SidStar constructor
        /// </summary>
        /// <param name="reader">Binary reader positioned at record start (after id+size)</param>
        /// <param name="recordId">Record ID (MSFS_SID or MSFS_STAR)</param>
        /// <param name="recordSize">Total record size</param>
        public static BglSidStar Parse(BinaryReader reader, ushort recordId, uint recordSize)
        {
            var proc = new BglSidStar();
            long startOffset = reader.BaseStream.Position - 6; // Account for id+size already read

            // Determine if SID or STAR
            proc.IsSid = (recordId == BglRecordTypes.MSFS_SID);

            // Skip 2 unknown bytes
            reader.ReadBytes(2);

            // Read counts (for reference, not directly used)
            byte runwayTransitionCount = reader.ReadByte();
            byte commonRouteLegCount = reader.ReadByte();
            byte enrouteTransitionCount = reader.ReadByte();

            // Skip 1 unknown byte
            reader.ReadByte();

            // Read identifier (8 bytes UTF-8)
            byte[] identBytes = reader.ReadBytes(8);
            proc.Identifier = Encoding.UTF8.GetString(identBytes).TrimEnd('\0', ' ');

            // Parse sub-records
            while (reader.BaseStream.Position < startOffset + recordSize)
            {
                // Read sub-record header
                ushort subRecId = reader.ReadUInt16();
                uint subRecSize = reader.ReadUInt32();
                long subRecStart = reader.BaseStream.Position;

                // Validate sub-record
                if (subRecSize == 0 || subRecSize > recordSize)
                {
                    Console.WriteLine($"[BglSidStar] Invalid sub-record size: {subRecSize}");
                    break;
                }

                try
                {
                    switch (subRecId)
                    {
                        case BglRecordTypes.COMMON_ROUTE_LEGS_MSFS:
                        case BglRecordTypes.COMMON_ROUTE_LEGS_MSFS_116:
                        case BglRecordTypes.COMMON_ROUTE_LEGS_MSFS_118:
                            ParseCommonRouteLegs(reader, proc, subRecId);
                            break;

                        case BglRecordTypes.RUNWAY_TRANSITIONS_MSFS:
                            ParseRunwayTransition(reader, proc, subRecId);
                            break;

                        case BglRecordTypes.ENROUTE_TRANSITIONS_MSFS:
                        case BglRecordTypes.ENROUTE_TRANSITIONS_MSFS_116:
                            ParseEnrouteTransition(reader, proc, subRecId);
                            break;

                        default:
                            // Unknown sub-record, skip it
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BglSidStar] Error parsing sub-record 0x{subRecId:X4}: {ex.Message}");
                }

                // Seek to end of sub-record
                reader.BaseStream.Seek(subRecStart + subRecSize - 6, SeekOrigin.Begin);
            }

            return proc;
        }

        /// <summary>
        /// Parse common route legs sub-record
        /// </summary>
        private static void ParseCommonRouteLegs(BinaryReader reader, BglSidStar proc, ushort recordType)
        {
            ushort numLegs = reader.ReadUInt16();

            for (int i = 0; i < numLegs; i++)
            {
                var leg = BglLeg.Parse(reader, recordType);
                if (leg.IsValid())
                {
                    proc.CommonRouteLegs.Add(leg);
                }
            }
        }

        /// <summary>
        /// Parse runway transition sub-record
        /// </summary>
        private static void ParseRunwayTransition(BinaryReader reader, BglSidStar proc, ushort parentRecordType)
        {
            // Transition count (not used)
            reader.ReadByte();

            // Runway number and designator
            int runwayNumber = reader.ReadByte();
            int runwayDesignator = reader.ReadByte() & 0x07;

            // Skip 3 unknown bytes
            reader.ReadBytes(3);

            // Convert to runway name
            string runwayName = BglConverter.RunwayToString(runwayNumber, runwayDesignator);

            // Read leg container record
            ushort legRecId = reader.ReadUInt16();
            uint legRecSize = reader.ReadUInt32();

            // Determine the actual leg record type for parsing
            ushort legType = legRecId;

            // Read legs
            ushort numLegs = reader.ReadUInt16();
            var legs = new List<BglLeg>();

            for (int i = 0; i < numLegs; i++)
            {
                var leg = BglLeg.Parse(reader, legType);
                if (leg.IsValid())
                {
                    legs.Add(leg);
                }
            }

            if (legs.Count > 0)
            {
                proc.RunwayTransitions[runwayName] = legs;
            }
        }

        /// <summary>
        /// Parse enroute transition sub-record
        /// </summary>
        private static void ParseEnrouteTransition(BinaryReader reader, BglSidStar proc, ushort recordType)
        {
            // Transition count (not used)
            reader.ReadByte();

            // Skip 1 unknown byte
            reader.ReadByte();

            // For MSFS 116+, skip 8-byte name field (now unused)
            if (recordType == BglRecordTypes.ENROUTE_TRANSITIONS_MSFS_116)
            {
                reader.ReadBytes(8);
            }

            // Read leg container record
            ushort legRecId = reader.ReadUInt16();
            uint legRecSize = reader.ReadUInt32();

            // Read legs
            ushort numLegs = reader.ReadUInt16();
            var legs = new List<BglLeg>();

            for (int i = 0; i < numLegs; i++)
            {
                var leg = BglLeg.Parse(reader, legRecId);
                if (leg.IsValid())
                {
                    legs.Add(leg);
                }
            }

            if (legs.Count > 0)
            {
                // Transition name is derived from fix identifier
                // For SID: last leg's fix
                // For STAR: first leg's fix
                string transitionName;
                if (proc.IsSid)
                {
                    transitionName = legs[legs.Count - 1].FixIdent;
                }
                else
                {
                    transitionName = legs[0].FixIdent;
                }

                if (!string.IsNullOrEmpty(transitionName))
                {
                    proc.EnrouteTransitions[transitionName] = legs;
                }
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Check if procedure has valid data
        /// </summary>
        public bool IsValid()
        {
            // Need at least one leg somewhere
            int totalLegs = CommonRouteLegs.Count;

            foreach (var trans in RunwayTransitions.Values)
                totalLegs += trans.Count;

            foreach (var trans in EnrouteTransitions.Values)
                totalLegs += trans.Count;

            return totalLegs > 0;
        }

        #endregion

        #region Conversion to ProcedureSummary

        /// <summary>
        /// Convert to ProcedureSummary model
        /// </summary>
        public ProcedureSummary ToProcedureSummary()
        {
            return new ProcedureSummary
            {
                Identifier = Identifier,
                Airport = AirportIcao,
                Type = IsSid ? ProcedureType.SID : ProcedureType.STAR,
                Runways = new List<string>(RunwayTransitions.Keys),
                Transitions = new List<string>(EnrouteTransitions.Keys)
            };
        }

        /// <summary>
        /// Get detailed procedure with waypoints
        /// </summary>
        public ProcedureDetail ToProcedureDetail(string transition = null)
        {
            var detail = new ProcedureDetail
            {
                Summary = ToProcedureSummary(),
                Transition = transition,
                DataSource = "MSFS BGL",
                AiracCycle = "MSFS Built-in",
                Waypoints = new List<ProcedureWaypoint>()
            };

            int seq = 1;

            // For SID: runway transition -> common route -> enroute transition
            // For STAR: enroute transition -> common route -> runway transition

            if (IsSid)
            {
                // Add runway transition legs
                if (!string.IsNullOrEmpty(transition) && RunwayTransitions.TryGetValue(transition, out var rwLegs))
                {
                    foreach (var leg in rwLegs)
                    {
                        detail.Waypoints.Add(leg.ToProcedureWaypoint(seq++));
                    }
                }

                // Add common route legs
                foreach (var leg in CommonRouteLegs)
                {
                    detail.Waypoints.Add(leg.ToProcedureWaypoint(seq++));
                }

                // Add enroute transition legs
                if (!string.IsNullOrEmpty(transition) && EnrouteTransitions.TryGetValue(transition, out var enLegs))
                {
                    foreach (var leg in enLegs)
                    {
                        detail.Waypoints.Add(leg.ToProcedureWaypoint(seq++));
                    }
                }
            }
            else
            {
                // STAR: reverse order
                // Add enroute transition legs
                if (!string.IsNullOrEmpty(transition) && EnrouteTransitions.TryGetValue(transition, out var enLegs))
                {
                    foreach (var leg in enLegs)
                    {
                        detail.Waypoints.Add(leg.ToProcedureWaypoint(seq++));
                    }
                }

                // Add common route legs
                foreach (var leg in CommonRouteLegs)
                {
                    detail.Waypoints.Add(leg.ToProcedureWaypoint(seq++));
                }

                // Add runway transition legs
                if (!string.IsNullOrEmpty(transition) && RunwayTransitions.TryGetValue(transition, out var rwLegs))
                {
                    foreach (var leg in rwLegs)
                    {
                        detail.Waypoints.Add(leg.ToProcedureWaypoint(seq++));
                    }
                }
            }

            return detail;
        }

        #endregion

        public override string ToString()
        {
            return $"{(IsSid ? "SID" : "STAR")} {Identifier}: RWY={string.Join(",", RunwayTransitions.Keys)} TRANS={string.Join(",", EnrouteTransitions.Keys)}";
        }
    }
}
