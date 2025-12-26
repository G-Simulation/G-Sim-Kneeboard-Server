using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Kneeboard_Server.Navigraph
{
    /// <summary>
    /// Service for downloading and managing Navigraph navdata packages
    /// </summary>
    public class NavigraphDataService : IDisposable
    {
        #region Constants

        private const string PACKAGES_ENDPOINT = "https://api.navigraph.com/v1/navdata/packages";

        #endregion

        #region Fields

        private readonly NavigraphAuthService _authService;
        private readonly NavigraphDbCache _dbCache;
        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>
        /// Whether navdata is available for queries
        /// </summary>
        public bool IsDataAvailable => _dbCache?.IsOpen ?? false;

        /// <summary>
        /// Current AIRAC cycle (e.g., "2501")
        /// </summary>
        public string CurrentAiracCycle { get; private set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime? LastUpdate { get; private set; }

        /// <summary>
        /// Path to the current database file
        /// </summary>
        public string DatabasePath { get; private set; }

        #endregion

        #region Constructor

        public NavigraphDataService(NavigraphAuthService authService)
        {
            _authService = authService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "KneeboardServer/2.0");

            // Cache directory in app folder
            _cacheDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "cache",
                "navigraph"
            );

            Directory.CreateDirectory(_cacheDirectory);

            _dbCache = new NavigraphDbCache();

            // Try to open existing database
            LoadExistingDatabase();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Check for updates and download new package if available
        /// </summary>
        public async Task<bool> CheckAndDownloadUpdatesAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                Console.WriteLine("[Navigraph] Not authenticated, cannot check for updates");
                return false;
            }

            try
            {
                // Ensure token is valid
                if (!await _authService.EnsureValidTokenAsync())
                {
                    Console.WriteLine("[Navigraph] Failed to refresh token");
                    return false;
                }

                // Fetch available packages
                var package = await GetCurrentPackageAsync();
                if (package == null)
                {
                    Console.WriteLine("[Navigraph] No package available");
                    return false;
                }

                string packageCycle = package["cycle"]?.ToString();
                string packageRevision = package["revision"]?.ToString();

                Console.WriteLine($"[Navigraph] Available package: AIRAC {packageCycle} Rev {packageRevision}");

                // Check if we need to update
                string localMetaPath = Path.Combine(_cacheDirectory, "metadata.json");
                if (File.Exists(localMetaPath))
                {
                    string localMeta = File.ReadAllText(localMetaPath);
                    var localJson = JObject.Parse(localMeta);

                    if (localJson["cycle"]?.ToString() == packageCycle &&
                        localJson["revision"]?.ToString() == packageRevision)
                    {
                        Console.WriteLine("[Navigraph] Already up to date");

                        // Make sure database is open
                        if (!IsDataAvailable)
                        {
                            OpenDatabase(localJson["database_path"]?.ToString());
                        }

                        return true;
                    }
                }

                // Download the package
                var files = package["files"] as JArray;
                if (files == null || files.Count == 0)
                {
                    Console.WriteLine("[Navigraph] No files in package");
                    return false;
                }

                // Find the DFD SQLite database file
                JObject dbFile = null;
                foreach (JObject file in files)
                {
                    string key = file["key"]?.ToString() ?? "";
                    if (key.EndsWith(".s3db") || key.EndsWith(".sqlite") || key.EndsWith(".db"))
                    {
                        dbFile = file;
                        break;
                    }
                }

                if (dbFile == null)
                {
                    // Take first file if no explicit DB found
                    dbFile = files[0] as JObject;
                }

                string signedUrl = dbFile["signed_url"]?.ToString();
                string expectedHash = dbFile["hash"]?.ToString();
                string fileName = dbFile["key"]?.ToString() ?? $"dfd_{packageCycle}.db";

                if (string.IsNullOrEmpty(signedUrl))
                {
                    Console.WriteLine("[Navigraph] No download URL in package");
                    return false;
                }

                Console.WriteLine($"[Navigraph] Downloading: {fileName}");

                // Download the file
                string localDbPath = Path.Combine(_cacheDirectory, fileName);
                bool downloadSuccess = await DownloadFileAsync(signedUrl, localDbPath, expectedHash);

                if (!downloadSuccess)
                {
                    Console.WriteLine("[Navigraph] Download failed");
                    return false;
                }

                // Save metadata
                var metadata = new JObject
                {
                    ["cycle"] = packageCycle,
                    ["revision"] = packageRevision,
                    ["database_path"] = localDbPath,
                    ["downloaded_at"] = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(localMetaPath, metadata.ToString());

                // Open the new database
                OpenDatabase(localDbPath);
                CurrentAiracCycle = packageCycle;
                LastUpdate = DateTime.UtcNow;

                Console.WriteLine($"[Navigraph] Database updated: AIRAC {packageCycle}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Update check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get list of SIDs for an airport
        /// </summary>
        public System.Collections.Generic.List<ProcedureSummary> GetSIDs(string airportIcao)
        {
            if (!IsDataAvailable)
            {
                return new System.Collections.Generic.List<ProcedureSummary>();
            }
            return _dbCache.GetSIDsForAirport(airportIcao);
        }

        /// <summary>
        /// Get list of STARs for an airport
        /// </summary>
        public System.Collections.Generic.List<ProcedureSummary> GetSTARs(string airportIcao)
        {
            if (!IsDataAvailable)
            {
                return new System.Collections.Generic.List<ProcedureSummary>();
            }
            return _dbCache.GetSTARsForAirport(airportIcao);
        }

        /// <summary>
        /// Get list of approaches for an airport
        /// </summary>
        public System.Collections.Generic.List<ApproachSummary> GetApproaches(string airportIcao)
        {
            if (!IsDataAvailable)
            {
                return new System.Collections.Generic.List<ApproachSummary>();
            }
            return _dbCache.GetApproachesForAirport(airportIcao);
        }

        /// <summary>
        /// Get detailed procedure with waypoints
        /// </summary>
        public ProcedureDetail GetProcedureDetail(string airportIcao, string procedureId, string transition = null, ProcedureType type = ProcedureType.SID)
        {
            if (!IsDataAvailable)
            {
                return null;
            }

            var waypoints = _dbCache.GetProcedureWaypoints(airportIcao, procedureId, transition, type);

            var detail = new ProcedureDetail
            {
                Summary = new ProcedureSummary
                {
                    Identifier = procedureId,
                    Airport = airportIcao,
                    Type = type
                },
                Transition = transition,
                Waypoints = waypoints,
                DataSource = "Navigraph",
                AiracCycle = CurrentAiracCycle
            };

            return detail;
        }

        /// <summary>
        /// Get ILS data for an airport
        /// </summary>
        public System.Collections.Generic.List<ILSData> GetILSData(string airportIcao)
        {
            if (!IsDataAvailable)
            {
                return new System.Collections.Generic.List<ILSData>();
            }
            return _dbCache.GetILSForAirport(airportIcao);
        }

        /// <summary>
        /// Get runway data for an airport
        /// </summary>
        public System.Collections.Generic.List<RunwayData> GetRunways(string airportIcao)
        {
            if (!IsDataAvailable)
            {
                return new System.Collections.Generic.List<RunwayData>();
            }
            return _dbCache.GetRunwaysForAirport(airportIcao);
        }

        /// <summary>
        /// Get status information
        /// </summary>
        public NavigraphStatus GetStatus()
        {
            var authStatus = _authService.GetStatus();
            authStatus.AiracCycle = CurrentAiracCycle;
            authStatus.LastUpdate = LastUpdate;
            authStatus.DatabasePath = DatabasePath;
            return authStatus;
        }

        #endregion

        #region Private Methods

        private async Task<JObject> GetCurrentPackageAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{PACKAGES_ENDPOINT}?format=DFD&package_status=current");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Navigraph] Packages request failed: {response.StatusCode} - {content}");
                    return null;
                }

                var packages = JArray.Parse(content);
                if (packages.Count > 0)
                {
                    return packages[0] as JObject;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Failed to fetch packages: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> DownloadFileAsync(string url, string localPath, string expectedHash)
        {
            try
            {
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    // Download to temp file first
                    string tempPath = localPath + ".tmp";
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var httpStream = await response.Content.ReadAsStreamAsync())
                    {
                        await httpStream.CopyToAsync(fileStream);
                    }

                    // Verify hash if provided
                    if (!string.IsNullOrEmpty(expectedHash))
                    {
                        string actualHash = ComputeFileHash(tempPath);
                        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[Navigraph] Hash mismatch! Expected: {expectedHash}, Got: {actualHash}");
                            File.Delete(tempPath);
                            return false;
                        }
                    }

                    // Close existing database if same path
                    if (DatabasePath == localPath)
                    {
                        _dbCache.Close();
                    }

                    // Move temp to final location
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }
                    File.Move(tempPath, localPath);

                    Console.WriteLine($"[Navigraph] Downloaded successfully: {Path.GetFileName(localPath)}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Download error: {ex.Message}");
                return false;
            }
        }

        private string ComputeFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void LoadExistingDatabase()
        {
            try
            {
                string metaPath = Path.Combine(_cacheDirectory, "metadata.json");
                if (File.Exists(metaPath))
                {
                    string content = File.ReadAllText(metaPath);
                    var json = JObject.Parse(content);

                    string dbPath = json["database_path"]?.ToString();
                    if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                    {
                        OpenDatabase(dbPath);
                        CurrentAiracCycle = json["cycle"]?.ToString();

                        string downloadedAt = json["downloaded_at"]?.ToString();
                        if (!string.IsNullOrEmpty(downloadedAt) && DateTime.TryParse(downloadedAt, out DateTime dt))
                        {
                            LastUpdate = dt;
                        }

                        Console.WriteLine($"[Navigraph] Loaded existing database: AIRAC {CurrentAiracCycle}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Failed to load existing database: {ex.Message}");
            }
        }

        private void OpenDatabase(string path)
        {
            try
            {
                _dbCache.Open(path);
                DatabasePath = path;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Failed to open database: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _dbCache?.Dispose();
                _httpClient?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
