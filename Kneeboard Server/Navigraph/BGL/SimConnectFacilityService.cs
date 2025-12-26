using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FlightSimulator.SimConnect;

namespace Kneeboard_Server.Navigraph.BGL
{
    /// <summary>
    /// SimConnect Facility Data API service for MSFS 2024 SID/STAR procedures
    /// Implemented EXACTLY like atools simconnectloader.cpp
    ///
    /// Based on atools:
    /// - addLegs() defines TYPE, FIX_ICAO, FIX_REGION, FIX_TYPE, ALTITUDE1, ALTITUDE2, SPEED_LIMIT, etc.
    /// - addNavaidsForLeg() collects fix references
    /// - loadNavaids() fetches coordinates for fixes
    /// - Coordinates come from WAYPOINT, VOR, NDB facility data
    /// </summary>
    public class SimConnectFacilityService : IDisposable
    {
        #region Facility API Availability Check

        private static bool? _facilityApiAvailable;

        public static bool IsFacilityApiAvailable
        {
            get
            {
                if (_facilityApiAvailable.HasValue)
                    return _facilityApiAvailable.Value;

                try
                {
                    var simConnectType = typeof(SimConnect);
                    var facilityDataEvent = simConnectType.GetEvent("OnRecvFacilityData");
                    var addToFacilityDef = simConnectType.GetMethod("AddToFacilityDefinition",
                        new[] { typeof(Enum), typeof(string) });

                    _facilityApiAvailable = (facilityDataEvent != null && addToFacilityDef != null);

                    Console.WriteLine($"[SimConnectFacility] Facility Data API available: {_facilityApiAvailable.Value}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SimConnectFacility] Error checking API: {ex.Message}");
                    _facilityApiAvailable = false;
                }

                return _facilityApiAvailable.Value;
            }
        }

        #endregion

        #region P/Invoke for RequestFacilityData_EX1

        // P/Invoke für RequestFacilityData_EX1 (nicht in C# Managed DLL verfügbar)
        // Siehe atools simconnectapi.h Zeile 351-353
        [DllImport("SimConnect.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int SimConnect_RequestFacilityData_EX1(
            IntPtr hSimConnect,
            uint DefineID,
            uint RequestID,
            string ICAO,
            string Region,
            byte Type);  // 'W'=0x57, 'V'=0x56, 'N'=0x4E

        private IntPtr _simConnectHandle = IntPtr.Zero;

        /// <summary>
        /// Get native SimConnect handle from managed object
        /// </summary>
        private IntPtr GetSimConnectHandle()
        {
            if (_simConnectHandle == IntPtr.Zero && _simConnect != null)
            {
                // Das Handle ist ein privates Feld in der Managed DLL
                var field = typeof(SimConnect).GetField("hSimConnect",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    _simConnectHandle = (IntPtr)field.GetValue(_simConnect);
            }
            return _simConnectHandle;
        }

        /// <summary>
        /// Request facility data with Type parameter - like atools RequestFacilityData_EX1
        /// </summary>
        private bool RequestFacilityDataEx1(uint defId, uint requestId, string icao, string region, int fixType)
        {
            IntPtr handle = GetSimConnectHandle();
            if (handle == IntPtr.Zero)
            {
                Console.WriteLine("[SimConnectFacility] ERROR: SimConnect handle is null");
                return false;
            }

            // Type kommt bereits als Buchstabe: 'W'=87, 'V'=86, 'N'=78
            // Oder als SimConnect Enum: 5=Waypoint, 1=VOR, 2=NDB
            byte typeChar;
            switch (fixType)
            {
                case 'W':  // 87 - already a letter
                case 5:    // SimConnect Waypoint enum
                    typeChar = (byte)'W';
                    break;
                case 'V':  // 86 - already a letter
                case 1:    // SimConnect VOR enum
                    typeChar = (byte)'V';
                    break;
                case 'N':  // 78 - already a letter
                case 2:    // SimConnect NDB enum
                    typeChar = (byte)'N';
                    break;
                default:
                    typeChar = (byte)'W'; // Default to Waypoint
                    break;
            }

            try
            {
                // Debug: Log first 10 requests
                if (_navaidExceptionCount < 10)
                {
                    Console.WriteLine($"[P/Invoke] Calling RequestFacilityData_EX1: handle=0x{handle.ToInt64():X}, def={defId}, req={requestId}, icao={icao}, region={region ?? ""}, type={(char)typeChar}");
                }

                int hr = SimConnect_RequestFacilityData_EX1(handle, defId, requestId, icao, region ?? "", typeChar);

                if (_navaidExceptionCount < 10)
                {
                    Console.WriteLine($"[P/Invoke] Result: HRESULT=0x{hr:X8} ({(hr == 0 ? "OK" : "FAIL")})");
                }

                if (hr != 0)
                {
                    _navaidExceptionCount++;
                }
                return hr == 0;  // S_OK
            }
            catch (Exception ex)
            {
                if (_navaidExceptionCount < 10)
                    Console.WriteLine($"[SimConnectFacility] RequestFacilityData_EX1 exception: {ex.Message}");
                _navaidExceptionCount++;
                return false;
            }
        }

        #endregion

        #region Force-Load Airports (MSFS 2024 SimConnect Bug Workaround)

        /// <summary>
        /// List of major airports known to have procedures but where MSFS 2024 SimConnect
        /// incorrectly reports numDepartures=0, numArrivals=0, numApproaches=0.
        /// These airports will be loaded regardless of SimConnect counts.
        /// Based on: https://forums.flightsimulator.com/t/missing-procedures-in-simconnect-facility-api
        /// </summary>
        private static readonly HashSet<string> ForceLoadAirports = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Germany - Major
            "EDDM", "EDDF", "EDDL", "EDDK", "EDDH", "EDDB", "EDDS", "EDDP", "EDDN", "EDDW",
            // Germany - Regional
            "EDDC", "EDDG", "EDDR", "EDDT", "EDDV",
            // Austria/Switzerland
            "LOWW", "LOWI", "LOWS", "LSZH", "LSGG", "LSZB",
            // UK
            "EGLL", "EGKK", "EGCC", "EGLC", "EGSS", "EGPH", "EGGW",
            // France
            "LFPG", "LFPO", "LFMN", "LFML", "LFBD", "LFBO", "LFLL",
            // Netherlands/Belgium
            "EHAM", "EHRD", "EBBR", "EBOS",
            // Spain/Portugal
            "LEMD", "LEBL", "LEPA", "LEMG", "LPPT", "LPFR",
            // Italy
            "LIRF", "LIMC", "LIPZ", "LIPE", "LIMF",
            // Scandinavia
            "ESSA", "ENGM", "EKCH", "EFHK", "ENZV",
            // USA - Major
            "KJFK", "KLAX", "KORD", "KATL", "KDFW", "KDEN", "KSFO", "KLAS", "KMIA", "KSEA",
            "KBOS", "KPHX", "KMSP", "KDTW", "KEWR", "KIAD", "KPHL", "KFLL", "KTPA", "KMCO",
            // USA - Regional
            "KSLC", "KSAN", "KPDX", "KCLT", "KBWI", "KDCA", "KRDU", "KSTL", "KCLE", "KPIT",
            // Canada
            "CYYZ", "CYVR", "CYUL", "CYYC", "CYOW",
            // Asia
            "RJTT", "RJAA", "RKSI", "VHHH", "WSSS", "VTBS",
            // Australia
            "YSSY", "YMML", "YBBN",
            // Middle East
            "OMDB", "OEJN", "LLBG"
        };

        #endregion

        #region Constants - exactly like atools FacilityDataDefinitionId

        private const int WM_USER_SIMCONNECT = 0x0403;

        // Facility Definition IDs - EXACTLY like atools enum (starts at 1000)
        private enum FacilityDefId : uint
        {
            AIRPORT_BASE = 1000,           // atools: FACILITY_DATA_AIRPORT_DEFINITION_ID
            AIRPORT_NUM = 1001,            // atools: +1
            AIRPORT_BASE_INFO = 1002,      // atools: +2
            AIRPORT_FREQ = 1003,           // atools: +3
            AIRPORT_HELIPAD = 1004,        // atools: +4
            AIRPORT_RW = 1005,             // atools: +5
            AIRPORT_START = 1006,          // atools: +6
            AIRPORT_PROC = 1007,           // atools: +7 - PROCEDURES
            AIRPORT_TAXI = 1008,           // atools: +8

            NAVAID_BASE = 2000,            // atools: FACILITY_DATA_NAVAID_DEFINITION_ID
            WAYPOINT_ROUTE = 2001,         // atools: +1
            WAYPOINT = 2002,               // atools: +2
            NDB = 2003,                    // atools: +3
            VOR = 2004                     // atools: +4
        }

        // For reflection - EXACTLY like atools IDs
        private enum FacilityDataDefinitionId { AIRPORT_NUM = 1001, AIRPORT_PROC = 1007, WAYPOINT = 2002, VOR = 2004, NDB = 2003 }
        private enum FacilityDataRequestId { BASE = 1000, AIRPORT_LIST = 100, AIRPORT_NUM_BASE = 2000 }

        #endregion

        #region Structs - exactly like atools

        /// <summary>
        /// Airport counts struct for AIRPORT_NUM definition - like atools AirportFacilityNum
        /// Fields match AddAirportNumFacilityDefinition(): ICAO, N_RUNWAYS, N_STARTS, etc.
        /// MUST be registered with RegisterFacilityDataDefineStruct!
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct AirportFacilityNumStruct
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string Icao;
            public int NumRunways;
            public int NumStarts;
            public int NumFrequencies;
            public int NumHelipads;
            public int NumApproaches;
            public int NumDepartures;
            public int NumArrivals;
        }

        /// <summary>
        /// Leg structure - EXACTLY like atools LegFacility from simconnectairport.h lines 340-419
        /// CRITICAL: All region fields MUST be 8 bytes like atools!
        /// CRITICAL: Field order MUST match AddLegDefinitions() order exactly!
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct LegFacility
        {
            public int Type;                    // Path termination type (TF, CF, DF, etc.)

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string FixIcao;              // char[8] in atools
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string FixRegion;            // char[8] in atools
            public int FixType;                 // 5=Waypoint, 1=VOR, 2=NDB

            public int FlyOver;                 // 1 = fly-over, 0 = fly-by
            public int DistanceMinute;          // qint32 in atools
            public int TrueDegree;              // qint32 in atools
            public int TurnDirection;           // 0=None, 1=Left, 2=Right

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string OriginIcao;           // char[8] in atools
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string OriginRegion;         // char[8] in atools
            public int OriginType;

            public float Theta;
            public float Rho;
            public float Course;
            public float RouteDistance;
            public int AltitudeDescription;     // approachAltDesc in atools
            public float Altitude1;
            public float Altitude2;

            public float SpeedLimit;            // float in atools
            public float VerticalAngle;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string ArcCenterFixIcao;     // char[8] in atools
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string ArcCenterFixRegion;   // char[8] in atools
            public int ArcCenterFixType;

            public float Radius;
            public int IsIaf;
            public int IsIf;
            public int IsFaf;
            public int IsMap;
            public float RequiredNavigationPerformance;
        }

        /// <summary>
        /// Waypoint structure - like atools WAYPOINT facility
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct WaypointFacility
        {
            public double Latitude;
            public double Longitude;
            public int Type;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string Icao;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]  // Fixed: was 4, atools uses 8
            public string Region;
        }

