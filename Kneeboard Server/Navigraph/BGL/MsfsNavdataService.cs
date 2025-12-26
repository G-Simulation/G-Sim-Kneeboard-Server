using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kneeboard_Server.Navigraph.BGL
{
    /// <summary>
    /// MSFS Version enum - like atools FsPaths::SimulatorType
    /// </summary>
    public enum MsfsVersion
    {
        Unknown,
        MSFS2020,   // Microsoft.FlightSimulator_8wekyb3d8bbwe
        MSFS2024    // Microsoft.Limitless_8wekyb3d8bbwe
    }

    /// <summary>
    /// Service for accessing MSFS BGL navdata as source for SID/STAR procedures
    /// Hybrid solution like atools: BGL for MSFS 2020, SimConnect for MSFS 2024
    /// </summary>
    public class MsfsNavdataService : IDisposable
    {
        #region Constants (like atools fspaths.cpp)

        // MSFS 2020 package name
        private const string MSFS2020_PACKAGE = "Microsoft.FlightSimulator_8wekyb3d8bbwe";
        // MSFS 2024 package name
        private const string MSFS2024_PACKAGE = "Microsoft.Limitless_8wekyb3d8bbwe";

        #endregion

        #region Fields

        private readonly Dictionary<string, BglParser> _parsedFiles
            = new Dictionary<string, BglParser>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _airportToFile
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly object _cacheLock = new object();
        private bool _indexed;
        private bool _disposed;

        #endregion

        #region Static Methods (like atools fspaths.cpp)

        /// <summary>
        /// Detect installed MSFS versions by checking for UserCfg.opt
        /// Like atools FsPaths::detectAllMsfsPaths()
        /// </summary>
        public static List<MsfsVersion> DetectInstalledVersions()
        {
            var versions = new List<MsfsVersion>();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Check MSFS 2020 (MS Store)
            string msfs2020Store = Path.Combine(localAppData, "Packages", MSFS2020_PACKAGE, "LocalCache", "UserCfg.opt");
            if (File.Exists(msfs2020Store))
            {
                versions.Add(MsfsVersion.MSFS2020);
                Console.WriteLine($"[MsfsNavdata] Found MSFS 2020 (MS Store): {msfs2020Store}");
            }

            // Check MSFS 2020 (Steam)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string msfs2020Steam = Path.Combine(appData, "Microsoft Flight Simulator", "UserCfg.opt");
            if (File.Exists(msfs2020Steam) && !versions.Contains(MsfsVersion.MSFS2020))
            {
                versions.Add(MsfsVersion.MSFS2020);
                Console.WriteLine($"[MsfsNavdata] Found MSFS 2020 (Steam): {msfs2020Steam}");
            }

            // Check MSFS 2024 (MS Store)
            string msfs2024 = Path.Combine(localAppData, "Packages", MSFS2024_PACKAGE, "LocalCache", "UserCfg.opt");
            if (File.Exists(msfs2024))
            {
                versions.Add(MsfsVersion.MSFS2024);
                Console.WriteLine($"[MsfsNavdata] Found MSFS 2024 (MS Store): {msfs2024}");
            }

            // Check MSFS 2024 (Steam)
            string msfs2024Steam = Path.Combine(appData, "Microsoft Flight Simulator 2024", "UserCfg.opt");
            if (File.Exists(msfs2024Steam) && !versions.Contains(MsfsVersion.MSFS2024))
            {
                versions.Add(MsfsVersion.MSFS2024);
                Console.WriteLine($"[MsfsNavdata] Found MSFS 2024 (Steam): {msfs2024Steam}");
            }

            return versions;
        }

        /// <summary>
        /// Get the UserCfg.opt path for a specific MSFS version
        /// </summary>
        private static string GetUserCfgPath(MsfsVersion version)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            switch (version)
            {
                case MsfsVersion.MSFS2020:
                    // Try MS Store first
                    string msfs2020Store = Path.Combine(localAppData, "Packages", MSFS2020_PACKAGE, "LocalCache", "UserCfg.opt");
                    if (File.Exists(msfs2020Store)) return msfs2020Store;

                    // Try Steam
                    string msfs2020Steam = Path.Combine(appData, "Microsoft Flight Simulator", "UserCfg.opt");
                    if (File.Exists(msfs2020Steam)) return msfs2020Steam;
                    break;

                case MsfsVersion.MSFS2024:
                    // Try MS Store first
                    string msfs2024Store = Path.Combine(localAppData, "Packages", MSFS2024_PACKAGE, "LocalCache", "UserCfg.opt");
                    if (File.Exists(msfs2024Store)) return msfs2024Store;

                    // Try Steam
                    string msfs2024Steam = Path.Combine(appData, "Microsoft Flight Simulator 2024", "UserCfg.opt");
                    if (File.Exists(msfs2024Steam)) return msfs2024Steam;
                    break;
            }

            return null;
        }

        /// <summary>
        /// Read InstalledPackagesPath from UserCfg.opt
        /// Like atools FsPaths::msfsBasePath() - line 1064-1087
        /// </summary>
        private static string ReadInstalledPackagesPath(string userCfgPath)
        {
            if (string.IsNullOrEmpty(userCfgPath) || !File.Exists(userCfgPath))
                return null;

            try
            {
                foreach (var line in File.ReadLines(userCfgPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("InstalledPackagesPath"))
                    {
                        // Extract path after "InstalledPackagesPath "
                        int spaceIndex = trimmed.IndexOf(' ');
                        if (spaceIndex > 0)
                        {
                            string path = trimmed.Substring(spaceIndex + 1).Trim();
                            // Remove quotes
                            if (path.StartsWith("\"")) path = path.Substring(1);
                            if (path.EndsWith("\"")) path = path.Substring(0, path.Length - 1);

                            if (Directory.Exists(path))
                            {
                                Console.WriteLine($"[MsfsNavdata] Found InstalledPackagesPath: {path}");
                                return path;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MsfsNavdata] Error reading UserCfg.opt: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the Packages path for a specific MSFS version
        /// Like atools FsPaths::getBasePath() - reads from UserCfg.opt first!
        /// </summary>
        public static string GetPackagesPath(MsfsVersion version)
        {
            // FIRST: Read InstalledPackagesPath from UserCfg.opt (like atools!)
            string userCfgPath = GetUserCfgPath(version);
            string installedPath = ReadInstalledPackagesPath(userCfgPath);
            if (!string.IsNullOrEmpty(installedPath))
                return installedPath;

            // FALLBACK: Use default paths
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            switch (version)
            {
                case MsfsVersion.MSFS2020:
                    // Try MS Store first
                    string storePath = Path.Combine(localAppData, "Packages", MSFS2020_PACKAGE, "LocalCache", "Packages");
                    if (Directory.Exists(storePath))
                        return storePath;

                    // Try Steam
                    string steamPath = Path.Combine(appData, "Microsoft Flight Simulator", "Packages");
                    if (Directory.Exists(steamPath))
                        return steamPath;

                    return storePath; // Default to store path

                case MsfsVersion.MSFS2024:
                    // Default MSFS 2024 path (if InstalledPackagesPath not found)
                    return Path.Combine(localAppData, "Packages", MSFS2024_PACKAGE, "LocalCache", "Packages");

                default:
                    return null;
            }
        }

        /// <summary>
        /// Get Navigraph navdata path for a specific MSFS version
        /// Uses only navigraph-nav-base (standard Navigraph navdata)
        /// Searches common installation locations dynamically
        /// </summary>
        public static string GetNavigraphNavdataPath(MsfsVersion version)
        {
            // Search for navigraph-nav-base in common locations
            var searchPaths = new List<string>();

            // Add standard packages path
            string packagesPath = GetPackagesPath(version);
            if (!string.IsNullOrEmpty(packagesPath))
            {
                searchPaths.Add(Path.Combine(packagesPath, "Community"));
            }

            // Search on all available drives for MSFS/Packages/Community
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;

                // Common MSFS installation patterns
                var possiblePaths = new[]
                {
                    Path.Combine(drive.Name, "MSFS", "Packages", "Community"),
                    Path.Combine(drive.Name, "MSFS2024", "Packages", "Community"),
                    Path.Combine(drive.Name, "MicrosoftFlightSimulator", "Packages", "Community"),
                    Path.Combine(drive.Name, "Games", "MSFS", "Packages", "Community"),
                    Path.Combine(drive.Name, "Games", "MicrosoftFlightSimulator", "Packages", "Community"),
                };

                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path) && !searchPaths.Contains(path))
                    {
                        searchPaths.Add(path);
                    }
                }
            }

            // Search each path for navigraph-nav-base
            foreach (var communityPath in searchPaths)
            {
                string navBasePath = Path.Combine(communityPath, "navigraph-nav-base", "scenery", "fs-base", "scenery");
                if (Directory.Exists(navBasePath))
                {
                    // Verify it contains APX*.bgl files
                    try
                    {
                        var apxFiles = Directory.GetFiles(navBasePath, "APX*.bgl", SearchOption.AllDirectories);
                        if (apxFiles.Length > 0)
                        {
                            Console.WriteLine($"[MsfsNavdata] Found Navigraph at: {navBasePath} ({apxFiles.Length} APX files)");
                            return navBasePath;
                        }
                    }
                    catch { }
                }
            }

            Console.WriteLine("[MsfsNavdata] navigraph-nav-base not found in any location");
            return null;
        }

        /// <summary>
        /// Get official MSFS navdata path for a specific version
        /// APX*.bgl (airports with SID/STAR) are in fs-base-genericairports, not fs-base-nav!
        /// fs-base-nav only has ATX (airways), NAX (navaids), NVX files
        /// </summary>
        public static string GetOfficialNavdataPath(MsfsVersion version)
        {
            string packagesPath = GetPackagesPath(version);
            if (string.IsNullOrEmpty(packagesPath))
                return null;

            // MSFS 2020: Official/OneStore/fs-base-genericairports or Official/Steam/fs-base-genericairports
            // This is where APX*.bgl files with airport procedures (SID/STAR) are located
            if (version == MsfsVersion.MSFS2020)
            {
                string oneStorePath = Path.Combine(packagesPath, "Official", "OneStore", "fs-base-genericairports", "scenery");
                if (Directory.Exists(oneStorePath))
                    return oneStorePath;

                string steamPath = Path.Combine(packagesPath, "Official", "Steam", "fs-base-genericairports", "scenery");
                if (Directory.Exists(steamPath))
                    return steamPath;
            }

            return null;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Path to MSFS navdata folder
        /// </summary>
        public string NavdataPath { get; private set; }

        /// <summary>
        /// Detected MSFS version for this service instance
        /// </summary>
        public MsfsVersion Version { get; private set; }

        /// <summary>
        /// Whether navdata is available
        /// </summary>
        public bool IsAvailable => Directory.Exists(NavdataPath);

        /// <summary>
        /// Whether indexing has been completed
        /// </summary>
        public bool IsIndexed => _indexed;

        /// <summary>
        /// Number of indexed airports
        /// </summary>
        public int IndexedAirportCount => _airportToFile.Count;

        /// <summary>
        /// Get list of all indexed airport ICAOs from BGL files
        /// Used for BGL-Fallback when SimConnect reports 0 procedures
        /// </summary>
        public IEnumerable<string> GetIndexedAirportIcaos()
        {
            if (!_indexed) IndexNavdata();
            return _airportToFile.Keys.ToList();
        }

        /// <summary>
        /// Get BGL file path for a specific airport
        /// Returns null if airport is not in index
        /// </summary>
        public string GetBglPathForAirport(string icao)
        {
            if (!_indexed) IndexNavdata();
            return _airportToFile.TryGetValue(icao.ToUpper(), out var path) ? path : null;
        }

        /// <summary>
        /// Whether this version uses SimConnect for procedures (MSFS 2024)
        /// Like atools: MSFS 2024 uses SimConnect for airports, not BGL parsing.
        ///
        /// The .NET SimConnect SDK DOES support the Facility Data API (with newer SDK versions):
        /// - AddToFacilityDefinition() to define data structure
        /// - RegisterFacilityDataDefineStruct() to register struct types
        /// - RequestFacilityData() to request data
        /// - OnRecvFacilityData event to receive data
        ///
        /// MSFS 2024: Uses SimConnect Facility API for procedures
        /// MSFS 2020: Uses BGL file parsing
        /// </summary>
        public bool RequiresSimConnect => Version == MsfsVersion.MSFS2024;

        #endregion

        #region Constructor

        /// <summary>
        /// Create service for a specific MSFS version
        /// </summary>
        public MsfsNavdataService(MsfsVersion version)
        {
            Version = version;
            NavdataPath = DetectNavdataPathForVersion(version);
            Console.WriteLine($"[MsfsNavdata] Version: {version}");
            Console.WriteLine($"[MsfsNavdata] Path: {NavdataPath}");
            Console.WriteLine($"[MsfsNavdata] Available: {IsAvailable}");
            Console.WriteLine($"[MsfsNavdata] RequiresSimConnect: {RequiresSimConnect}");
        }

        /// <summary>
        /// Create service with auto-detection (prefers MSFS 2020 for BGL parsing)
        /// </summary>
        public MsfsNavdataService(string navdataPath = null)
        {
            if (!string.IsNullOrEmpty(navdataPath))
            {
                NavdataPath = navdataPath;
                Version = DetectVersionFromPath(navdataPath);
            }
            else
            {
                // Auto-detect: prefer MSFS 2020 because it supports BGL parsing
                var versions = DetectInstalledVersions();
                if (versions.Contains(MsfsVersion.MSFS2020))
                {
                    Version = MsfsVersion.MSFS2020;
                }
                else if (versions.Contains(MsfsVersion.MSFS2024))
                {
                    Version = MsfsVersion.MSFS2024;
                }
                else
                {
                    Version = MsfsVersion.Unknown;
                }

                NavdataPath = DetectNavdataPathForVersion(Version);
            }

            Console.WriteLine($"[MsfsNavdata] Version: {Version}");
            Console.WriteLine($"[MsfsNavdata] Path: {NavdataPath}");
            Console.WriteLine($"[MsfsNavdata] Available: {IsAvailable}");
            Console.WriteLine($"[MsfsNavdata] RequiresSimConnect: {RequiresSimConnect}");
        }

        /// <summary>
        /// Detect navdata path for a specific MSFS version
        /// Like atools: priority is Navigraph data, then official data
        /// </summary>
        private string DetectNavdataPathForVersion(MsfsVersion version)
        {
            // Try Navigraph first (higher priority like atools)
            string navigraphPath = GetNavigraphNavdataPath(version);
            if (!string.IsNullOrEmpty(navigraphPath) && Directory.Exists(navigraphPath))
            {
                Console.WriteLine($"[MsfsNavdata] Found Navigraph navdata: {navigraphPath}");
                return navigraphPath;
            }

            // Fall back to official navdata
            string officialPath = GetOfficialNavdataPath(version);
            if (!string.IsNullOrEmpty(officialPath) && Directory.Exists(officialPath))
            {
                Console.WriteLine($"[MsfsNavdata] Found official navdata: {officialPath}");
                return officialPath;
            }

            // Default fallback
            Console.WriteLine($"[MsfsNavdata] No navdata found for {version}");
            return navigraphPath ?? officialPath ?? "";
        }

        /// <summary>
        /// Try to detect MSFS version from a navdata path
        /// </summary>
        private MsfsVersion DetectVersionFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return MsfsVersion.Unknown;

            if (path.Contains(MSFS2024_PACKAGE) || path.Contains("Limitless"))
                return MsfsVersion.MSFS2024;

            if (path.Contains(MSFS2020_PACKAGE) || path.Contains("FlightSimulator"))
                return MsfsVersion.MSFS2020;

            // Check D:\MSFS path convention
            if (path.Contains("2024") || path.Contains("Official2024"))
                return MsfsVersion.MSFS2024;

            if (path.Contains("2020") || path.Contains("Official2020"))
                return MsfsVersion.MSFS2020;

            // Default to MSFS 2020 for unknown paths (supports BGL parsing)
            return MsfsVersion.MSFS2020;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Index all BGL files to build airport-to-file mapping
        /// </summary>
        public void IndexNavdata()
        {
            if (_indexed || !IsAvailable) return;

            lock (_cacheLock)
            {
                if (_indexed) return;

                Console.WriteLine($"[MsfsNavdata] Indexing navdata at: {NavdataPath}");

                try
                {
                    // Find all APX*.bgl files (Navigraph nav-base and MSFS default)
                    // APX files contain AIRPORT sections with SID/STAR sub-records
                    var bglFiles = Directory.GetFiles(NavdataPath, "APX*.bgl", SearchOption.AllDirectories);

                    Console.WriteLine($"[MsfsNavdata] Found {bglFiles.Length} airport BGL files");

                    int count = 0;
                    foreach (var file in bglFiles)
                    {
                        try
                        {
                            IndexBglFile(file);
                            count++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MsfsNavdata] Error indexing {file}: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"[MsfsNavdata] Indexed {_airportToFile.Count} airports from {count} files");
                    _indexed = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MsfsNavdata] Indexing error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get SIDs for an airport
        /// </summary>
        public List<ProcedureSummary> GetSIDs(string airportIcao)
        {
            var procedures = GetProceduresForAirport(airportIcao, true);
            return procedures.Select(p => p.ToProcedureSummary()).ToList();
        }

        /// <summary>
        /// Get STARs for an airport
        /// </summary>
        public List<ProcedureSummary> GetSTARs(string airportIcao)
        {
            var procedures = GetProceduresForAirport(airportIcao, false);
            return procedures.Select(p => p.ToProcedureSummary()).ToList();
        }

        /// <summary>
        /// Get detailed procedure with waypoints
        /// </summary>
        public ProcedureDetail GetProcedureDetail(string airportIcao, string procedureId,
            string transition = null, ProcedureType type = ProcedureType.SID)
        {
            bool isSid = (type == ProcedureType.SID);
            var procedures = GetProceduresForAirport(airportIcao, isSid);

            var proc = procedures.FirstOrDefault(p =>
                p.Identifier.Equals(procedureId, StringComparison.OrdinalIgnoreCase));

            if (proc == null) return null;

            return proc.ToProcedureDetail(transition);
        }

        /// <summary>
        /// Test parsing for a specific airport and print results
        /// </summary>
        public void TestAirport(string airportIcao)
        {
            Console.WriteLine($"\n=== Testing {airportIcao} ===");

            if (!_indexed) IndexNavdata();

            var sids = GetSIDs(airportIcao);
            Console.WriteLine($"\nSIDs ({sids.Count}):");
            foreach (var sid in sids)
            {
                Console.WriteLine($"  {sid.Identifier}: RWY={string.Join(",", sid.Runways)} TRANS={string.Join(",", sid.Transitions)}");
            }

            var stars = GetSTARs(airportIcao);
            Console.WriteLine($"\nSTARs ({stars.Count}):");
            foreach (var star in stars)
            {
                Console.WriteLine($"  {star.Identifier}: RWY={string.Join(",", star.Runways)} TRANS={string.Join(",", star.Transitions)}");
            }

            // Show detailed first SID
            if (sids.Count > 0)
            {
                var firstSid = sids.First();
                var detail = GetProcedureDetail(airportIcao, firstSid.Identifier,
                    firstSid.Runways.FirstOrDefault(), ProcedureType.SID);

                if (detail != null)
                {
                    Console.WriteLine($"\nDetail for SID {firstSid.Identifier}:");
                    Console.WriteLine($"  Waypoints: {detail.Waypoints.Count}");
                    foreach (var wp in detail.Waypoints.Take(10))
                    {
                        Console.WriteLine($"    {wp.Sequence}: {wp.Identifier} ({wp.PathTermination}) ALT={wp.Altitude1} CRS={wp.MagneticCourse:F0}");
                    }
                    if (detail.Waypoints.Count > 10)
                        Console.WriteLine($"    ... and {detail.Waypoints.Count - 10} more");
                }
            }

            Console.WriteLine($"\n=== End Test {airportIcao} ===\n");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Index a single BGL file
        /// </summary>
        private void IndexBglFile(string filePath)
        {
            using (var parser = new BglParser(filePath))
            {
                parser.Parse();

                foreach (var icao in parser.GetAirportIcaos())
                {
                    if (!_airportToFile.ContainsKey(icao))
                    {
                        _airportToFile[icao] = filePath;
                    }
                }
            }
        }

        /// <summary>
        /// Get procedures for an airport
        /// </summary>
        private List<BglSidStar> GetProceduresForAirport(string airportIcao, bool isSid)
        {
            if (!_indexed) IndexNavdata();

            string icao = airportIcao.ToUpper();

            lock (_cacheLock)
            {
                if (!_airportToFile.TryGetValue(icao, out string bglFile))
                {
                    Console.WriteLine($"[MsfsNavdata] Airport {icao} not found in index");
                    return new List<BglSidStar>();
                }

                // Get or create parser for this file
                if (!_parsedFiles.TryGetValue(bglFile, out BglParser parser))
                {
                    parser = new BglParser(bglFile);
                    parser.Parse();
                    _parsedFiles[bglFile] = parser;
                }

                return isSid ? parser.GetSIDs(icao) : parser.GetSTARs(icao);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_cacheLock)
                {
                    foreach (var parser in _parsedFiles.Values)
                    {
                        parser.Dispose();
                    }
                    _parsedFiles.Clear();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
