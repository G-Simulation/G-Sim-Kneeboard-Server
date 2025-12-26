using System;
using System.Collections.Generic;

namespace Kneeboard_Server.Navigraph
{
    #region Authentication Models

    /// <summary>
    /// Response from Navigraph Device Authorization endpoint
    /// </summary>
    public class DeviceCodeResponse
    {
        public string DeviceCode { get; set; }
        public string UserCode { get; set; }
        public string VerificationUri { get; set; }
        public string VerificationUriComplete { get; set; }
        public int ExpiresIn { get; set; }
        public int Interval { get; set; }
    }

    /// <summary>
    /// Token response from Navigraph
    /// </summary>
    public class TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
        public string Scope { get; set; }
        public string IdToken { get; set; }
    }

    /// <summary>
    /// Error response from OAuth endpoints
    /// </summary>
    public class OAuthError
    {
        public string Error { get; set; }
        public string ErrorDescription { get; set; }
    }

    #endregion

    #region Navdata Package Models

    /// <summary>
    /// Response from /v1/navdata/packages endpoint
    /// </summary>
    public class NavdataPackagesResponse
    {
        public List<NavdataPackage> Packages { get; set; }
    }

    /// <summary>
    /// A navdata package from Navigraph
    /// </summary>
    public class NavdataPackage
    {
        public string PackageId { get; set; }
        public string Cycle { get; set; }
        public string Revision { get; set; }
        public string Description { get; set; }
        public string Format { get; set; }
        public string PackageStatus { get; set; }  // "outdated", "current", "future"
        public string FormatType { get; set; }
        public List<PackageFile> Files { get; set; }
        public List<string> Addons { get; set; }
    }

    /// <summary>
    /// A file within a navdata package
    /// </summary>
    public class PackageFile
    {
        public string FileId { get; set; }
        public string Key { get; set; }  // filename
        public string Hash { get; set; }  // SHA256
        public string SignedUrl { get; set; }  // CloudFront download URL
    }

    #endregion

    #region Procedure Models

    /// <summary>
    /// Procedure type enumeration
    /// </summary>
    public enum ProcedureType
    {
        SID,
        STAR,
        Approach
    }

    /// <summary>
    /// Approach type enumeration
    /// </summary>
    public enum ApproachType
    {
        ILS,
        LOC,
        RNAV,
        RNP,
        VOR,
        VORDME,
        NDB,
        GPS,
        LDA,
        SDF,
        VISUAL,
        Unknown
    }

    /// <summary>
    /// Summary of a procedure (for list endpoints)
    /// </summary>
    public class ProcedureSummary
    {
        public string Identifier { get; set; }
        public string Airport { get; set; }
        public ProcedureType Type { get; set; }
        public List<string> Runways { get; set; } = new List<string>();
        public List<string> Transitions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Approach-specific summary with approach type
    /// </summary>
    public class ApproachSummary : ProcedureSummary
    {
        public ApproachType ApproachType { get; set; }
        public string Runway { get; set; }
        public double? MinimumAltitude { get; set; }
    }

    /// <summary>
    /// A waypoint within a procedure with all constraint details
    /// </summary>
    public class ProcedureWaypoint
    {
        public int Sequence { get; set; }
        public string Identifier { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        /// <summary>
        /// ARINC 424 path termination code (TF, CF, DF, RF, etc.)
        /// </summary>
        public string PathTermination { get; set; }

        /// <summary>
        /// Turn direction: L (Left), R (Right), or null
        /// </summary>
        public string TurnDirection { get; set; }

        /// <summary>
        /// True if this is a fly-over waypoint (vs fly-by)
        /// </summary>
        public bool IsFlyOver { get; set; }

        /// <summary>
        /// Required Navigation Performance value
        /// </summary>
        public double? Rnp { get; set; }

        // Altitude Constraints
        /// <summary>
        /// Altitude description: @ (at), + (at or above), - (at or below), B (between)
        /// </summary>
        public string AltitudeDescription { get; set; }
        public int? Altitude1 { get; set; }
        public int? Altitude2 { get; set; }

        // Speed Constraints
        /// <summary>
        /// Speed limit description: @ (at), + (at or above), - (at or below)
        /// </summary>
        public string SpeedLimitDescription { get; set; }
        public int? SpeedLimit { get; set; }

        // Course/Distance
        public double? MagneticCourse { get; set; }
        public double? RouteDistance { get; set; }

        // RF (Radius to Fix) leg data
        public double? ArcRadius { get; set; }
        public string CenterWaypoint { get; set; }
        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }

        // Vertical guidance
        public double? VerticalAngle { get; set; }

        // Recommended navaid
        public string RecommendedNavaid { get; set; }
        public double? RecommendedNavaidLatitude { get; set; }
        public double? RecommendedNavaidLongitude { get; set; }
    }

    /// <summary>
    /// Full procedure detail with all waypoints
    /// </summary>
    public class ProcedureDetail
    {
        public ProcedureSummary Summary { get; set; }
        public string Transition { get; set; }
        public List<ProcedureWaypoint> Waypoints { get; set; } = new List<ProcedureWaypoint>();
        public string DataSource { get; set; }  // "Navigraph" or "SimBrief"
        public string AiracCycle { get; set; }
    }

    #endregion

    #region ILS/Navigation Aid Models

    /// <summary>
    /// ILS/Localizer data for an approach
    /// </summary>
    public class ILSData
    {
        public string Airport { get; set; }
        public string Runway { get; set; }
        public string Identifier { get; set; }
        public double Frequency { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double LocalizerBearing { get; set; }
        public double? GlideSlopeAngle { get; set; }
        public double? ThresholdCrossingHeight { get; set; }
        public string Category { get; set; }  // "CAT I", "CAT II", "CAT III"
        public double? Elevation { get; set; }
    }

    /// <summary>
    /// VHF Navaid data
    /// </summary>
    public class VHFNavaid
    {
        public string Identifier { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }  // VOR, VORDME, DME, TACAN, VORTAC
        public double Frequency { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Elevation { get; set; }
        public double? MagneticVariation { get; set; }
    }

    /// <summary>
    /// Runway data
    /// </summary>
    public class RunwayData
    {
        public string Airport { get; set; }
        public string RunwayIdentifier { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Heading { get; set; }
        public int Length { get; set; }  // in feet
        public int Width { get; set; }   // in feet
        public double? Elevation { get; set; }
        public double? GlideSlopeAngle { get; set; }
        public double? ThresholdCrossingHeight { get; set; }
        public string ILSIdentifier { get; set; }
        public double? ILSFrequency { get; set; }
    }

    #endregion

    #region API Response Models

    /// <summary>
    /// Standard response wrapper for procedure list endpoints
    /// </summary>
    public class ProcedureListResponse
    {
        public string Source { get; set; }
        public string AiracCycle { get; set; }
        public string Airport { get; set; }
        public List<ProcedureSummary> Procedures { get; set; }
    }

    /// <summary>
    /// Response for SID list endpoint
    /// </summary>
    public class SIDListResponse
    {
        public string Source { get; set; }
        public string AiracCycle { get; set; }
        public string Airport { get; set; }
        public List<ProcedureSummary> Sids { get; set; }
    }

    /// <summary>
    /// Response for STAR list endpoint
    /// </summary>
    public class STARListResponse
    {
        public string Source { get; set; }
        public string AiracCycle { get; set; }
        public string Airport { get; set; }
        public List<ProcedureSummary> Stars { get; set; }
    }

    /// <summary>
    /// Response for approach list endpoint
    /// </summary>
    public class ApproachListResponse
    {
        public string Source { get; set; }
        public string AiracCycle { get; set; }
        public string Airport { get; set; }
        public List<ApproachSummary> Approaches { get; set; }
    }

    /// <summary>
    /// Navigraph authentication/subscription status
    /// </summary>
    public class NavigraphStatus
    {
        public bool Authenticated { get; set; }
        public bool HasSubscription { get; set; }
        public string AiracCycle { get; set; }
        public DateTime? LastUpdate { get; set; }
        public string DatabasePath { get; set; }
        public string Username { get; set; }
    }

    /// <summary>
    /// Device code info for login UI
    /// </summary>
    public class NavigraphLoginInfo
    {
        public string UserCode { get; set; }
        public string VerificationUrl { get; set; }
        public int ExpiresIn { get; set; }
    }

    #endregion

    #region Path Termination Helper

    /// <summary>
    /// Helper class for ARINC 424 path termination codes
    /// </summary>
    public static class PathTerminationHelper
    {
        public static string GetDescription(string code)
        {
            switch (code?.ToUpper())
            {
                case "IF": return "Initial Fix";
                case "TF": return "Track to Fix";
                case "CF": return "Course to Fix";
                case "DF": return "Direct to Fix";
                case "FA": return "Fix to Altitude";
                case "FC": return "Track from Fix to Distance";
                case "FD": return "Track from Fix to DME Distance";
                case "FM": return "From Fix to Manual Termination";
                case "CA": return "Course to Altitude";
                case "CD": return "Course to DME Distance";
                case "CI": return "Course to Intercept";
                case "CR": return "Course to Radial Termination";
                case "VA": return "Heading to Altitude";
                case "VD": return "Heading to DME Distance";
                case "VI": return "Heading to Intercept";
                case "VM": return "Heading to Manual Termination";
                case "VR": return "Heading to Radial Termination";
                case "AF": return "Arc to Fix";
                case "RF": return "Radius to Fix";
                case "PI": return "Procedure Turn";
                case "HA": return "Racetrack to Altitude";
                case "HF": return "Racetrack to Fix";
                case "HM": return "Racetrack to Manual Termination";
                default: return code ?? "Unknown";
            }
        }

        public static bool IsArcLeg(string code)
        {
            return code?.ToUpper() == "RF" || code?.ToUpper() == "AF";
        }

        public static bool IsHoldingPattern(string code)
        {
            var upper = code?.ToUpper();
            return upper == "HA" || upper == "HF" || upper == "HM";
        }
    }

    #endregion
}