        /// <summary>
        /// VOR structure - like atools VOR facility
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct VorFacility
        {
            public double Latitude;
            public double Longitude;
            public double Altitude;
            public uint Frequency;      // Fixed: was double, atools uses uint32 (4 bytes)
            public int Type;
            public float Range;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string Icao;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]  // Fixed: was 4, atools uses 8
            public string Region;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string Name;
        }

        /// <summary>
        /// NDB structure - like atools NDB facility
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct NdbFacility
        {
            public double Latitude;
            public double Longitude;
            public double Altitude;
            public int Frequency;
            public int Type;
            public float Range;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string Icao;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]  // Fixed: was 4, atools uses 8
            public string Region;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string Name;
        }

        /// <summary>
        /// EXAKT wie atools simconnectairport.h Zeile 426-455
        /// DepartureFacility - SID Name (char[8])
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct DepartureFacility
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string Name;
        }

        /// <summary>
        /// EXAKT wie atools simconnectairport.h Zeile 426-455
        /// ArrivalFacility - STAR Name (char[8])
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct ArrivalFacility
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string Name;
        }

        /// <summary>
        /// EXAKT wie atools simconnectairport.h Zeile 426-455
        /// RunwayTransitionFacility - RW Nummer + Designator
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RunwayTransitionFacility
        {
            public int RunwayNumber;
            public int RunwayDesignator;
        }

        /// <summary>
        /// EXAKT wie atools simconnectairport.h Zeile 426-455
        /// EnrouteTransitionFacility - Transition Name (char[8])
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct EnrouteTransitionFacility
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string Name;
        }

        /// <summary>
        /// Fix identifier - like atools FacilityId for navaid lookup
        /// </summary>
        public struct FixId
        {
            public string Icao;
            public string Region;
            public int Type;  // Stored as-is, but normalized for comparison

            public FixId(string icao, string region, int type)
            {
                Icao = icao?.Trim() ?? "";
                Region = region?.Trim() ?? "";
                Type = type;
            }

            public bool IsValid => !string.IsNullOrEmpty(Icao);

            /// <summary>
            /// Normalize type for comparison - handles both letter codes and enum values
            /// 'W'=87 and 5 both map to 1 (Waypoint)
            /// 'V'=86 and 1 both map to 2 (VOR)
            /// 'N'=78 and 2 both map to 3 (NDB)
            /// Note: 'W', 'V', 'N' are char literals with int values 87, 86, 78
            /// </summary>
            private int NormalizedType
            {
                get
                {
                    switch (Type)
                    {
                        case 'W':  // = 87 (Waypoint letter code)
                        case 5:    // SimConnect enum SIMCONNECT_FACILITY_WAYPOINT
                            return 1;  // Waypoint
                        case 'V':  // = 86 (VOR letter code)
                        case 1:    // SimConnect enum SIMCONNECT_FACILITY_VOR
                            return 2;  // VOR
                        case 'N':  // = 78 (NDB letter code)
                        case 2:    // SimConnect enum SIMCONNECT_FACILITY_NDB
                            return 3;  // NDB
                        default:
                            return 1;  // Default waypoint
                    }
                }
            }

            public override int GetHashCode()
            {
                return (Icao?.ToUpper() ?? "").GetHashCode() ^
                       (Region?.ToUpper() ?? "").GetHashCode() ^
                       NormalizedType.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is FixId other)
                {
                    return string.Equals(Icao, other.Icao, StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(Region, other.Region, StringComparison.OrdinalIgnoreCase) &&
                           NormalizedType == other.NormalizedType;
                }
                return false;
            }
        }

        /// <summary>
        /// Navaid with coordinates - result of navaid lookup
        /// </summary>
        public class NavaidCoord
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string Icao { get; set; }
            public string Region { get; set; }
            public int Type { get; set; }
        }

        /// <summary>
        /// Airport facility counts - like atools AirportFacilityNum
        /// Used to quickly determine which airports have procedures
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct AirportFacilityNum
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
            public string Icao;
            public int NumRunways;
            public int NumStarts;
            public int NumFrequencies;
            public int NumHelipads;
            public int NumApproaches;
            public int NumDepartures;
            public int NumArrivals;

            public bool HasProcedures => NumApproaches > 0 || NumDepartures > 0 || NumArrivals > 0;
        }

        #endregion

        #region Fields

        private SimConnect _simConnect;
        private IntPtr _windowHandle;
        private bool _isConnected;
        private bool _disposed;
        private System.Timers.Timer _messageTimer;
        private readonly object _lock = new object();

        // Procedure data - like atools
        private readonly Dictionary<string, List<DepartureProcedure>> _departuresByAirport
            = new Dictionary<string, List<DepartureProcedure>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ArrivalProcedure>> _arrivalsByAirport
            = new Dictionary<string, List<ArrivalProcedure>>(StringComparer.OrdinalIgnoreCase);

        // Navaid cache - like atools navaidFacilities hash
        private readonly Dictionary<FixId, NavaidCoord> _navaidCache = new Dictionary<FixId, NavaidCoord>();

        // Pending fix requests - like atools navaidsToRequest set
        private readonly HashSet<FixId> _pendingNavaids = new HashSet<FixId>();

        // Pending navaid requests - maps FixId to TaskCompletionSource
        private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingNavaidRequests
            = new Dictionary<string, TaskCompletionSource<bool>>(StringComparer.OrdinalIgnoreCase);

        // Current request state
        private TaskCompletionSource<bool> _requestComplete;
        private string _currentAirportIcao;
        private DepartureProcedure _currentDeparture;
        private ArrivalProcedure _currentArrival;
        private uint _nextRequestId = 1000;

        // EXAKT wie atools simconnectloader.cpp Zeile 307-308
        // legsParentType2: 0=AIRPORT, 12=RUNWAY_TRANSITION, 13=ENROUTE_TRANSITION
        private int _legsParentType2 = 0;

        // Airport list loading - like atools requestAirportList()
        private List<string> _airportIds = new List<string>();
        private bool _airportListComplete;
        private int _airportListTotal;

        // Airport counts loading - like atools FACILITY_DATA_AIRPORT_NUM_DEFINITION_ID
        private Dictionary<string, AirportFacilityNum> _airportCounts = new Dictionary<string, AirportFacilityNum>(StringComparer.OrdinalIgnoreCase);
        private int _pendingCountRequests;
        private int _receivedCountRequests;

        // Procedure batch loading - Fire-and-Forget like atools
        private int _pendingProcRequests;
        private int _receivedProcRequests;
        private HashSet<string> _pendingProcIcaos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Current facility definition being processed - like atools currentFacilityDefinition
        // This is set before sending batch requests to know how to interpret AIRPORT data
        private FacilityDefId _currentFacilityDefinition = FacilityDefId.AIRPORT_BASE;

        // Navaid batch loading - Fire-and-Forget like atools requestNavaids()
        // atools: facilitiesFetchedBatch, numException counters
        private int _pendingNavaidBatchRequests;
        private int _receivedNavaidBatchRequests;
        private int _navaidExceptionCount;
        private int _navaidDataCount;  // Debug counter for navaid data packets
        private int _waypointsReceived;  // Debug counter

        #endregion

        #region Procedure Classes

        public class DepartureProcedure
        {
            public string Name { get; set; }
            public List<LegFacility> CommonLegs { get; set; } = new List<LegFacility>();
            public List<RunwayTransition> RunwayTransitions { get; set; } = new List<RunwayTransition>();
            public List<EnrouteTransition> EnrouteTransitions { get; set; } = new List<EnrouteTransition>();
        }

        public class ArrivalProcedure
        {
            public string Name { get; set; }
            public List<LegFacility> CommonLegs { get; set; } = new List<LegFacility>();
            public List<RunwayTransition> RunwayTransitions { get; set; } = new List<RunwayTransition>();
            public List<EnrouteTransition> EnrouteTransitions { get; set; } = new List<EnrouteTransition>();
        }

        public class RunwayTransition
        {
            public int RunwayNumber { get; set; }
            public int RunwayDesignator { get; set; }
            public List<LegFacility> Legs { get; set; } = new List<LegFacility>();
        }

        public class EnrouteTransition
        {
            public string Name { get; set; }
            public List<LegFacility> Legs { get; set; } = new List<LegFacility>();
        }

        #endregion

        #region Properties

        public bool IsConnected => _isConnected;

        #endregion

        #region Constructor

        public SimConnectFacilityService(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        #endregion

        #region Public Methods

        public bool Connect()
        {
            if (!IsFacilityApiAvailable)
            {
                Console.WriteLine("[SimConnectFacility] Facility API not available");
                return false;
            }

            try
            {
                _simConnect = new SimConnect("Kneeboard Facility Service", _windowHandle, WM_USER_SIMCONNECT, null, 0);

                _simConnect.OnRecvOpen += OnRecvOpen;
                _simConnect.OnRecvQuit += OnRecvQuit;
                _simConnect.OnRecvException += OnRecvException;
                _simConnect.OnRecvFacilityData += OnRecvFacilityData;
                _simConnect.OnRecvFacilityDataEnd += OnRecvFacilityDataEnd;
                _simConnect.OnRecvAirportList += OnRecvAirportList;

                _messageTimer = new System.Timers.Timer(100);
                _messageTimer.Elapsed += (s, e) => {
                    try { _simConnect?.ReceiveMessage(); }
                    catch { }
                };
                _messageTimer.Start();

                Console.WriteLine("[SimConnectFacility] Connecting to MSFS...");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Connect failed: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                _messageTimer?.Stop();
                _messageTimer?.Dispose();
                _messageTimer = null;

                if (_simConnect != null)
                {
                    try
                    {
                        _simConnect.OnRecvOpen -= OnRecvOpen;
                        _simConnect.OnRecvQuit -= OnRecvQuit;
                        _simConnect.OnRecvException -= OnRecvException;
                        _simConnect.OnRecvFacilityData -= OnRecvFacilityData;
                        _simConnect.OnRecvFacilityDataEnd -= OnRecvFacilityDataEnd;
                        _simConnect.OnRecvAirportList -= OnRecvAirportList;
                    }
                    catch { }

                    _simConnect.Dispose();
                    _simConnect = null;
                }
                _isConnected = false;
            }
        }

        /// <summary>
        /// Get SIDs for an airport - like atools
        /// Returns procedure summaries with names
        /// </summary>
        public async Task<List<ProcedureSummary>> GetSIDsAsync(string airportIcao)
        {
            await LoadAirportProceduresAsync(airportIcao);

            lock (_lock)
            {
                if (_departuresByAirport.TryGetValue(airportIcao.ToUpper(), out var deps))
                {
                    return deps.Select(d => new ProcedureSummary
                    {
                        Identifier = d.Name,
                        Type = ProcedureType.SID,
                        Runways = d.RunwayTransitions.Select(rt => FormatRunway(rt.RunwayNumber, rt.RunwayDesignator)).ToList(),
                        Transitions = d.EnrouteTransitions.Select(et => et.Name).ToList()
                    }).ToList();
                }
            }

            return new List<ProcedureSummary>();
        }

        /// <summary>
        /// Get STARs for an airport - like atools
        /// </summary>
        public async Task<List<ProcedureSummary>> GetSTARsAsync(string airportIcao)
        {
            await LoadAirportProceduresAsync(airportIcao);

            lock (_lock)
            {
                if (_arrivalsByAirport.TryGetValue(airportIcao.ToUpper(), out var arrs))
                {
                    return arrs.Select(a => new ProcedureSummary
                    {
                        Identifier = a.Name,
                        Type = ProcedureType.STAR,
                        Runways = a.RunwayTransitions.Select(rt => FormatRunway(rt.RunwayNumber, rt.RunwayDesignator)).ToList(),
                        Transitions = a.EnrouteTransitions.Select(et => et.Name).ToList()
                    }).ToList();
                }
            }

            return new List<ProcedureSummary>();
        }

        /// <summary>
        /// Get procedure detail with waypoints - EXACTLY like atools
        /// Collects fix references from legs, loads navaids, then combines
        /// </summary>
        public async Task<ProcedureDetail> GetProcedureDetailAsync(string airportIcao, string procedureName,
            string transition, ProcedureType type)
        {
            await LoadAirportProceduresAsync(airportIcao);

            List<LegFacility> legs;
            string icao = airportIcao.ToUpper();

            lock (_lock)
            {
                if (type == ProcedureType.SID)
                {
                    if (!_departuresByAirport.TryGetValue(icao, out var deps))
                        return null;

                    var dep = deps.FirstOrDefault(d =>
                        string.Equals(d.Name, procedureName, StringComparison.OrdinalIgnoreCase));
                    if (dep == null) return null;

                    legs = GetDepartureLegs(dep, transition);
                }
                else
                {
                    if (!_arrivalsByAirport.TryGetValue(icao, out var arrs))
                        return null;

                    var arr = arrs.FirstOrDefault(a =>
                        string.Equals(a.Name, procedureName, StringComparison.OrdinalIgnoreCase));
                    if (arr == null) return null;

                    legs = GetArrivalLegs(arr, transition);
                }
            }

            // Like atools: collect fix references from legs
            var fixesToLoad = new HashSet<FixId>();
            foreach (var leg in legs)
            {
                AddNavaidForLeg(leg, fixesToLoad);
            }

            // Load navaids to get coordinates
            await LoadNavaidsAsync(fixesToLoad);

            // Convert legs to waypoints with coordinates
            var waypoints = new List<ProcedureWaypoint>();
            int seq = 1;
            foreach (var leg in legs)
            {
                var wp = ConvertLegToWaypoint(leg, seq++);
                waypoints.Add(wp);
            }

            return new ProcedureDetail
            {
                Summary = new ProcedureSummary
                {
                    Identifier = procedureName,
                    Airport = airportIcao,
                    Type = type
                },
                Transition = transition,
                Waypoints = waypoints,
                DataSource = "SimConnect"
            };
        }

        /// <summary>
        /// Like atools requestAirportList() - Request all airport idents
        /// </summary>
        public async Task<List<string>> RequestAirportListAsync()
        {
            if (!_isConnected)
            {
                Console.WriteLine("[SimConnectFacility] Not connected, cannot request airport list");
                return new List<string>();
            }

            _airportIds.Clear();
            _airportListComplete = false;
            _airportListTotal = 0;

            Console.WriteLine("[SimConnectFacility] Requesting airport list...");

            try
            {
                // Request airport list - like atools line 813
                _simConnect.RequestFacilitiesList(
                    SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT,
                    FacilityDataRequestId.AIRPORT_LIST);

                // Aktiv pollen wie atools - line 816-823
                int timeout = 0;
                while (!_airportListComplete && timeout < 3000)  // 30s max (10ms × 3000)
                {
                    // Process multiple messages per iteration (like atools)
                    for (int j = 0; j < 20; j++)
                    {
                        try { _simConnect.ReceiveMessage(); }
                        catch { break; }
                    }
                    await Task.Delay(10);  // 10ms like atools (was 100ms)
                    timeout++;
                }

                // Thread-safe copy of airport list with duplicate/invalid filtering
                lock (_lock)
                {
                    int rawCount = _airportIds.Count;

                    // Collect invalid entries for analysis
                    var invalidEntries = _airportIds
                        .Where(id => !IsValidAirportIdent(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // Log invalid entries for debugging
                    if (invalidEntries.Count > 0)
                    {
                        Console.WriteLine($"[SimConnectFacility] Invalid airport entries ({invalidEntries.Count}):");
                        foreach (var invalid in invalidEntries.Take(50)) // Show first 50
                        {
                            string reason = GetInvalidReason(invalid);
                            Console.WriteLine($"  '{invalid}' - {reason}");
                        }
                        if (invalidEntries.Count > 50)
                            Console.WriteLine($"  ... and {invalidEntries.Count - 50} more");
                    }

                    // Find duplicates
                    var duplicateEntries = _airportIds
                        .GroupBy(id => id.ToUpperInvariant())
                        .Where(g => g.Count() > 1)
                        .Select(g => new { Ident = g.Key, Count = g.Count() })
                        .ToList();

                    if (duplicateEntries.Count > 0)
                    {
                        Console.WriteLine($"[SimConnectFacility] Duplicate airport entries ({duplicateEntries.Count}):");
                        foreach (var dup in duplicateEntries.Take(20)) // Show first 20
                        {
                            Console.WriteLine($"  '{dup.Ident}' appears {dup.Count}x");
                        }
                        if (duplicateEntries.Count > 20)
                            Console.WriteLine($"  ... and {duplicateEntries.Count - 20} more");
                    }

                    // Filter duplicates and invalid entries
                    var validatedList = _airportIds
                        .Where(id => IsValidAirportIdent(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(id => id)
                        .ToList();

                    int duplicates = rawCount - _airportIds.Distinct(StringComparer.OrdinalIgnoreCase).Count();

                    Console.WriteLine($"[SimConnectFacility] Got {rawCount} airports raw");
                    Console.WriteLine($"[SimConnectFacility] Filtered: {duplicates} duplicates, {invalidEntries.Count} invalid entries");
                    Console.WriteLine($"[SimConnectFacility] Valid airports: {validatedList.Count}");

                    return validatedList;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Error requesting airport list: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Like atools requestAirports(FACILITY_DATA_AIRPORT_NUM_DEFINITION_ID) - Get counts for all airports
        /// This is FAST because we only get counts, not full data
        /// </summary>
        public async Task<List<AirportFacilityNum>> RequestAirportCountsAsync(List<string> airportIds, IProgress<(string message, int current, int total)> progress = null, Stopwatch totalStopwatch = null)
        {
            if (!_isConnected)
            {
                Console.WriteLine("[SimConnectFacility] Not connected, cannot request airport counts");
                return new List<AirportFacilityNum>();
            }

            _airportCounts.Clear();
            _pendingCountRequests = 0;
            _receivedCountRequests = 0;

            // CRITICAL: Set current facility definition BEFORE sending requests
            // Like atools line 854: currentFacilityDefinition = definitionId
            _currentFacilityDefinition = FacilityDefId.AIRPORT_NUM;

            int total = airportIds.Count;
            Console.WriteLine($"[SimConnectFacility] Requesting counts for {total} airports (def={_currentFacilityDefinition})...");

            var phaseStopwatch = Stopwatch.StartNew();

            try
            {
                int batchSize = 2000;  // Like atools Zeile 329!
                uint requestIdBase = 10000;

                for (int i = 0; i < total; i += batchSize)
                {
                    var batch = airportIds.Skip(i).Take(batchSize).ToList();
                    _pendingCountRequests = batch.Count;
                    _receivedCountRequests = 0;

                    // Send batch requests
                    foreach (var icao in batch)
                    {
                        _simConnect.RequestFacilityData(
                            (FacilityDataDefinitionId)FacilityDefId.AIRPORT_NUM,
                            (FacilityDataRequestId)(requestIdBase++),
                            icao,
                            "");
                    }

                    // Wait for batch to complete - like atools line 930-941
                    int timeout = 0;
                    while (_receivedCountRequests < _pendingCountRequests && timeout < 300)  // 30s max per batch
                    {
                        // Process multiple messages per iteration (like atools)
                        for (int j = 0; j < 20; j++)
                        {
                            try { _simConnect.ReceiveMessage(); }
                            catch { break; }
                        }
                        await Task.Delay(10);  // 10ms like atools (was 100ms)
                        timeout++;
                    }

                    // Progress update - Phase 2: Counts (time is shown live by UI timer)
                    int completed = Math.Min(i + batchSize, total);
                    int percent = (int)((completed * 100.0) / total);
                    progress?.Report(($"2/5: {percent}%", completed, total));

                    if (completed % 500 == 0 || completed == total)
                        Console.WriteLine($"[SimConnectFacility] Loaded counts for {completed}/{total} airports ({percent}%)");
                }

                lock (_lock)
                {
                    var result = _airportCounts.Values.ToList();
                    Console.WriteLine($"[SimConnectFacility] Got counts for {result.Count} airports");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Error requesting airport counts: {ex.Message}");
                return new List<AirportFacilityNum>();
            }
        }

        /// <summary>
        /// Format elapsed time as mm:ss
        /// </summary>
        private static string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        /// <summary>
        /// Calculate estimated remaining time based on progress
        /// </summary>
        private static TimeSpan EstimateRemaining(TimeSpan elapsed, int current, int total)
        {
            if (current <= 0 || total <= 0)
                return TimeSpan.Zero;

            double rate = elapsed.TotalSeconds / current;
            int remaining = total - current;
            return TimeSpan.FromSeconds(rate * remaining);
        }

        /// <summary>
        /// Like atools loadAirports() - Load all airports and procedures into database
        /// Uses TWO-PHASE approach like atools:
        /// 1. Get all airport idents
        /// 2. Get COUNTS for all airports (FAST!)
        /// 3. Filter to only airports with procedures
        /// 4. Load full procedures only for those airports
        /// </summary>
        public async Task LoadAllAirportsAsync(NavdataDatabase db, IProgress<(string message, int current, int total)> progress = null)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var phaseStopwatch = new Stopwatch();

            // Phase 1: Get all airport idents - like atools requestAirportList()
            phaseStopwatch.Restart();
            progress?.Report(("1/5: Liste...", 0, 0));
            var allAirportIds = await RequestAirportListAsync();

            if (allAirportIds.Count == 0)
            {
                Console.WriteLine("[SimConnectFacility] No airports found");
                return;
            }

            Console.WriteLine($"[SimConnectFacility] Phase 1 complete: {allAirportIds.Count} airports in {FormatTime(phaseStopwatch.Elapsed)}");

            // Phase 2: Get COUNTS for all airports - like atools requestAirports(FACILITY_DATA_AIRPORT_NUM_DEFINITION_ID)
            // This is FAST because we only get counts, not full procedure data
            phaseStopwatch.Restart();
            progress?.Report(($"2/5: 0%", 0, allAirportIds.Count));
            var airportCounts = await RequestAirportCountsAsync(allAirportIds, progress, totalStopwatch);

            // Phase 3: Filter to only airports with procedures - like atools line 890-893
            // PLUS: Load ALL ICAO airports (4-letter codes) regardless of HasProcedures
            // This bypasses the SimConnect bug where numDepartures=0 for major airports like EDDM, EDDF
            var airportsWithProcedures = airportCounts
                .Where(a => a.HasProcedures ||
                       (a.Icao?.Trim()?.Length == 4 && a.Icao.Trim().All(char.IsLetter)))  // All ICAO airports!
                .Select(a => a.Icao?.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            // Count how many are from SimConnect vs ICAO-forced
            int simConnectCount = airportCounts.Count(a => a.HasProcedures);
            int icaoForceCount = airportsWithProcedures.Count - simConnectCount;

            Console.WriteLine($"[SimConnectFacility] Phase 2 complete: {airportCounts.Count} counts loaded in {FormatTime(phaseStopwatch.Elapsed)}");
            Console.WriteLine($"[SimConnectFacility] Phase 3: {airportsWithProcedures.Count} airports to load ({simConnectCount} SimConnect + {icaoForceCount} ICAO force-load)");

            // Phase 3b: Find airports with BGL data but 0 SimConnect procedures
            List<string> bglFallbackAirports = new List<string>();
            MsfsNavdataService msfsNavdata = null;
            try
            {
                msfsNavdata = new MsfsNavdataService(MsfsVersion.MSFS2024);
                if (msfsNavdata.IsAvailable)
                {
                    msfsNavdata.IndexNavdata();
                    var bglAirports = msfsNavdata.GetIndexedAirportIcaos().ToList();

                    bglFallbackAirports = bglAirports
                        .Where(icao => !airportsWithProcedures.Contains(icao, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    Console.WriteLine($"[SimConnectFacility] Phase 3b: {bglFallbackAirports.Count} airports in BGL but not in SimConnect (will use BGL fallback)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Phase 3b error: {ex.Message}");
            }

            if (airportsWithProcedures.Count == 0 && bglFallbackAirports.Count == 0)
            {
                Console.WriteLine("[SimConnectFacility] No airports with procedures found");
                return;
            }

            // Phase 4: Load full procedures only for airports that have them
            // Using FIRE-AND-FORGET pattern like atools (batchSize = 2000, send all then wait)
            phaseStopwatch.Restart();
            int total = airportsWithProcedures.Count;
            progress?.Report(($"3/5: {airportsWithProcedures.Count} APT", 0, total));
            Console.WriteLine($"[SimConnectFacility] Phase 4: Loading procedures for {total} airports (Fire-and-Forget mode)");

            // CRITICAL: Set current facility definition BEFORE sending requests
            // Like atools line 854: currentFacilityDefinition = definitionId
            _currentFacilityDefinition = FacilityDefId.AIRPORT_PROC;

            db.BeginTransaction();

            try
            {
                int batchSize = 2000;  // Like atools Zeile 329!
                int sentInBatch = 0;
                int totalSent = 0;
                int saved = 0;
                var batchIcaos = new List<string>();

                // Reset procedure tracking
                lock (_lock)
                {
                    _pendingProcRequests = 0;
                    _receivedProcRequests = 0;
                    _pendingProcIcaos.Clear();
                }

                for (int i = 0; i < total; i++)
                {
                    string icao = airportsWithProcedures[i];

                    // Send request WITHOUT waiting (Fire-and-Forget!)
                    lock (_lock)
                    {
                        if (!_departuresByAirport.ContainsKey(icao))
                        {
                            _pendingProcIcaos.Add(icao);
                            _pendingProcRequests++;
                        }
                    }

                    uint requestId = _nextRequestId++;
                    _simConnect.RequestFacilityData(
                        (FacilityDataDefinitionId)FacilityDefId.AIRPORT_PROC,
                        (FacilityDataRequestId)requestId,
                        icao,
                        "");

                    batchIcaos.Add(icao);
                    sentInBatch++;
                    totalSent++;

                    // Every 2000 requests OR at end: wait for responses and write to DB
                    if (sentInBatch >= batchSize || i == total - 1)
                    {
                        // Wait for all responses in this batch
                        int timeout = 0;
                        int maxTimeout = 300;  // 30s max per batch (10ms × 300 = 3s, but with multiple ReceiveMessage)

                        while (_receivedProcRequests < _pendingProcRequests && timeout < maxTimeout)
                        {
                            // Process multiple messages per iteration (like atools)
                            for (int j = 0; j < 20; j++)
                            {
                                try { _simConnect.ReceiveMessage(); }
                                catch { break; }
                            }
                            await Task.Delay(10);  // Short delay like atools (50ms, we use 10ms)
                            timeout++;
                        }

                        // Write all received procedures to database
                        foreach (var batchIcao in batchIcaos)
                        {
                            WriteProceduresToDatabase(batchIcao, db);
                            saved++;
                        }

                        // Progress update
                        int percent = (int)((totalSent * 100.0) / total);
                        progress?.Report(($"4/5: {percent}%", totalSent, total));

                        // Reset for next batch
                        batchIcaos.Clear();
                        sentInBatch = 0;
                        lock (_lock)
                        {
                            _pendingProcRequests = 0;
                            _receivedProcRequests = 0;
                            _pendingProcIcaos.Clear();
                        }

                        // Commit batch to DB
                        db.CommitTransaction();
                        if (i < total - 1)
                            db.BeginTransaction();

                        Console.WriteLine($"[SimConnectFacility] Batch complete: {totalSent}/{total} airports ({percent}%)");
                    }
                }

                progress?.Report(($"Fertig: {saved} APT", total, total));
                Console.WriteLine($"[SimConnectFacility] Phase 4 complete: Saved {saved} airports with procedures to database in {FormatTime(phaseStopwatch.Elapsed)}");

                // Phase 4b: BGL-Fallback for airports not found via SimConnect (EDDM, EDDF, etc.)
                if (bglFallbackAirports.Count > 0 && msfsNavdata != null)
                {
                    phaseStopwatch.Restart();
                    progress?.Report(($"4b/5: BGL-Fallback", 0, bglFallbackAirports.Count));
                    Console.WriteLine($"[SimConnectFacility] Phase 4b: Loading {bglFallbackAirports.Count} airports from BGL files...");

                    db.BeginTransaction();
                    try
                    {
                        int bglLoaded = LoadAirportsFromBgl(bglFallbackAirports, db, msfsNavdata);
                        db.CommitTransaction();
                        saved += bglLoaded;
                        Console.WriteLine($"[SimConnectFacility] Phase 4b complete: {bglLoaded} airports loaded from BGL in {FormatTime(phaseStopwatch.Elapsed)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SimConnectFacility] Phase 4b error: {ex.Message}");
                        try { db.RollbackTransaction(); } catch { }
                    }
                }

                // Phase 5: Load navaids - EXAKT wie atools loadNavaids() (Zeile 662-675)
                int pendingCount;
                lock (_lock) { pendingCount = _pendingNavaids.Count; }
                Console.WriteLine($"[SimConnectFacility] Phase 5: Loading {pendingCount} navaids...");
                phaseStopwatch.Restart();

                if (pendingCount > 0)
                {
                    progress?.Report(($"5/5: Navaids", 0, pendingCount));
                    await LoadNavaidsForBulkAsync(db, progress);
                }

                Console.WriteLine($"[SimConnectFacility] Phase 5 complete: {_navaidCache.Count} navaids loaded in {FormatTime(phaseStopwatch.Elapsed)}");

                var totalTime = totalStopwatch.Elapsed;
                Console.WriteLine($"[SimConnectFacility] TOTAL TIME: {FormatTime(totalTime)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Error loading airports: {ex.Message}");
                Console.WriteLine($"[SimConnectFacility] Stack trace: {ex.StackTrace}");
                try { db.RollbackTransaction(); } catch { }
                // Don't rethrow - just log and continue
                // throw;
            }
        }

        /// <summary>
        /// DEBUG VERSION: Load only 3 test airports (EDDM, EDDF, KJFK) for fast debugging
        /// Use with --debug-import command line argument
        /// </summary>
        public async Task LoadDebugAirportsAsync(NavdataDatabase db, IProgress<(string message, int current, int total)> progress = null)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var debugAirports = new[] { "EDDM", "EDDF", "KJFK" };

            Console.WriteLine("====================================================");
            Console.WriteLine("[DEBUG] LoadDebugAirportsAsync - FAST DEBUG MODE");
            Console.WriteLine($"[DEBUG] Loading only {debugAirports.Length} airports: {string.Join(", ", debugAirports)}");
            Console.WriteLine("====================================================");

            // CRITICAL: Set current facility definition BEFORE sending requests
            _currentFacilityDefinition = FacilityDefId.AIRPORT_PROC;

            db.BeginTransaction();

            try
            {
                int saved = 0;

                // Reset procedure tracking
                lock (_lock)
                {
                    _pendingProcRequests = 0;
                    _receivedProcRequests = 0;
                    _pendingProcIcaos.Clear();
                }

                foreach (var icao in debugAirports)
                {
                    Console.WriteLine($"[DEBUG] Requesting procedures for {icao}...");
                    progress?.Report(($"Debug: {icao}", saved, debugAirports.Length));

                    // Send request
                    lock (_lock)
                    {
                        _pendingProcIcaos.Add(icao);
                        _pendingProcRequests++;
                    }

                    uint requestId = _nextRequestId++;
                    _simConnect.RequestFacilityData(
                        (FacilityDataDefinitionId)FacilityDefId.AIRPORT_PROC,
                        (FacilityDataRequestId)requestId,
                        icao,
                        "");

                    // Wait for response (single request)
                    int timeout = 0;
                    const int maxTimeout = 100; // 1 second per airport

                    while (_receivedProcRequests < _pendingProcRequests && timeout < maxTimeout)
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            try { _simConnect.ReceiveMessage(); }
                            catch { break; }
                        }
                        await Task.Delay(10);
                        timeout++;
                    }

                    // Write to database
                    WriteProceduresToDatabase(icao, db);
                    saved++;

                    Console.WriteLine($"[DEBUG] {icao}: Received={_receivedProcRequests}, Pending={_pendingProcRequests}");

                    // Check what we got
                    lock (_lock)
                    {
                        if (_departuresByAirport.TryGetValue(icao, out var deps))
                            Console.WriteLine($"[DEBUG] {icao}: {deps.Count} departures (SIDs)");
                        if (_arrivalsByAirport.TryGetValue(icao, out var arrs))
                            Console.WriteLine($"[DEBUG] {icao}: {arrs.Count} arrivals (STARs)");
                    }

                    // Reset for next airport
                    lock (_lock)
                    {
                        _pendingProcRequests = 0;
                        _receivedProcRequests = 0;
                        _pendingProcIcaos.Clear();
                    }
                }

                db.CommitTransaction();
                Console.WriteLine($"[DEBUG] Phase 1-4 complete: {saved} airports with procedures");

                // Phase 5: Load navaids (uses existing debug limit of 5 navaids)
                int pendingCount;
                lock (_lock) { pendingCount = _pendingNavaids.Count; }
                Console.WriteLine($"[DEBUG] Phase 5: Loading navaids (pending={pendingCount})...");

                if (pendingCount > 0)
                {
                    progress?.Report(("Debug: Navaids", 0, pendingCount));
                    await LoadNavaidsForBulkAsync(db, progress);
                }

                var totalTime = totalStopwatch.Elapsed;
                Console.WriteLine("====================================================");
                Console.WriteLine($"[DEBUG] LoadDebugAirportsAsync COMPLETE in {FormatTime(totalTime)}");
                Console.WriteLine($"[DEBUG] Cache has {_navaidCache.Count} navaids");
                Console.WriteLine("====================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ERROR: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack: {ex.StackTrace}");
                try { db.RollbackTransaction(); } catch { }
            }
        }

        /// <summary>
        /// EXAKT wie atools requestNavaids() (Zeile 955-1036) - Fire-and-Forget pattern!
        /// 1. Alle Requests abfeuern (Fire)
        /// 2. Mit CallDispatch/ReceiveMessage warten bis alle da sind (Forget)
        /// 3. Dann in DB schreiben
        /// </summary>
        private async Task LoadNavaidsForBulkAsync(NavdataDatabase db, IProgress<(string, int, int)> progress)
        {
            // Thread-safe copy to avoid "Collection was modified" exception
            List<FixId> navaidsToLoad;
            lock (_lock)
            {
                navaidsToLoad = _pendingNavaids.Where(f => !_navaidCache.ContainsKey(f)).ToList();
            }
            int total = navaidsToLoad.Count;

            if (total == 0)
            {
                Console.WriteLine("[SimConnectFacility] No navaids to load (all cached)");
                lock (_lock) { _pendingNavaids.Clear(); }
                return;
            }

            Console.WriteLine($"[SimConnectFacility] Loading {total} navaids with Fire-and-Forget (wie atools)...");

            // atools: batchSize = 2000 (Zeile 329: batchSize = 2000)
            const int batchSize = 2000;
            int requested = 0;
            int batchNum = 0;

            for (int i = 0; i < total; i += batchSize)
            {
                var batch = navaidsToLoad.Skip(i).Take(batchSize).ToList();
                batchNum++;

                // Reset batch counters - like atools Zeile 1009
                _pendingNavaidBatchRequests = 0;
                _receivedNavaidBatchRequests = 0;
                _navaidExceptionCount = 0;

                // Fire all requests in this batch - like atools Zeile 964-999
                foreach (var fix in batch)
                {
                    if (_navaidCache.ContainsKey(fix))
                        continue;

                    try
                    {
                        var defId = GetNavaidDefIdForFixType(fix.Type);
                        if (defId == 0) continue;

                        uint requestId = _nextRequestId++;

                        // P/Invoke RequestFacilityData_EX1 mit Type-Parameter wie atools!
                        // Die managed RequestFacilityData hat keinen Type-Parameter
                        bool success = RequestFacilityDataEx1(
                            (uint)defId,
                            requestId,
                            fix.Icao,
                            fix.Region ?? "",
                            fix.Type);

                        if (success)
                        {
                            _pendingNavaidBatchRequests++;
                            requested++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SimConnectFacility] Error requesting {fix.Icao}: {ex.Message}");
                        _navaidExceptionCount++;
                    }
                }

                // Wait for batch completion - like atools Zeile 1011-1022
                // while(facilitiesFetchedBatch + numException < requested)
                int waitLoops = 0;
                const int maxWaitLoops = 100; // 100 * 50ms = 5 seconds max per batch

                while (_receivedNavaidBatchRequests + _navaidExceptionCount < _pendingNavaidBatchRequests
                       && waitLoops < maxWaitLoops)
                {
                    // ReceiveMessage pumpt die Callbacks - like atools CallDispatch
                    try { _simConnect?.ReceiveMessage(); }
                    catch { }

                    await Task.Delay(50); // like atools navaidFetchDelay (Zeile 1021: QThread::msleep)
                    waitLoops++;
                }

                // Progress update - JEDER Batch wird geloggt!
                int percent = total > 0 ? ((i + batch.Count) * 100) / total : 100;
                progress?.Report(($"5/5: Navaids {percent}%", i + batch.Count, total));

                // Log JEDEN Batch für bessere Sichtbarkeit
                Console.WriteLine($"[Phase5] Batch {batchNum}: {_receivedNavaidBatchRequests}/{_pendingNavaidBatchRequests} recv, cache={_navaidCache.Count}, total={i + batch.Count}/{total} ({percent}%)");
            }

            Console.WriteLine($"[SimConnectFacility] Navaid loading complete: {requested} requested, {_navaidCache.Count} in cache");

            // Write to database - like atools writeNavaidsToDatabase() (Zeile 737-763)
            int written = 0;
            db.BeginTransaction();

            // Thread-safe copy for database writing
            List<FixId> fixesToWrite;
            lock (_lock) { fixesToWrite = _pendingNavaids.ToList(); }

            try
            {
                // Debug: Count by type before writing
                int typeWaypoint = 0, typeVor = 0, typeNdb = 0, typeOther = 0;
                foreach (var f in fixesToWrite)
                {
                    if (f.Type == 'W' || f.Type == 87 || f.Type == 5) typeWaypoint++;
                    else if (f.Type == 'V' || f.Type == 86 || f.Type == 1) typeVor++;
                    else if (f.Type == 'N' || f.Type == 78 || f.Type == 2) typeNdb++;
                    else typeOther++;
                }
                Console.WriteLine($"[Phase5] Fixes to write: WPT={typeWaypoint}, VOR={typeVor}, NDB={typeNdb}, Other={typeOther}");
                Console.WriteLine($"[Phase5] Cache has {_navaidCache.Count} entries");

                // DEBUG: Nur erste 3 VORs detailliert loggen
                int vorDebugCount = 0;
                foreach (var fix in fixesToWrite)
                {
                    // DEBUG: VOR Details loggen
                    if ((fix.Type == 'V' || fix.Type == 86 || fix.Type == 1) && vorDebugCount < 3)
                    {
                        vorDebugCount++;
                        Console.WriteLine($"[DEBUG VOR {vorDebugCount}] Looking for: ICAO={fix.Icao}, Region={fix.Region}, Type={fix.Type}");

                        // Check cache manually
                        bool foundInCache = _navaidCache.TryGetValue(fix, out var cachedCoord);
                        Console.WriteLine($"[DEBUG VOR {vorDebugCount}] Found in cache: {foundInCache}");

                        if (foundInCache)
                        {
                            Console.WriteLine($"[DEBUG VOR {vorDebugCount}] Cached: lat={cachedCoord.Latitude}, lon={cachedCoord.Longitude}, type={cachedCoord.Type}");
                        }
                        else
                        {
                            // Try to find similar entries
                            Console.WriteLine($"[DEBUG VOR {vorDebugCount}] Searching cache for similar entries...");
                            foreach (var kvp in _navaidCache)
                            {
                                if (string.Equals(kvp.Key.Icao, fix.Icao, StringComparison.OrdinalIgnoreCase))
                                {
                                    Console.WriteLine($"[DEBUG VOR {vorDebugCount}]   Found: ICAO={kvp.Key.Icao}, Region={kvp.Key.Region}, Type={kvp.Key.Type} -> lat={kvp.Value.Latitude}");
                                }
                            }
                        }
                    }

                    if (_navaidCache.TryGetValue(fix, out var coord))
                    {
                        // SimConnect FixType: 'W'=87 (Waypoint), 'V'=86 (VOR), 'N'=78 (NDB)
                        // atools/SDK enum: 5=Waypoint, 1=VOR, 2=NDB
                        if (fix.Type == 'W' || fix.Type == 87 || fix.Type == 5)  // Waypoint
                        {
                            db.InsertWaypoint(fix.Icao.Trim(), fix.Region?.Trim() ?? "", fix.Type, coord.Latitude, coord.Longitude);
                            written++;
                        }
                        else if (fix.Type == 'V' || fix.Type == 86 || fix.Type == 1)  // VOR
                        {
                            db.InsertVor(fix.Icao.Trim(), fix.Region?.Trim() ?? "", "", fix.Type, 0, 0, coord.Latitude, coord.Longitude);
                            written++;
                        }
                        else if (fix.Type == 'N' || fix.Type == 78 || fix.Type == 2)  // NDB
                        {
                            db.InsertNdb(fix.Icao.Trim(), fix.Region?.Trim() ?? "", "", fix.Type, 0, 0, coord.Latitude, coord.Longitude);
                            written++;
                        }
                    }
                }

                db.CommitTransaction();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Navaid loading error: {ex.Message}");
                Console.WriteLine($"[SimConnectFacility] Stack trace: {ex.StackTrace}");
                try { db.RollbackTransaction(); } catch { }
                // Don't rethrow - just continue
            }

            Console.WriteLine($"[SimConnectFacility] Total: {written} navaids written to database");
            lock (_lock) { _pendingNavaids.Clear(); }
        }

        /// <summary>
        /// Get facility definition ID for fix type - like atools Zeile 969-996
        /// </summary>
        private int GetNavaidDefIdForFixType(int fixType)
        {
            // fixType kann als Buchstabe ('W'=87, 'V'=86, 'N'=78) oder als Enum (5, 1, 2) kommen
            switch (fixType)
            {
                case 'W':  // 87 - letter
                case 5:    // SimConnect Waypoint enum
                    return (int)FacilityDefId.WAYPOINT;
                case 'V':  // 86 - letter
                case 1:    // SimConnect VOR enum
                    return (int)FacilityDefId.VOR;
                case 'N':  // 78 - letter
                case 2:    // SimConnect NDB enum
                    return (int)FacilityDefId.NDB;
                default:
                    return (int)FacilityDefId.WAYPOINT; // Default to waypoint
            }
        }

        /// <summary>
        /// Write loaded procedures to SQLite database - like atools writeAirportsToDatabase()
        /// </summary>
        public void WriteProceduresToDatabase(string icao, NavdataDatabase db)
        {
            lock (_lock)
            {
                // Get or create airport
                var airportId = db.GetAirportId(icao);
                if (airportId < 0)
                {
                    int numDeps = 0, numArrs = 0;
                    if (_departuresByAirport.TryGetValue(icao, out var deps))
                        numDeps = deps.Count;
                    if (_arrivalsByAirport.TryGetValue(icao, out var arrs))
                        numArrs = arrs.Count;

                    db.InsertAirport(icao, "", "", 0, 0, 0, 0, numDeps, numArrs);
                    airportId = db.GetAirportId(icao);
                }

                // Write SIDs
                if (_departuresByAirport.TryGetValue(icao, out var departures))
                {
                    foreach (var dep in departures)
                    {
                        var procId = db.InsertProcedure(airportId, "SID", "", dep.Name, "");

                        // Write common legs
                        int seq = 1;
                        foreach (var leg in dep.CommonLegs)
                        {
                            WriteLegToDatabase(db, procId, null, seq++, leg);
                        }

                        // Write runway transitions
                        foreach (var rwTrans in dep.RunwayTransitions)
                        {
                            string rwName = FormatRunway(rwTrans.RunwayNumber, rwTrans.RunwayDesignator);
                            var transId = db.InsertTransition(procId, "RW", rwName, rwTrans.RunwayNumber, rwTrans.RunwayDesignator);

                            int transSeq = 1;
                            foreach (var leg in rwTrans.Legs)
                            {
                                WriteLegToDatabase(db, null, transId, transSeq++, leg);
                            }
                        }

                        // Write enroute transitions
                        foreach (var enTrans in dep.EnrouteTransitions)
                        {
                            var transId = db.InsertTransition(procId, "EN", enTrans.Name, 0, 0);

                            int transSeq = 1;
                            foreach (var leg in enTrans.Legs)
                            {
                                WriteLegToDatabase(db, null, transId, transSeq++, leg);
                            }
                        }
                    }
                }

                // Write STARs
                if (_arrivalsByAirport.TryGetValue(icao, out var arrivals))
                {
                    foreach (var arr in arrivals)
                    {
                        var procId = db.InsertProcedure(airportId, "STAR", "", arr.Name, "");

                        // Write common legs
                        int seq = 1;
                        foreach (var leg in arr.CommonLegs)
                        {
                            WriteLegToDatabase(db, procId, null, seq++, leg);
                        }

                        // Write runway transitions
                        foreach (var rwTrans in arr.RunwayTransitions)
                        {
                            string rwName = FormatRunway(rwTrans.RunwayNumber, rwTrans.RunwayDesignator);
                            var transId = db.InsertTransition(procId, "RW", rwName, rwTrans.RunwayNumber, rwTrans.RunwayDesignator);

                            int transSeq = 1;
                            foreach (var leg in rwTrans.Legs)
                            {
                                WriteLegToDatabase(db, null, transId, transSeq++, leg);
                            }
                        }

                        // Write enroute transitions
                        foreach (var enTrans in arr.EnrouteTransitions)
                        {
                            var transId = db.InsertTransition(procId, "EN", enTrans.Name, 0, 0);

                            int transSeq = 1;
                            foreach (var leg in enTrans.Legs)
                            {
                                WriteLegToDatabase(db, null, transId, transSeq++, leg);
                            }
                        }
                    }
                }

                db.MarkAirportLoaded(icao);
            }
        }

        /// <summary>
        /// Write a single leg to the database
        /// </summary>
        private void WriteLegToDatabase(NavdataDatabase db, long? procId, long? transId, int seq, LegFacility leg)
        {
            db.InsertLeg(
                procId, transId, seq,
                leg.Type,
                leg.FixIcao?.Trim() ?? "", leg.FixRegion?.Trim() ?? "", leg.FixType,
                leg.FlyOver, leg.TurnDirection,
                leg.Course, leg.RouteDistance,
                leg.Altitude1, leg.Altitude2, leg.AltitudeDescription,
                (int)leg.SpeedLimit, leg.VerticalAngle,
                leg.ArcCenterFixIcao?.Trim() ?? "", leg.ArcCenterFixRegion?.Trim() ?? "", leg.ArcCenterFixType,
                leg.Radius, leg.Theta, leg.Rho,
                leg.IsIaf, leg.IsIf, leg.IsFaf, leg.IsMap,
                leg.RequiredNavigationPerformance
            );

            // Also cache the fix coordinates if we have them
            if (!string.IsNullOrEmpty(leg.FixIcao))
            {
                var fixId = new FixId(leg.FixIcao, leg.FixRegion, leg.FixType);
                if (_navaidCache.TryGetValue(fixId, out var navaid))
                {
                    db.InsertWaypoint(leg.FixIcao.Trim(), leg.FixRegion?.Trim() ?? "", leg.FixType, navaid.Latitude, navaid.Longitude);
                }
            }
        }

        /// <summary>
        /// Load airports from BGL files that SimConnect reported with 0 procedures
        /// This is the BGL-Fallback for EDDM, EDDF, etc.
        /// </summary>
        public int LoadAirportsFromBgl(List<string> icaos, NavdataDatabase db, MsfsNavdataService navdata)
        {
            int loaded = 0;
            Console.WriteLine($"[BGL-Fallback] Loading {icaos.Count} airports from BGL files...");

            foreach (var icao in icaos)
            {
                try
                {
                    // Get procedures from BGL parser
                    var sids = GetBglProcedures(navdata, icao, true);
                    var stars = GetBglProcedures(navdata, icao, false);

                    if (sids.Count == 0 && stars.Count == 0)
                    {
                        continue;
                    }

                    // Insert airport
                    db.InsertAirport(icao, "", "", 0, 0, 0, 0, sids.Count, stars.Count);
                    var airportId = db.GetAirportId(icao);

                    if (airportId < 0)
                    {
                        Console.WriteLine($"[BGL-Fallback] Failed to insert airport {icao}");
                        continue;
                    }

                    // Write SIDs
                    foreach (var sid in sids)
                    {
                        var procId = db.InsertProcedure(airportId, "SID", "", sid.Identifier, "");

                        // Write common legs
                        int seq = 1;
                        foreach (var leg in sid.CommonRouteLegs)
                        {
                            WriteBglLegToDatabase(db, procId, null, seq++, leg);
                        }

                        // Write runway transitions
                        foreach (var rwTrans in sid.RunwayTransitions)
                        {
                            var transId = db.InsertTransition(procId, "RW", rwTrans.Key, 0, 0);
                            int transSeq = 1;
                            foreach (var leg in rwTrans.Value)
                            {
                                WriteBglLegToDatabase(db, null, transId, transSeq++, leg);
                            }
                        }

                        // Write enroute transitions
                        foreach (var enTrans in sid.EnrouteTransitions)
                        {
                            var transId = db.InsertTransition(procId, "EN", enTrans.Key, 0, 0);
                            int transSeq = 1;
                            foreach (var leg in enTrans.Value)
                            {
                                WriteBglLegToDatabase(db, null, transId, transSeq++, leg);
                            }
                        }
                    }

                    // Write STARs
                    foreach (var star in stars)
                    {
                        var procId = db.InsertProcedure(airportId, "STAR", "", star.Identifier, "");

                        // Write common legs
                        int seq = 1;
                        foreach (var leg in star.CommonRouteLegs)
                        {
                            WriteBglLegToDatabase(db, procId, null, seq++, leg);
                        }

                        // Write runway transitions
                        foreach (var rwTrans in star.RunwayTransitions)
                        {
                            var transId = db.InsertTransition(procId, "RW", rwTrans.Key, 0, 0);
                            int transSeq = 1;
                            foreach (var leg in rwTrans.Value)
                            {
                                WriteBglLegToDatabase(db, null, transId, transSeq++, leg);
                            }
                        }

                        // Write enroute transitions
                        foreach (var enTrans in star.EnrouteTransitions)
                        {
                            var transId = db.InsertTransition(procId, "EN", enTrans.Key, 0, 0);
                            int transSeq = 1;
                            foreach (var leg in enTrans.Value)
                            {
                                WriteBglLegToDatabase(db, null, transId, transSeq++, leg);
                            }
                        }
                    }

                    db.MarkAirportLoaded(icao);
                    loaded++;

                    if (loaded % 100 == 0 || loaded == icaos.Count)
                    {
                        Console.WriteLine($"[BGL-Fallback] Loaded {loaded}/{icaos.Count} airports");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BGL-Fallback] Error loading {icao}: {ex.Message}");
                }
            }

            Console.WriteLine($"[BGL-Fallback] Complete: {loaded} airports loaded from BGL");
            return loaded;
        }

        /// <summary>
        /// Get BGL procedures for an airport
        /// </summary>
        private List<BglSidStar> GetBglProcedures(MsfsNavdataService navdata, string icao, bool isSid)
        {
            // MsfsNavdataService.GetSIDs/GetSTARs returns ProcedureSummary, but we need BglSidStar
            // Use the internal BglParser directly
            var bglPath = navdata.GetBglPathForAirport(icao);
            if (string.IsNullOrEmpty(bglPath))
                return new List<BglSidStar>();

            using (var parser = new BglParser(bglPath))
            {
                parser.Parse();
                return isSid ? parser.GetSIDs(icao) : parser.GetSTARs(icao);
            }
        }

        /// <summary>
        /// Write a BGL leg to the database
        /// </summary>
        private void WriteBglLegToDatabase(NavdataDatabase db, long? procId, long? transId, int seq, BglLeg leg)
        {
            // Convert FixType enum to int type for database
            int fixType = ConvertBglFixTypeToInt(leg.FixType);
            int arcCenterType = ConvertBglFixTypeToInt(leg.RecommendedFixType);

            db.InsertLeg(
                procId, transId, seq,
                (int)leg.Type,
                leg.FixIdent?.Trim() ?? "", leg.FixRegion?.Trim() ?? "", fixType,
                leg.IsFlyOver ? 1 : 0, (int)leg.TurnDirection,
                leg.Course, leg.DistanceOrTime,
                leg.Altitude1, leg.Altitude2, (int)leg.AltitudeDescriptor,
                (int)leg.SpeedLimit, leg.VerticalAngle,
                leg.RecommendedFixIdent?.Trim() ?? "", leg.RecommendedFixRegion?.Trim() ?? "", arcCenterType,
                leg.Rho, leg.Theta, leg.Rho,  // Radius, Theta, Rho
                0, 0, 0, 0,  // IsIaf, IsIf, IsFaf, IsMap (not available in BGL)
                0  // RequiredNavigationPerformance
            );
        }

        /// <summary>
        /// Convert BGL FixType enum to database int type (char code)
        /// </summary>
        private int ConvertBglFixTypeToInt(FixType type)
        {
            switch (type)
            {
                case FixType.VOR: return 'V';
                case FixType.NDB: return 'N';
                case FixType.TerminalNDB: return 'N';
                case FixType.Waypoint: return 'W';
                case FixType.TerminalWaypoint: return 'W';
                case FixType.Airport: return 'A';
                case FixType.Runway: return 'R';
                default: return 'W';  // Default to waypoint
            }
        }

        #endregion

        #region Private Methods - like atools

        /// <summary>
        /// Like atools addNavaidsForLeg() - collect fix references from a leg
        /// </summary>
        private void AddNavaidForLeg(LegFacility leg, HashSet<FixId> fixes)
        {
            // Fix reference
            if (!string.IsNullOrEmpty(leg.FixIcao))
            {
                var fixId = new FixId(leg.FixIcao, leg.FixRegion, leg.FixType);
                fixes.Add(fixId);
                if (fixes.Count <= 5)
                    Console.WriteLine($"[AddNavaidForLeg] Added fix: {leg.FixIcao} region={leg.FixRegion} type={leg.FixType}");
            }

            // Origin reference
            if (!string.IsNullOrEmpty(leg.OriginIcao))
                fixes.Add(new FixId(leg.OriginIcao, leg.OriginRegion, leg.OriginType));

            // Arc center reference
            if (!string.IsNullOrEmpty(leg.ArcCenterFixIcao))
                fixes.Add(new FixId(leg.ArcCenterFixIcao, leg.ArcCenterFixRegion, leg.ArcCenterFixType));
        }

        /// <summary>
        /// Like atools loadNavaids() - load coordinates for fix references
        /// </summary>
        private async Task LoadNavaidsAsync(HashSet<FixId> fixes)
        {
            var toLoad = new List<FixId>();

            lock (_lock)
            {
                foreach (var fix in fixes)
                {
                    if (fix.IsValid && !_navaidCache.ContainsKey(fix))
                        toLoad.Add(fix);
                }
            }

            if (toLoad.Count == 0)
                return;

            Console.WriteLine($"[SimConnectFacility] Loading {toLoad.Count} navaids...");

            foreach (var fix in toLoad)
            {
                await RequestNavaidAsync(fix);
            }

            Console.WriteLine($"[SimConnectFacility] Navaid cache now has {_navaidCache.Count} entries");
        }

        /// <summary>
        /// Request a single navaid - like atools requestNavaids
        /// </summary>
        private async Task RequestNavaidAsync(FixId fix)
        {
            if (!_isConnected || !fix.IsValid)
                return;

            try
            {
                var defId = GetNavaidDefId(fix.Type);
                if (defId == 0) return;

                var tcs = new TaskCompletionSource<bool>();
                uint requestId = _nextRequestId++;
                string key = $"{fix.Icao}_{fix.Region}_{fix.Type}";

                // Register this request so we can complete it when data arrives
                lock (_lock)
                {
                    _pendingNavaidRequests[key] = tcs;
                }

                // Request the navaid data via P/Invoke with Type parameter
                RequestFacilityDataEx1(
                    (uint)defId,
                    requestId,
                    fix.Icao,
                    fix.Region ?? "",
                    fix.Type);

                // Wait for response or timeout
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));

                // Clean up
                lock (_lock)
                {
                    _pendingNavaidRequests.Remove(key);
                }

                if (completed != tcs.Task)
                {
                    Console.WriteLine($"[SimConnectFacility] Timeout waiting for navaid {fix.Icao}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Error requesting navaid {fix.Icao}: {ex.Message}");
            }
        }

        private int GetNavaidDefId(int fixType)
        {
            // fixType kann als Buchstabe ('W'=87, 'V'=86, 'N'=78) oder als Enum (5, 1, 2) kommen
            switch (fixType)
            {
                case 'W':  // 87 - letter
                case 5:    // SimConnect Waypoint enum
                    return (int)FacilityDefId.WAYPOINT;  // 2002
                case 'V':  // 86 - letter
                case 1:    // SimConnect VOR enum
                    return (int)FacilityDefId.VOR;       // 2004
                case 'N':  // 78 - letter
                case 2:    // SimConnect NDB enum
                    return (int)FacilityDefId.NDB;       // 2003
                default:
                    return (int)FacilityDefId.WAYPOINT;
            }
        }

        /// <summary>
        /// Convert leg to waypoint with coordinates from navaid cache
        /// </summary>
        private ProcedureWaypoint ConvertLegToWaypoint(LegFacility leg, int sequence)
        {
            var wp = new ProcedureWaypoint
            {
                Sequence = sequence,
                Identifier = leg.FixIcao?.Trim() ?? "",
                PathTermination = GetPathTerminationType(leg.Type),
                TurnDirection = leg.TurnDirection == 1 ? "L" : (leg.TurnDirection == 2 ? "R" : null),
                IsFlyOver = leg.FlyOver == 1,
                MagneticCourse = leg.Course > 0 ? (double?)leg.Course : null,
                RouteDistance = leg.RouteDistance > 0 ? (double?)leg.RouteDistance : null,
                Altitude1 = leg.Altitude1 > 0 ? (int?)leg.Altitude1 : null,
                Altitude2 = leg.Altitude2 > 0 ? (int?)leg.Altitude2 : null,
                SpeedLimit = leg.SpeedLimit > 0 ? (int?)leg.SpeedLimit : null,
                VerticalAngle = leg.VerticalAngle != 0 ? (double?)leg.VerticalAngle : null,
                AltitudeDescription = GetAltitudeDescription(leg.AltitudeDescription),
                ArcRadius = leg.Radius > 0 ? (double?)leg.Radius : null,
                CenterWaypoint = leg.ArcCenterFixIcao?.Trim()
            };

            // Get coordinates from navaid cache
            var fixId = new FixId(leg.FixIcao, leg.FixRegion, leg.FixType);
            if (_navaidCache.TryGetValue(fixId, out var navaid))
            {
                wp.Latitude = navaid.Latitude;
                wp.Longitude = navaid.Longitude;
            }

            // Get arc center coordinates
            if (!string.IsNullOrEmpty(leg.ArcCenterFixIcao))
            {
                var centerId = new FixId(leg.ArcCenterFixIcao, leg.ArcCenterFixRegion, leg.ArcCenterFixType);
                if (_navaidCache.TryGetValue(centerId, out var center))
                {
                    wp.CenterLatitude = center.Latitude;
                    wp.CenterLongitude = center.Longitude;
                }
            }

            return wp;
        }

        private string GetPathTerminationType(int type)
        {
            switch (type)
            {
                case 1: return "IF";  // Initial Fix
                case 2: return "TF";  // Track to Fix
                case 3: return "CF";  // Course to Fix
                case 4: return "DF";  // Direct to Fix
                case 5: return "FA";  // Fix to Altitude
                case 6: return "FC";  // Track from Fix to Distance
                case 7: return "FD";  // Track from Fix to DME Distance
                case 8: return "FM";  // From Fix to Manual Termination
                case 9: return "CA";  // Course to Altitude
                case 10: return "CD"; // Course to DME Distance
                case 11: return "CI"; // Course to Intercept
                case 12: return "CR"; // Course to Radial Termination
                case 13: return "VA"; // Heading to Altitude
                case 14: return "VD"; // Heading to DME Distance
                case 15: return "VI"; // Heading to Intercept
                case 16: return "VM"; // Heading to Manual Termination
                case 17: return "VR"; // Heading to Radial Termination
                case 18: return "AF"; // Arc to Fix
                case 19: return "RF"; // Radius to Fix
                case 20: return "PI"; // Procedure Turn
                case 21: return "HA"; // Racetrack to Altitude
                case 22: return "HF"; // Racetrack to Fix
                case 23: return "HM"; // Racetrack to Manual Termination
                default: return "TF";
            }
        }

        private string GetAltitudeDescription(int desc)
        {
            switch (desc)
            {
                case 1: return "@";  // At
                case 2: return "+";  // At or above
                case 3: return "-";  // At or below
                case 4: return "B";  // Between
                default: return null;
            }
        }

        private List<LegFacility> GetDepartureLegs(DepartureProcedure dep, string transition)
        {
            var legs = new List<LegFacility>();

            // Runway transition legs (if specified)
            if (!string.IsNullOrEmpty(transition))
            {
                var rwTrans = dep.RunwayTransitions.FirstOrDefault(rt =>
                    FormatRunway(rt.RunwayNumber, rt.RunwayDesignator).Equals(transition, StringComparison.OrdinalIgnoreCase));
                if (rwTrans != null)
                    legs.AddRange(rwTrans.Legs);
            }

            // Common legs
            legs.AddRange(dep.CommonLegs);

            // Enroute transition legs (if specified)
            if (!string.IsNullOrEmpty(transition))
            {
                var enTrans = dep.EnrouteTransitions.FirstOrDefault(et =>
                    string.Equals(et.Name, transition, StringComparison.OrdinalIgnoreCase));
                if (enTrans != null)
                    legs.AddRange(enTrans.Legs);
            }

            return legs;
        }

        private List<LegFacility> GetArrivalLegs(ArrivalProcedure arr, string transition)
        {
            var legs = new List<LegFacility>();

            // Enroute transition legs (if specified)
            if (!string.IsNullOrEmpty(transition))
            {
                var enTrans = arr.EnrouteTransitions.FirstOrDefault(et =>
                    string.Equals(et.Name, transition, StringComparison.OrdinalIgnoreCase));
                if (enTrans != null)
                    legs.AddRange(enTrans.Legs);
            }

            // Common legs
            legs.AddRange(arr.CommonLegs);

            // Runway transition legs (if specified)
            if (!string.IsNullOrEmpty(transition))
            {
                var rwTrans = arr.RunwayTransitions.FirstOrDefault(rt =>
                    FormatRunway(rt.RunwayNumber, rt.RunwayDesignator).Equals(transition, StringComparison.OrdinalIgnoreCase));
                if (rwTrans != null)
                    legs.AddRange(rwTrans.Legs);
            }

            return legs;
        }

        private string FormatRunway(int number, int designator)
        {
            string rw = number.ToString("D2");
            switch (designator)
            {
                case 1: return rw + "L";
                case 2: return rw + "R";
                case 3: return rw + "C";
                case 4: return rw + "W";
                default: return rw;
            }
        }

        /// <summary>
        /// Validate airport ident - filter out invalid entries
        /// Valid ICAO codes: 3-4 characters, alphanumeric (e.g., EDDF, LAX, KJFK)
        /// Some MSFS airports have longer idents for helipads, etc.
        /// </summary>
        private bool IsValidAirportIdent(string ident)
        {
            if (string.IsNullOrWhiteSpace(ident))
                return false;

            string trimmed = ident.Trim();

            // Must have at least 2 characters
            if (trimmed.Length < 2)
                return false;

            // Must not be too long (ICAO max 4, but MSFS uses up to 7 for some)
            if (trimmed.Length > 7)
                return false;

            // First character must be a letter (standard ICAO format)
            if (!char.IsLetter(trimmed[0]))
                return false;

            // All characters must be alphanumeric
            foreach (char c in trimmed)
            {
                if (!char.IsLetterOrDigit(c))
                    return false;
            }

            // Filter out known invalid patterns
            // Some MSFS entries are test/placeholder entries
            if (trimmed.StartsWith("XX", StringComparison.OrdinalIgnoreCase))
                return false;

            if (trimmed.StartsWith("ZZ", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Get reason why an airport ident is invalid (for debugging)
        /// </summary>
        private string GetInvalidReason(string ident)
        {
            if (string.IsNullOrWhiteSpace(ident))
                return "empty/whitespace";

            string trimmed = ident.Trim();

            if (trimmed.Length < 2)
                return $"too short ({trimmed.Length} chars)";

            if (trimmed.Length > 7)
                return $"too long ({trimmed.Length} chars)";

            if (!char.IsLetter(trimmed[0]))
                return $"starts with non-letter '{trimmed[0]}'";

            foreach (char c in trimmed)
            {
                if (!char.IsLetterOrDigit(c))
                    return $"contains invalid char '{c}'";
            }

            if (trimmed.StartsWith("XX", StringComparison.OrdinalIgnoreCase))
                return "XX prefix (test/placeholder)";

            if (trimmed.StartsWith("ZZ", StringComparison.OrdinalIgnoreCase))
                return "ZZ prefix (test/placeholder)";

            return "unknown";
        }

        private async Task LoadAirportProceduresAsync(string airportIcao, bool verbose = false)
        {
            string icao = airportIcao.ToUpper();

            // Prüfe ob bereits Daten geladen sind (nicht nur ob Key existiert!)
            lock (_lock)
            {
                if (_departuresByAirport.TryGetValue(icao, out var existingDeps) && existingDeps.Count > 0)
                    return;
                if (_arrivalsByAirport.TryGetValue(icao, out var existingArrs) && existingArrs.Count > 0)
                    return;
            }

            if (!_isConnected)
                return;

            try
            {
                _currentAirportIcao = icao;
                _requestComplete = new TaskCompletionSource<bool>();
                _currentFacilityDefinition = FacilityDefId.AIRPORT_PROC;

                uint requestId = _nextRequestId++;
                _simConnect.RequestFacilityData(
                    (FacilityDataDefinitionId)FacilityDefId.AIRPORT_PROC,
                    (FacilityDataRequestId)requestId,
                    icao,
                    "");

                // Warte auf FacilityDataEnd event (max 5 Sekunden)
                int timeout = 0;
                while (!_requestComplete.Task.IsCompleted && timeout < 50)
                {
                    try
                    {
                        _simConnect.ReceiveMessage();
                    }
                    catch { }
                    await Task.Delay(100);
                    timeout++;
                }

                if (verbose)
                {
                    lock (_lock)
                    {
                        int sidCount = _departuresByAirport.TryGetValue(icao, out var deps) ? deps.Count : 0;
                        int starCount = _arrivalsByAirport.TryGetValue(icao, out var arrs) ? arrs.Count : 0;
                        Console.WriteLine($"[SimConnectFacility] {icao}: {sidCount} SIDs, {starCount} STARs");
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Event Handlers

        private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine($"[SimConnectFacility] Connected to {data.szApplicationName}");
            _isConnected = true;
            SetupFacilityDefinitions();
        }

        private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine("[SimConnectFacility] Simulator closed");
            Disconnect();
        }

        private void OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            // Increment exception counter - like atools numException++ (Zeile 1011)
            _navaidExceptionCount++;

            // Only log first 5 exceptions to avoid flooding console
            // ERROR exceptions are normal for navaids not found (fire-and-forget pattern)
            if (_navaidExceptionCount <= 5)
            {
                Console.WriteLine($"[SimConnectFacility] Exception #{_navaidExceptionCount}: {(SIMCONNECT_EXCEPTION)data.dwException}");
            }
            else if (_navaidExceptionCount == 6)
            {
                Console.WriteLine($"[SimConnectFacility] Suppressing further exception logs (normal for fire-and-forget)...");
            }
        }

        private void OnRecvFacilityData(SimConnect sender, SIMCONNECT_RECV_FACILITY_DATA data)
        {
            try
            {
                ProcessFacilityData(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Error processing data: {ex.Message}");
            }
        }

        private void OnRecvFacilityDataEnd(SimConnect sender, SIMCONNECT_RECV_FACILITY_DATA_END data)
        {
            // Increment navaid batch counter - like atools facilitiesFetchedBatch++
            // This is called when a facility request completes (success or not found)
            _receivedNavaidBatchRequests++;

            // Also increment proc counter for procedure loading
            _receivedProcRequests++;

            _requestComplete?.TrySetResult(true);
        }

        /// <summary>
        /// Handle airport list response - like atools dispatchProcedure SIMCONNECT_RECV_ID_AIRPORT_LIST
        /// </summary>
        private void OnRecvAirportList(SimConnect sender, SIMCONNECT_RECV_AIRPORT_LIST data)
        {
            try
            {
                lock (_lock)
                {
                    // Add airports to list - rgData is SIMCONNECT_DATA_FACILITY_AIRPORT[]
                    foreach (var airportObj in data.rgData)
                    {
                        var airport = (SIMCONNECT_DATA_FACILITY_AIRPORT)airportObj;
                        if (!string.IsNullOrEmpty(airport.Ident))
                        {
                            _airportIds.Add(airport.Ident.Trim());
                        }
                    }

                    _airportListTotal = (int)data.dwOutOf;

                    // Check if complete (dwEntryNumber is 0-based, dwOutOf is total count)
                    if (data.dwEntryNumber + data.dwArraySize >= data.dwOutOf)
                    {
                        _airportListComplete = true;
                        Console.WriteLine($"[SimConnectFacility] Airport list complete: {_airportIds.Count} airports");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Error processing airport list: {ex.Message}");
                _airportListComplete = true;  // Stop waiting on error
            }
        }

        private void ProcessFacilityData(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            // SIMCONNECT_FACILITY_DATA_TYPE values from SDK:
            // AIRPORT = 0, DEPARTURE = 10, ARRIVAL = 11,
            // APPROACH_LEG = 15, RUNWAY_TRANSITION = 16, ENROUTE_TRANSITION = 17,
            // WAYPOINT = 20, VOR = 21, NDB = 22
            int type = (int)data.Type;

            // DEBUG: Log navaid types (19-22) to see what we're getting
            if (type >= 19 && type <= 22)
            {
                _navaidDataCount++;
                if (_navaidDataCount <= 20)
                {
                    Console.WriteLine($"[SimConnectFacility] NAVAID DATA: type={type}, dataLen={data.Data?.Length ?? 0}");
                }
            }

            // Type 0 = AIRPORT - extract ICAO and track for batch mode
            if (type == 0)
            {
                ProcessAirportData(data);
                return;
            }

            // EXAKT wie atools simconnectdummy.h Zeile 372-402
            // 0=AIRPORT, 1=RUNWAY, 2=START, 3=FREQUENCY, 4=HELIPAD
            // 5=APPROACH, 6=APPROACH_TRANSITION, 7=APPROACH_LEG, 8=FINAL_APPROACH_LEG, 9=MISSED_APPROACH_LEG
            // 10=DEPARTURE, 11=ARRIVAL, 12=RUNWAY_TRANSITION, 13=ENROUTE_TRANSITION
            // 14=TAXI_POINT, 15=TAXI_PARKING, 16=TAXI_PATH, 17=TAXI_NAME, 18=JETWAY
            // 19=VOR, 20=NDB, 21=WAYPOINT, 22=ROUTE
            switch (type)
            {
                case 7: // APPROACH_LEG - für SID/STAR Legs!
                    ProcessLeg(data);
                    break;

                case 10: // DEPARTURE (SID)
                    ProcessDeparture(data);
                    break;

                case 11: // ARRIVAL (STAR)
                    ProcessArrival(data);
                    break;

                case 12: // RUNWAY_TRANSITION
                    ProcessRunwayTransition(data);
                    break;

                case 13: // ENROUTE_TRANSITION
                    ProcessEnrouteTransition(data);
                    break;

                case 19: // VOR
                    ProcessVor(data);
                    break;

                case 20: // NDB
                    ProcessNdb(data);
                    break;

                case 21: // WAYPOINT
                    ProcessWaypoint(data);
                    break;
            }
        }

        /// <summary>
        /// Process AIRPORT data - handles both AIRPORT_NUM (counts) and AIRPORT_PROC (procedures) requests
        /// Like atools dispatchProcedure - uses currentFacilityDefinition to determine data format
        /// For AIRPORT_NUM: Extract counts directly from binary data (like atools line 1328)
        /// For AIRPORT_PROC: Set current airport ICAO for subsequent procedure data
        /// </summary>
        private void ProcessAirportData(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0) return;

            try
            {
                // Like atools line 1323-1331: Check which definition we're processing
                if (_currentFacilityDefinition == FacilityDefId.AIRPORT_NUM)
                {
                    // AIRPORT_NUM: Extract counts from the data
                    // Data format matches our AddAirportNumFacilityDefinition(): ICAO + 7 ints
                    ProcessAirportNumData(data);
                }
                else
                {
                    // AIRPORT_PROC or other: Just extract ICAO for procedure context
                    ProcessAirportProcData(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Error processing airport data: {ex.Message}");
                lock (_lock)
                {
                    _receivedCountRequests++;  // Count as received even on error
                    _receivedProcRequests++;   // Also count proc requests on error
                }
            }
        }

        /// <summary>
        /// Process AIRPORT_NUM data - like atools line 1327-1331
        /// With RegisterFacilityDataDefineStruct, data.Data[0] is now AirportFacilityNumStruct!
        /// </summary>
        private void ProcessAirportNumData(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0)
            {
                lock (_lock) { _receivedCountRequests++; }
                return;
            }

            try
            {
                // With RegisterFacilityDataDefineStruct<AirportFacilityNumStruct>, data.Data[0] is the struct!
                var airportNum = (AirportFacilityNumStruct)data.Data[0];

                string icao = airportNum.Icao?.Trim();
                if (string.IsNullOrEmpty(icao))
                {
                    lock (_lock) { _receivedCountRequests++; }
                    return;
                }

                // Debug first few results
                if (_airportCounts.Count < 5)
                {
                    Console.WriteLine($"[SimConnectFacility] AIRPORT_NUM: {icao} - RW:{airportNum.NumRunways} APP:{airportNum.NumApproaches} DEP:{airportNum.NumDepartures} ARR:{airportNum.NumArrivals}");
                }

                var numData = new AirportFacilityNum
                {
                    Icao = icao,
                    NumRunways = airportNum.NumRunways,
                    NumStarts = airportNum.NumStarts,
                    NumFrequencies = airportNum.NumFrequencies,
                    NumHelipads = airportNum.NumHelipads,
                    NumApproaches = airportNum.NumApproaches,
                    NumDepartures = airportNum.NumDepartures,
                    NumArrivals = airportNum.NumArrivals
                };

                lock (_lock)
                {
                    _airportCounts[icao] = numData;
                    _receivedCountRequests++;
                }
            }
            catch (Exception ex)
            {
                if (_receivedCountRequests < 5)
                    Console.WriteLine($"[SimConnectFacility] AIRPORT_NUM error: {ex.Message}");
                lock (_lock) { _receivedCountRequests++; }
            }
        }

        /// <summary>
        /// Process AIRPORT_PROC data - like atools line 1357-1364
        /// Just extracts ICAO to set context for subsequent procedure data
        /// </summary>
        private void ProcessAirportProcData(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            var obj = data.Data[0];
            string icao = ExtractString(obj, "Icao");
            if (string.IsNullOrEmpty(icao))
                icao = ExtractString(obj, "ICAO");
            if (string.IsNullOrEmpty(icao))
                icao = ExtractString(obj, "icao");

            // Try byte array extraction as fallback
            if (string.IsNullOrEmpty(icao) && obj is byte[] bytes && bytes.Length >= 8)
            {
                int nullIndex = Array.IndexOf(bytes, (byte)0);
                if (nullIndex < 0) nullIndex = 8;
                icao = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(nullIndex, 8)).Trim();
            }

            if (string.IsNullOrEmpty(icao)) return;

            icao = icao.Trim();

            lock (_lock)
            {
                _currentAirportIcao = icao;

                // Track received procedure requests for batch mode
                if (_pendingProcIcaos.Contains(icao))
                {
                    _pendingProcIcaos.Remove(icao);
                    _receivedProcRequests++;
                }
            }
        }

        /// <summary>
        /// EXAKT wie atools simconnectloader.cpp Zeile 1585
        /// Mit RegisterFacilityDataDefineStruct kommt data.Data[0] als DepartureFacility!
        /// </summary>
        private void ProcessDeparture(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0) return;

            // EXAKT wie atools: Direct cast nach RegisterFacilityDataDefineStruct!
            var departure = (DepartureFacility)data.Data[0];
            string name = departure.Name?.TrimEnd('\0').Trim();

            if (string.IsNullOrEmpty(name)) return;

            Console.WriteLine($"[SID] {_currentAirportIcao}: {name}");

            lock (_lock)
            {
                if (!_departuresByAirport.ContainsKey(_currentAirportIcao))
                    _departuresByAirport[_currentAirportIcao] = new List<DepartureProcedure>();

                _currentDeparture = new DepartureProcedure { Name = name };
                _currentArrival = null;  // Reset arrival context
                _legsParentType2 = 0;    // Reset wie atools
                _departuresByAirport[_currentAirportIcao].Add(_currentDeparture);
            }
        }

        /// <summary>
        /// EXAKT wie atools simconnectloader.cpp Zeile 1592
        /// Mit RegisterFacilityDataDefineStruct kommt data.Data[0] als ArrivalFacility!
        /// </summary>
        private void ProcessArrival(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0) return;

            // EXAKT wie atools: Direct cast nach RegisterFacilityDataDefineStruct!
            var arrival = (ArrivalFacility)data.Data[0];
            string name = arrival.Name?.TrimEnd('\0').Trim();

            if (string.IsNullOrEmpty(name)) return;

            Console.WriteLine($"[STAR] {_currentAirportIcao}: {name}");

            lock (_lock)
            {
                if (!_arrivalsByAirport.ContainsKey(_currentAirportIcao))
                    _arrivalsByAirport[_currentAirportIcao] = new List<ArrivalProcedure>();

                _currentArrival = new ArrivalProcedure { Name = name };
                _currentDeparture = null;  // Reset departure context
                _legsParentType2 = 0;      // Reset wie atools
                _arrivalsByAirport[_currentAirportIcao].Add(_currentArrival);
            }
        }

        /// <summary>
        /// EXAKT wie atools simconnectloader.cpp Zeile 1555-1579
        /// Leg-Routing basiert auf _legsParentType2:
        /// - 0 = Common Legs (direkt zur Prozedur)
        /// - 12 = RUNWAY_TRANSITION Legs
        /// - 13 = ENROUTE_TRANSITION Legs
        /// </summary>
        private void ProcessLeg(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0) return;

            try
            {
                var leg = ExtractLeg(data.Data[0]);

                // Track pending navaids - coordinates will be fetched via RequestFacilityData_EX1
                AddNavaidForLeg(leg, _pendingNavaids);

                lock (_lock)
                {
                    // EXAKT wie atools Zeile 1555-1579
                    if (_currentDeparture != null)
                    {
                        if (_legsParentType2 == 12 && _currentDeparture.RunwayTransitions.Count > 0)
                        {
                            // Leg gehört zur letzten RunwayTransition
                            _currentDeparture.RunwayTransitions.Last().Legs.Add(leg);
                        }
                        else if (_legsParentType2 == 13 && _currentDeparture.EnrouteTransitions.Count > 0)
                        {
                            // Leg gehört zur letzten EnrouteTransition
                            _currentDeparture.EnrouteTransitions.Last().Legs.Add(leg);
                        }
                        else
                        {
                            // Common Leg
                            _currentDeparture.CommonLegs.Add(leg);
                        }
                    }
                    else if (_currentArrival != null)
                    {
                        if (_legsParentType2 == 12 && _currentArrival.RunwayTransitions.Count > 0)
                        {
                            // Leg gehört zur letzten RunwayTransition
                            _currentArrival.RunwayTransitions.Last().Legs.Add(leg);
                        }
                        else if (_legsParentType2 == 13 && _currentArrival.EnrouteTransitions.Count > 0)
                        {
                            // Leg gehört zur letzten EnrouteTransition
                            _currentArrival.EnrouteTransitions.Last().Legs.Add(leg);
                        }
                        else
                        {
                            // Common Leg
                            _currentArrival.CommonLegs.Add(leg);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// EXAKT wie atools simconnectloader.cpp Zeile 1600-1612
        /// Mit RegisterFacilityDataDefineStruct kommt data.Data[0] als RunwayTransitionFacility!
        /// </summary>
        private void ProcessRunwayTransition(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0) return;

            try
            {
                // EXAKT wie atools: Direct cast!
                var trans = (RunwayTransitionFacility)data.Data[0];

                lock (_lock)
                {
                    var rwTrans = new RunwayTransition
                    {
                        RunwayNumber = trans.RunwayNumber,
                        RunwayDesignator = trans.RunwayDesignator
                    };

                    if (_currentDeparture != null)
                        _currentDeparture.RunwayTransitions.Add(rwTrans);
                    else if (_currentArrival != null)
                        _currentArrival.RunwayTransitions.Add(rwTrans);

                    // EXAKT wie atools: legsParentType2 = RUNWAY_TRANSITION (12)
                    _legsParentType2 = 12;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] ProcessRunwayTransition error: {ex.Message}");
            }
        }

        /// <summary>
        /// EXAKT wie atools simconnectloader.cpp Zeile 1614-1628
        /// Mit RegisterFacilityDataDefineStruct kommt data.Data[0] als EnrouteTransitionFacility!
        /// </summary>
        private void ProcessEnrouteTransition(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0) return;

            try
            {
                // EXAKT wie atools: Direct cast!
                var trans = (EnrouteTransitionFacility)data.Data[0];
                string name = trans.Name?.TrimEnd('\0').Trim();

                lock (_lock)
                {
                    var enTrans = new EnrouteTransition { Name = name };

                    if (_currentDeparture != null)
                        _currentDeparture.EnrouteTransitions.Add(enTrans);
                    else if (_currentArrival != null)
                        _currentArrival.EnrouteTransitions.Add(enTrans);

                    // EXAKT wie atools: legsParentType2 = ENROUTE_TRANSITION (13)
                    _legsParentType2 = 13;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] ProcessEnrouteTransition error: {ex.Message}");
            }
        }

        private void ProcessWaypoint(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            _waypointsReceived++;
            if (_waypointsReceived <= 5)
                Console.WriteLine($"[ProcessWaypoint] Called! data.Data.Length={data.Data?.Length ?? 0}");

            if (data.Data == null || data.Data.Length == 0) return;

            try
            {
                // Mit RegisterFacilityDataDefineStruct kommt data.Data[0] als WaypointFacility!
                var wp = (WaypointFacility)data.Data[0];
                string icao = wp.Icao?.Trim();
                string region = wp.Region?.Trim();

                if (!string.IsNullOrEmpty(icao))
                {
                    lock (_lock)
                    {
                        // Type 5 = SIMCONNECT_FACILITY_DATA_TYPE_WAYPOINT (from LegFacility.FixType)
                        var fixId = new FixId(icao, region, 5);
                        _navaidCache[fixId] = new NavaidCoord
                        {
                            Latitude = wp.Latitude,
                            Longitude = wp.Longitude,
                            Icao = icao,
                            Region = region,
                            Type = 5
                        };
                        CompleteNavaidRequest(icao, region, 5);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_navaidExceptionCount < 5)
                    Console.WriteLine($"[SimConnectFacility] ProcessWaypoint error: {ex.Message}");
                _navaidExceptionCount++;
            }
        }

        private void ProcessVor(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0) return;

            try
            {
                // Mit RegisterFacilityDataDefineStruct kommt data.Data[0] als VorFacility!
                var vor = (VorFacility)data.Data[0];
                string icao = vor.Icao?.Trim();
                string region = vor.Region?.Trim();

                Console.WriteLine($"[DEBUG ProcessVor] Received: ICAO={icao}, Region={region}, Lat={vor.Latitude}, Lon={vor.Longitude}");

                if (!string.IsNullOrEmpty(icao))
                {
                    lock (_lock)
                    {
                        // Type 1 = SIMCONNECT_FACILITY_DATA_TYPE_VOR (from LegFacility.FixType)
                        var fixId = new FixId(icao, region, 1);
                        _navaidCache[fixId] = new NavaidCoord
                        {
                            Latitude = vor.Latitude,
                            Longitude = vor.Longitude,
                            Icao = icao,
                            Region = region,
                            Type = 1
                        };
                        Console.WriteLine($"[DEBUG ProcessVor] Added to cache: key=(ICAO={fixId.Icao}, Region={fixId.Region}, Type={fixId.Type})");
                        CompleteNavaidRequest(icao, region, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] ProcessVor error: {ex.Message}");
                _navaidExceptionCount++;
            }
        }

        private void ProcessNdb(SIMCONNECT_RECV_FACILITY_DATA data)
        {
            if (data.Data == null || data.Data.Length == 0) return;

            try
            {
                // Mit RegisterFacilityDataDefineStruct kommt data.Data[0] als NdbFacility!
                var ndb = (NdbFacility)data.Data[0];
                string icao = ndb.Icao?.Trim();
                string region = ndb.Region?.Trim();

                if (!string.IsNullOrEmpty(icao))
                {
                    lock (_lock)
                    {
                        // Type 2 = SIMCONNECT_FACILITY_DATA_TYPE_NDB (from LegFacility.FixType)
                        var fixId = new FixId(icao, region, 2);
                        _navaidCache[fixId] = new NavaidCoord
                        {
                            Latitude = ndb.Latitude,
                            Longitude = ndb.Longitude,
                            Icao = icao,
                            Region = region,
                            Type = 2
                        };
                        CompleteNavaidRequest(icao, region, 2);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_navaidExceptionCount < 5)
                    Console.WriteLine($"[SimConnectFacility] ProcessNdb error: {ex.Message}");
                _navaidExceptionCount++;
            }
        }

        /// <summary>
        /// Complete a pending navaid request by ICAO/region/type
        /// </summary>
        private void CompleteNavaidRequest(string icao, string region, int type)
        {
            string key = $"{icao}_{region}_{type}";
            if (_pendingNavaidRequests.TryGetValue(key, out var tcs))
            {
                tcs.TrySetResult(true);
            }
        }

        private static int _extractLegDebugCount = 0;
        private LegFacility ExtractLeg(object data)
        {
            var leg = new LegFacility();

            if (data is LegFacility legData)
            {
                if (_extractLegDebugCount < 3)
                {
                    Console.WriteLine($"[ExtractLeg] Direct cast OK: FixIcao='{legData.FixIcao}'");
                    _extractLegDebugCount++;
                }
                return legData;
            }

            // Debug: What type did we get?
            if (_extractLegDebugCount < 3)
            {
                Console.WriteLine($"[ExtractLeg] Type={data?.GetType().Name}, Fields={string.Join(",", data?.GetType().GetFields().Select(f => f.Name) ?? Array.Empty<string>())}");
                _extractLegDebugCount++;
            }

            // Try reflection
            var type = data.GetType();
            leg.Type = ExtractInt(data, "Type");
            leg.FixIcao = ExtractString(data, "FixIcao");
            leg.FixRegion = ExtractString(data, "FixRegion");
            leg.FixType = ExtractInt(data, "FixType");
            leg.FlyOver = ExtractInt(data, "FlyOver");
            leg.TurnDirection = ExtractInt(data, "TurnDirection");
            leg.Course = ExtractFloat(data, "Course");
            leg.RouteDistance = ExtractFloat(data, "RouteDistance");
            leg.AltitudeDescription = ExtractInt(data, "AltitudeDescription");
            leg.Altitude1 = ExtractFloat(data, "Altitude1");
            leg.Altitude2 = ExtractFloat(data, "Altitude2");
            leg.SpeedLimit = ExtractFloat(data, "SpeedLimit");
            leg.VerticalAngle = ExtractFloat(data, "VerticalAngle");
            leg.ArcCenterFixIcao = ExtractString(data, "ArcCenterFixIcao");
            leg.ArcCenterFixRegion = ExtractString(data, "ArcCenterFixRegion");
            leg.ArcCenterFixType = ExtractInt(data, "ArcCenterFixType");
            leg.Radius = ExtractFloat(data, "Radius");

            if (_extractLegDebugCount < 5)
            {
                Console.WriteLine($"[ExtractLeg] After reflection: FixIcao='{leg.FixIcao}'");
            }

            return leg;
        }

        private string ExtractString(object data, string fieldName)
        {
            if (data == null) return null;
            var field = data.GetType().GetField(fieldName);
            if (field != null) return field.GetValue(data) as string;
            var prop = data.GetType().GetProperty(fieldName);
            if (prop != null) return prop.GetValue(data) as string;
            return null;
        }

        private int ExtractInt(object data, string fieldName)
        {
            if (data == null) return 0;
            var field = data.GetType().GetField(fieldName);
            if (field != null) return Convert.ToInt32(field.GetValue(data));
            var prop = data.GetType().GetProperty(fieldName);
            if (prop != null) return Convert.ToInt32(prop.GetValue(data));
            return 0;
        }

        private double ExtractDouble(object data, string fieldName)
        {
            if (data == null) return 0;
            var field = data.GetType().GetField(fieldName);
            if (field != null) return Convert.ToDouble(field.GetValue(data));
            var prop = data.GetType().GetProperty(fieldName);
            if (prop != null) return Convert.ToDouble(prop.GetValue(data));
            return 0;
        }

        private float ExtractFloat(object data, string fieldName)
        {
            if (data == null) return 0;
            var field = data.GetType().GetField(fieldName);
            if (field != null) return Convert.ToSingle(field.GetValue(data));
            var prop = data.GetType().GetProperty(fieldName);
            if (prop != null) return Convert.ToSingle(prop.GetValue(data));
            return 0;
        }

        /// <summary>
        /// Setup facility definitions - EXACTLY like atools addAirportProcedureFacilityDefinition()
        /// See simconnectloader.cpp lines 493-531
        /// </summary>
        private void SetupFacilityDefinitions()
        {
            try
            {
                // AIRPORT_NUM definition - like atools addAirportNumFacilityDefinition() line 400-405
                // This is used to quickly get counts of procedures for each airport
                AddAirportNumFacilityDefinition();

                // AIRPORT_PROC definition - EXACTLY like atools line 493-531
                Enum procDefId = (FacilityDataDefinitionId)FacilityDefId.AIRPORT_PROC;

                // AIRPORT container
                _simConnect.AddToFacilityDefinition(procDefId, "OPEN AIRPORT");
                _simConnect.AddToFacilityDefinition(procDefId, "ICAO");

                // APPROACH - atools line 501-518 (MUST come before DEPARTURE!)
                _simConnect.AddToFacilityDefinition(procDefId, "OPEN APPROACH");
                _simConnect.AddToFacilityDefinition(procDefId, "TYPE");
                _simConnect.AddToFacilityDefinition(procDefId, "SUFFIX");
                _simConnect.AddToFacilityDefinition(procDefId, "RUNWAY_NUMBER");
                _simConnect.AddToFacilityDefinition(procDefId, "RUNWAY_DESIGNATOR");
                _simConnect.AddToFacilityDefinition(procDefId, "FAF_ICAO");
                _simConnect.AddToFacilityDefinition(procDefId, "FAF_REGION");
                _simConnect.AddToFacilityDefinition(procDefId, "FAF_ALTITUDE");
                _simConnect.AddToFacilityDefinition(procDefId, "FAF_TYPE");
                _simConnect.AddToFacilityDefinition(procDefId, "MISSED_ALTITUDE");
                _simConnect.AddToFacilityDefinition(procDefId, "IS_RNPAR");
                // FINAL_APPROACH_LEG
                AddLegDefinitions(procDefId, "FINAL_APPROACH_LEG");
                // MISSED_APPROACH_LEG
                AddLegDefinitions(procDefId, "MISSED_APPROACH_LEG");
                // APPROACH_TRANSITION
                _simConnect.AddToFacilityDefinition(procDefId, "OPEN APPROACH_TRANSITION");
                _simConnect.AddToFacilityDefinition(procDefId, "TYPE");
                _simConnect.AddToFacilityDefinition(procDefId, "IAF_ICAO");
                _simConnect.AddToFacilityDefinition(procDefId, "IAF_REGION");
                _simConnect.AddToFacilityDefinition(procDefId, "IAF_TYPE");
                _simConnect.AddToFacilityDefinition(procDefId, "IAF_ALTITUDE");
                _simConnect.AddToFacilityDefinition(procDefId, "DME_ARC_ICAO");
                _simConnect.AddToFacilityDefinition(procDefId, "DME_ARC_REGION");
                _simConnect.AddToFacilityDefinition(procDefId, "DME_ARC_TYPE");
                _simConnect.AddToFacilityDefinition(procDefId, "DME_ARC_RADIAL");
                _simConnect.AddToFacilityDefinition(procDefId, "DME_ARC_DISTANCE");
                AddLegDefinitions(procDefId, "APPROACH_LEG");
                _simConnect.AddToFacilityDefinition(procDefId, "CLOSE APPROACH_TRANSITION");
                _simConnect.AddToFacilityDefinition(procDefId, "CLOSE APPROACH");

                // SID / DEPARTURE - atools line 520-523
                _simConnect.AddToFacilityDefinition(procDefId, "OPEN DEPARTURE");
                _simConnect.AddToFacilityDefinition(procDefId, "NAME");
                AddLegDefinitions(procDefId, "APPROACH_LEG");
                AddTransitionDefinitions(procDefId);
                _simConnect.AddToFacilityDefinition(procDefId, "CLOSE DEPARTURE");

                // STAR / ARRIVAL - atools line 525-528
                _simConnect.AddToFacilityDefinition(procDefId, "OPEN ARRIVAL");
                _simConnect.AddToFacilityDefinition(procDefId, "NAME");
                AddLegDefinitions(procDefId, "APPROACH_LEG");
                AddTransitionDefinitions(procDefId);
                _simConnect.AddToFacilityDefinition(procDefId, "CLOSE ARRIVAL");

                _simConnect.AddToFacilityDefinition(procDefId, "CLOSE AIRPORT");

                // WAYPOINT definition
                Enum wpDefId = (FacilityDataDefinitionId)FacilityDefId.WAYPOINT;
                _simConnect.AddToFacilityDefinition(wpDefId, "OPEN WAYPOINT");
                _simConnect.AddToFacilityDefinition(wpDefId, "LATITUDE");
                _simConnect.AddToFacilityDefinition(wpDefId, "LONGITUDE");
                _simConnect.AddToFacilityDefinition(wpDefId, "TYPE");
                _simConnect.AddToFacilityDefinition(wpDefId, "ICAO");
                _simConnect.AddToFacilityDefinition(wpDefId, "REGION");
                _simConnect.AddToFacilityDefinition(wpDefId, "CLOSE WAYPOINT");

                // VOR definition
                Enum vorDefId = (FacilityDataDefinitionId)FacilityDefId.VOR;
                _simConnect.AddToFacilityDefinition(vorDefId, "OPEN VOR");
                _simConnect.AddToFacilityDefinition(vorDefId, "VOR_LATITUDE");
                _simConnect.AddToFacilityDefinition(vorDefId, "VOR_LONGITUDE");
                _simConnect.AddToFacilityDefinition(vorDefId, "VOR_ALTITUDE");
                _simConnect.AddToFacilityDefinition(vorDefId, "FREQUENCY");
                _simConnect.AddToFacilityDefinition(vorDefId, "TYPE");
                _simConnect.AddToFacilityDefinition(vorDefId, "NAV_RANGE");
                _simConnect.AddToFacilityDefinition(vorDefId, "ICAO");
                _simConnect.AddToFacilityDefinition(vorDefId, "REGION");
                _simConnect.AddToFacilityDefinition(vorDefId, "NAME");
                _simConnect.AddToFacilityDefinition(vorDefId, "CLOSE VOR");

                // NDB definition
                Enum ndbDefId = (FacilityDataDefinitionId)FacilityDefId.NDB;
                _simConnect.AddToFacilityDefinition(ndbDefId, "OPEN NDB");
                _simConnect.AddToFacilityDefinition(ndbDefId, "LATITUDE");
                _simConnect.AddToFacilityDefinition(ndbDefId, "LONGITUDE");
                _simConnect.AddToFacilityDefinition(ndbDefId, "ALTITUDE");
                _simConnect.AddToFacilityDefinition(ndbDefId, "FREQUENCY");
                _simConnect.AddToFacilityDefinition(ndbDefId, "TYPE");
                _simConnect.AddToFacilityDefinition(ndbDefId, "RANGE");
                _simConnect.AddToFacilityDefinition(ndbDefId, "ICAO");
                _simConnect.AddToFacilityDefinition(ndbDefId, "REGION");
                _simConnect.AddToFacilityDefinition(ndbDefId, "NAME");
                _simConnect.AddToFacilityDefinition(ndbDefId, "CLOSE NDB");

                Console.WriteLine("[SimConnectFacility] Facility definitions setup - exactly like atools");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnectFacility] Error setting up definitions: {ex.Message}");
            }
        }

        /// <summary>
        /// Add leg definitions - exactly like atools addLegs() lines 343-356
        /// </summary>
        private void AddLegDefinitions(Enum defId, string legName)
        {
            _simConnect.AddToFacilityDefinition(defId, $"OPEN {legName}");
            _simConnect.AddToFacilityDefinition(defId, "TYPE");
            _simConnect.AddToFacilityDefinition(defId, "FIX_ICAO");
            _simConnect.AddToFacilityDefinition(defId, "FIX_REGION");
            _simConnect.AddToFacilityDefinition(defId, "FIX_TYPE");
            _simConnect.AddToFacilityDefinition(defId, "FLY_OVER");
            _simConnect.AddToFacilityDefinition(defId, "DISTANCE_MINUTE");
            _simConnect.AddToFacilityDefinition(defId, "TRUE_DEGREE");
            _simConnect.AddToFacilityDefinition(defId, "TURN_DIRECTION");
            _simConnect.AddToFacilityDefinition(defId, "ORIGIN_ICAO");
            _simConnect.AddToFacilityDefinition(defId, "ORIGIN_REGION");
            _simConnect.AddToFacilityDefinition(defId, "ORIGIN_TYPE");
            _simConnect.AddToFacilityDefinition(defId, "THETA");
            _simConnect.AddToFacilityDefinition(defId, "RHO");
            _simConnect.AddToFacilityDefinition(defId, "COURSE");
            _simConnect.AddToFacilityDefinition(defId, "ROUTE_DISTANCE");
            _simConnect.AddToFacilityDefinition(defId, "APPROACH_ALT_DESC");
            _simConnect.AddToFacilityDefinition(defId, "ALTITUDE1");
            _simConnect.AddToFacilityDefinition(defId, "ALTITUDE2");
            _simConnect.AddToFacilityDefinition(defId, "SPEED_LIMIT");
            _simConnect.AddToFacilityDefinition(defId, "VERTICAL_ANGLE");
            _simConnect.AddToFacilityDefinition(defId, "ARC_CENTER_FIX_ICAO");
            _simConnect.AddToFacilityDefinition(defId, "ARC_CENTER_FIX_REGION");
            _simConnect.AddToFacilityDefinition(defId, "ARC_CENTER_FIX_TYPE");
            _simConnect.AddToFacilityDefinition(defId, "RADIUS");
            _simConnect.AddToFacilityDefinition(defId, "IS_IAF");
            _simConnect.AddToFacilityDefinition(defId, "IS_IF");
            _simConnect.AddToFacilityDefinition(defId, "IS_FAF");
            _simConnect.AddToFacilityDefinition(defId, "IS_MAP");
            _simConnect.AddToFacilityDefinition(defId, "REQUIRED_NAVIGATION_PERFORMANCE");
            _simConnect.AddToFacilityDefinition(defId, $"CLOSE {legName}");
        }

        /// <summary>
        /// Add transition definitions - like atools addArrivalDepartureTrans() lines 335-341
        /// </summary>
        private void AddTransitionDefinitions(Enum defId)
        {
            // Runway transition
            _simConnect.AddToFacilityDefinition(defId, "OPEN RUNWAY_TRANSITION");
            _simConnect.AddToFacilityDefinition(defId, "RUNWAY_NUMBER");
            _simConnect.AddToFacilityDefinition(defId, "RUNWAY_DESIGNATOR");
            AddLegDefinitions(defId, "APPROACH_LEG");
            _simConnect.AddToFacilityDefinition(defId, "CLOSE RUNWAY_TRANSITION");

            // Enroute transition
            _simConnect.AddToFacilityDefinition(defId, "OPEN ENROUTE_TRANSITION");
            _simConnect.AddToFacilityDefinition(defId, "NAME");
            AddLegDefinitions(defId, "APPROACH_LEG");
            _simConnect.AddToFacilityDefinition(defId, "CLOSE ENROUTE_TRANSITION");
        }

        /// <summary>
        /// Add airport count definitions - like atools addAirportNumFacilityDefinition() line 400-405
        /// This is used to quickly determine which airports have procedures without loading full data
        /// </summary>
        private void AddAirportNumFacilityDefinition()
        {
            Enum numDefId = (FacilityDataDefinitionId)FacilityDefId.AIRPORT_NUM;
            _simConnect.AddToFacilityDefinition(numDefId, "OPEN AIRPORT");
            _simConnect.AddToFacilityDefinition(numDefId, "ICAO");
            _simConnect.AddToFacilityDefinition(numDefId, "N_RUNWAYS");
            _simConnect.AddToFacilityDefinition(numDefId, "N_STARTS");
            _simConnect.AddToFacilityDefinition(numDefId, "N_FREQUENCIES");
            _simConnect.AddToFacilityDefinition(numDefId, "N_HELIPADS");
            _simConnect.AddToFacilityDefinition(numDefId, "N_APPROACHES");
            _simConnect.AddToFacilityDefinition(numDefId, "N_DEPARTURES");
            _simConnect.AddToFacilityDefinition(numDefId, "N_ARRIVALS");
            _simConnect.AddToFacilityDefinition(numDefId, "CLOSE AIRPORT");

            // CRITICAL: Register the struct so data.Data[0] returns as AirportFacilityNumStruct!
            // Without this, SDK returns individual UInt32 values per callback instead of complete struct
            _simConnect.RegisterFacilityDataDefineStruct<AirportFacilityNumStruct>(SIMCONNECT_FACILITY_DATA_TYPE.AIRPORT);

            // EXAKT wie atools - Registriere alle Procedure-Structs!
            // Ohne diese Registrierung kommt der Name nicht richtig an
            _simConnect.RegisterFacilityDataDefineStruct<DepartureFacility>(SIMCONNECT_FACILITY_DATA_TYPE.DEPARTURE);
            _simConnect.RegisterFacilityDataDefineStruct<ArrivalFacility>(SIMCONNECT_FACILITY_DATA_TYPE.ARRIVAL);
            _simConnect.RegisterFacilityDataDefineStruct<RunwayTransitionFacility>(SIMCONNECT_FACILITY_DATA_TYPE.RUNWAY_TRANSITION);
            _simConnect.RegisterFacilityDataDefineStruct<EnrouteTransitionFacility>(SIMCONNECT_FACILITY_DATA_TYPE.ENROUTE_TRANSITION);

            // KRITISCH: LegFacility MUSS registriert werden damit FixIcao etc. gefüllt werden!
            // Ohne diese Zeile sind alle Leg-Felder LEER!
            _simConnect.RegisterFacilityDataDefineStruct<LegFacility>(SIMCONNECT_FACILITY_DATA_TYPE.APPROACH_LEG);

            // KRITISCH: Navaid Structs registrieren für Phase 5!
            // Ohne diese Registrierung werden WAYPOINT/VOR/NDB Responses nicht korrekt verarbeitet
            _simConnect.RegisterFacilityDataDefineStruct<WaypointFacility>(SIMCONNECT_FACILITY_DATA_TYPE.WAYPOINT);
            _simConnect.RegisterFacilityDataDefineStruct<VorFacility>(SIMCONNECT_FACILITY_DATA_TYPE.VOR);
            _simConnect.RegisterFacilityDataDefineStruct<NdbFacility>(SIMCONNECT_FACILITY_DATA_TYPE.NDB);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }

        #endregion
    }
}
