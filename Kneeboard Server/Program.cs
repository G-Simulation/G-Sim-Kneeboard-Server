using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Kneeboard_Server.Navigraph.BGL;

namespace Kneeboard_Server
{
    static class Program
    {
        private static Mutex m_Mutex;
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Test mode for BGL parsing
            if (args.Length >= 2 && args[0] == "--test-bgl")
            {
                RunBglTest(args[1]);
                return;
            }

            // Check database contents
            if (args.Length >= 1 && args[0] == "--check-db")
            {
                CheckDatabase();
                return;
            }

            // Test FixId matching
            if (args.Length >= 1 && args[0] == "--test-fixid")
            {
                TestFixIdMatching();
                return;
            }
            bool debug = false;
            if (debug == false)
            {
                int milliseconds = 1000;
                Thread.Sleep(milliseconds);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool createdNew;
                m_Mutex = new Mutex(true, "KneeboardServerMutex", out createdNew);
                if (createdNew)
                {
                    if (!IsAdministrator())
                    {
                        Console.WriteLine("Restarting as admin");
                        StartAsAdmin(Assembly.GetExecutingAssembly().Location);
                        return;
                    }
                    else
                    {
                        Application.Run(new Kneeboard_Server());
                    }
                }
                else
                {
                    // MessageBox.Show("The application is already running.", Application.ProductName,
                    // MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            else
            {
                int milliseconds = 1000;
                Thread.Sleep(milliseconds);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Kneeboard_Server());
            }
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void StartAsAdmin(string fileName)
        {
            var proc = new Process
            {
                StartInfo =
        {
            FileName = fileName,
            UseShellExecute = true,
            Verb = "runas"
        }
            };
            proc.Start();
        }

        /// <summary>
        /// Run BGL parser test for an airport
        /// Tests both MSFS 2020 and MSFS 2024 versions
        /// </summary>
        private static void RunBglTest(string airportIcao)
        {
            string outputPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                $"bgl_test_{airportIcao}.txt");

            using (var writer = new System.IO.StreamWriter(outputPath))
            {
                var originalOut = Console.Out;
                Console.SetOut(writer);

                try
                {
                    Console.WriteLine("=== BGL Parser Test ===");
                    Console.WriteLine($"Testing airport: {airportIcao}");
                    Console.WriteLine($"Time: {DateTime.Now}");
                    Console.WriteLine();

                    // Test all installed MSFS versions
                    var versions = MsfsNavdataService.DetectInstalledVersions();
                    Console.WriteLine($"Installed versions: {string.Join(", ", versions)}");
                    Console.WriteLine();

                    foreach (var version in versions)
                    {
                        Console.WriteLine($"\n========== Testing {version} ==========\n");
                        try
                        {
                            using (var service = new MsfsNavdataService(version))
                            {
                                if (service.IsAvailable)
                                {
                                    if (service.RequiresSimConnect)
                                    {
                                        Console.WriteLine($"[{version}] Requires SimConnect - testing Facility API...");

                                        // Test SimConnect Facility API for MSFS 2024
                                        TestSimConnectFacilityApi(airportIcao);
                                    }
                                    else
                                    {
                                        service.TestAirport(airportIcao);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[{version}] No navdata available at: {service.NavdataPath}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{version}] Error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }

                Console.SetOut(originalOut);
            }

            // Don't show message box in headless mode, just exit
            System.IO.File.WriteAllText(outputPath + ".done", "Complete");
        }

        /// <summary>
        /// Test SimConnect Facility API for MSFS 2024 SID/STAR procedures
        /// </summary>
        private static void TestSimConnectFacilityApi(string airportIcao)
        {
            Console.WriteLine("[SimConnect] Checking Facility API availability...");

            // Check if Facility API is available in SDK
            if (!SimConnectFacilityService.IsFacilityApiAvailable)
            {
                Console.WriteLine("[SimConnect] Facility API NOT available in current SDK");
                Console.WriteLine("[SimConnect] To use MSFS 2024 SID/STAR:");
                Console.WriteLine("[SimConnect]   1. Download MSFS 2024 SDK from https://docs.flightsimulator.com/");
                Console.WriteLine("[SimConnect]   2. Copy Microsoft.FlightSimulator.SimConnect.dll to project folder");
                Console.WriteLine("[SimConnect]   3. Rebuild the project");
                return;
            }

            Console.WriteLine("[SimConnect] Facility API is available!");
            Console.WriteLine("[SimConnect] Creating test window for message handling...");

            try
            {
                // Create a hidden window for SimConnect message handling
                using (var testForm = new System.Windows.Forms.Form())
                {
                    testForm.Text = "SimConnect Test";
                    testForm.ShowInTaskbar = false;
                    testForm.WindowState = System.Windows.Forms.FormWindowState.Minimized;

                    // Need to show briefly to get handle
                    testForm.Show();
                    testForm.Hide();

                    Console.WriteLine($"[SimConnect] Window handle: {testForm.Handle}");

                    using (var facilityService = new SimConnectFacilityService(testForm.Handle))
                    {
                        Console.WriteLine("[SimConnect] Attempting to connect to MSFS 2024...");

                        if (facilityService.Connect())
                        {
                            Console.WriteLine("[SimConnect] Connected! Waiting for connection to establish...");

                            // Wait for connection to be fully established
                            for (int i = 0; i < 30 && !facilityService.IsConnected; i++)
                            {
                                System.Windows.Forms.Application.DoEvents();
                                Thread.Sleep(100);
                            }

                            if (facilityService.IsConnected)
                            {
                                Console.WriteLine("[SimConnect] Connection established!");
                                Console.WriteLine($"[SimConnect] Requesting SID/STAR for {airportIcao}...");

                                // Request procedures
                                var sidsTask = facilityService.GetSIDsAsync(airportIcao);
                                var starsTask = facilityService.GetSTARsAsync(airportIcao);

                                // Process messages while waiting
                                var startTime = DateTime.Now;
                                while ((DateTime.Now - startTime).TotalSeconds < 15)
                                {
                                    System.Windows.Forms.Application.DoEvents();
                                    Thread.Sleep(50);

                                    if (sidsTask.IsCompleted && starsTask.IsCompleted)
                                        break;
                                }

                                // Get results
                                var sids = sidsTask.IsCompleted ? sidsTask.Result : new System.Collections.Generic.List<Navigraph.ProcedureSummary>();
                                var stars = starsTask.IsCompleted ? starsTask.Result : new System.Collections.Generic.List<Navigraph.ProcedureSummary>();

                                Console.WriteLine($"\n[SimConnect] Results for {airportIcao}:");
                                Console.WriteLine($"SIDs ({sids.Count}):");
                                foreach (var sid in sids)
                                {
                                    Console.WriteLine($"  - {sid.Identifier}");
                                }

                                Console.WriteLine($"STARs ({stars.Count}):");
                                foreach (var star in stars)
                                {
                                    Console.WriteLine($"  - {star.Identifier}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("[SimConnect] Connection not established within timeout");
                            }
                        }
                        else
                        {
                            Console.WriteLine("[SimConnect] Failed to connect - is MSFS 2024 running?");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnect] Error: {ex.Message}");
                Console.WriteLine($"[SimConnect] Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Check database contents - counts and sample data
        /// </summary>
        private static void CheckDatabase()
        {
            string dbPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "data", "msfs_navdata.sqlite");

            Console.WriteLine("=== Navdata Database Check ===");
            Console.WriteLine($"Database: {dbPath}");
            Console.WriteLine($"Exists: {System.IO.File.Exists(dbPath)}");
            Console.WriteLine();

            if (!System.IO.File.Exists(dbPath))
            {
                Console.WriteLine("ERROR: Database not found!");
                return;
            }

            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();

                    // List all tables first
                    Console.WriteLine("=== ALL TABLES IN DATABASE ===");
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine($"  {reader["name"]}");
                            }
                        }
                    }
                    Console.WriteLine();

                    // Count tables
                    Console.WriteLine("=== TABLE COUNTS ===");
                    string[] tables = { "airport", "runway", "sid", "star", "approach", "transition", "procedure_leg", "waypoint", "vor", "ndb" };
                    foreach (var table in tables)
                    {
                        try
                        {
                            using (var cmd = new System.Data.SQLite.SQLiteCommand($"SELECT COUNT(*) FROM {table}", conn))
                            {
                                var count = cmd.ExecuteScalar();
                                Console.WriteLine($"  {table}: {count}");
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"  {table}: (table not found)");
                        }
                    }

                    // Sample waypoints
                    Console.WriteLine("\n=== SAMPLE WAYPOINTS (first 10) ===");
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        "SELECT ident, region, latitude, longitude FROM waypoint LIMIT 10", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine($"  {reader["ident"],-7} {reader["region"],-4} lat={reader["latitude"],-12} lon={reader["longitude"]}");
                            }
                        }
                    }

                    // Sample VORs
                    Console.WriteLine("\n=== SAMPLE VORs (first 10) ===");
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        "SELECT ident, region, latitude, longitude, frequency FROM vor LIMIT 10", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine($"  {reader["ident"],-7} {reader["region"],-4} lat={reader["latitude"],-12} lon={reader["longitude"],-12} freq={reader["frequency"]}");
                            }
                        }
                    }

                    // Sample NDBs
                    Console.WriteLine("\n=== SAMPLE NDBs (first 10) ===");
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        "SELECT ident, region, latitude, longitude, frequency FROM ndb LIMIT 10", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine($"  {reader["ident"],-7} {reader["region"],-4} lat={reader["latitude"],-12} lon={reader["longitude"],-12} freq={reader["frequency"]}");
                            }
                        }
                    }

                    // Show approach_leg table structure
                    Console.WriteLine("\n=== APPROACH_LEG TABLE STRUCTURE ===");
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        "PRAGMA table_info(approach_leg)", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine($"  {reader["name"]} ({reader["type"]})");
                            }
                        }
                    }

                    // Check approach legs with fix references
                    Console.WriteLine("\n=== APPROACH LEGS WITH FIXES (first 20) ===");
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        @"SELECT al.fix_ident, al.fix_region, al.fix_type,
                                 w.latitude as wpt_lat, w.longitude as wpt_lon,
                                 v.latitude as vor_lat, v.longitude as vor_lon,
                                 n.latitude as ndb_lat, n.longitude as ndb_lon
                          FROM approach_leg al
                          LEFT JOIN waypoint w ON al.fix_ident = w.ident AND al.fix_region = w.region
                          LEFT JOIN vor v ON al.fix_ident = v.ident AND al.fix_region = v.region
                          LEFT JOIN ndb n ON al.fix_ident = n.ident AND al.fix_region = n.region
                          WHERE al.fix_ident IS NOT NULL AND al.fix_ident != ''
                          LIMIT 20", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string fixIdent = reader["fix_ident"]?.ToString() ?? "";
                                string fixRegion = reader["fix_region"]?.ToString() ?? "";
                                int fixType = 0;
                                if (reader["fix_type"] != DBNull.Value)
                                    int.TryParse(reader["fix_type"].ToString(), out fixType);

                                double? lat = null, lon = null;
                                string source = "NONE";

                                // Check waypoint first
                                if (reader["wpt_lat"] != DBNull.Value)
                                {
                                    lat = Convert.ToDouble(reader["wpt_lat"]);
                                    lon = Convert.ToDouble(reader["wpt_lon"]);
                                    source = "WPT";
                                }
                                else if (reader["vor_lat"] != DBNull.Value)
                                {
                                    lat = Convert.ToDouble(reader["vor_lat"]);
                                    lon = Convert.ToDouble(reader["vor_lon"]);
                                    source = "VOR";
                                }
                                else if (reader["ndb_lat"] != DBNull.Value)
                                {
                                    lat = Convert.ToDouble(reader["ndb_lat"]);
                                    lon = Convert.ToDouble(reader["ndb_lon"]);
                                    source = "NDB";
                                }

                                if (lat.HasValue)
                                {
                                    Console.WriteLine($"  fix={fixIdent,-7} [{source}] lat={lat:F6} lon={lon:F6}");
                                }
                                else
                                {
                                    Console.WriteLine($"  fix={fixIdent,-7} [NO NAVAID] type={fixType}");
                                }
                            }
                        }
                    }

                    // Count how many approach legs have resolvable coordinates
                    Console.WriteLine("\n=== COORDINATE RESOLUTION STATS ===");
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        @"SELECT
                            COUNT(*) as total_legs,
                            SUM(CASE WHEN fix_ident IS NOT NULL AND fix_ident != '' THEN 1 ELSE 0 END) as legs_with_fix,
                            (SELECT COUNT(*) FROM approach_leg al
                             WHERE EXISTS (SELECT 1 FROM waypoint w WHERE al.fix_ident = w.ident AND al.fix_region = w.region)) as matched_waypoint,
                            (SELECT COUNT(*) FROM approach_leg al
                             WHERE EXISTS (SELECT 1 FROM vor v WHERE al.fix_ident = v.ident AND al.fix_region = v.region)) as matched_vor,
                            (SELECT COUNT(*) FROM approach_leg al
                             WHERE EXISTS (SELECT 1 FROM ndb n WHERE al.fix_ident = n.ident AND al.fix_region = n.region)) as matched_ndb
                          FROM approach_leg", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Console.WriteLine($"  Total approach legs: {reader["total_legs"]}");
                                Console.WriteLine($"  Legs with fix_ident: {reader["legs_with_fix"]}");
                                Console.WriteLine($"  Matched to waypoint: {reader["matched_waypoint"]}");
                                Console.WriteLine($"  Matched to VOR: {reader["matched_vor"]}");
                                Console.WriteLine($"  Matched to NDB: {reader["matched_ndb"]}");
                            }
                        }
                    }

                    // Show sample raw approach legs
                    Console.WriteLine("\n=== SAMPLE APPROACH LEGS (first 10) ===");
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        "SELECT * FROM approach_leg LIMIT 10", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var cols = new System.Collections.Generic.List<string>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    cols.Add($"{reader.GetName(i)}={reader[i]}");
                                }
                                Console.WriteLine($"  {string.Join(", ", cols)}");
                            }
                        }
                    }

                    Console.WriteLine("\n=== DONE ===");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void TestFixIdMatching()
        {
            Console.WriteLine("=== TESTING FIXID MATCHING ===\n");

            // Test case: VOR from leg (Type=86='V') vs VOR from ProcessVor (Type=1)
            var cache = new System.Collections.Generic.Dictionary<SimConnectFacilityService.FixId, SimConnectFacilityService.NavaidCoord>();

            // Simulate what ProcessVor does: adds with Type=1
            var vorFromProcessVor = new SimConnectFacilityService.FixId("MUN", "ED", 1);
            cache[vorFromProcessVor] = new SimConnectFacilityService.NavaidCoord
            {
                Latitude = 48.353,
                Longitude = 11.786,
                Icao = "MUN",
                Region = "ED",
                Type = 1
            };
            Console.WriteLine($"Added to cache: FixId(MUN, ED, Type=1)");
            Console.WriteLine($"  Hash: {vorFromProcessVor.GetHashCode()}");

            // Simulate what happens during write: lookup with Type=86='V'
            var vorFromLeg = new SimConnectFacilityService.FixId("MUN", "ED", 86);  // 86 = 'V'
            Console.WriteLine($"\nLooking up: FixId(MUN, ED, Type=86='V')");
            Console.WriteLine($"  Hash: {vorFromLeg.GetHashCode()}");

            bool found = cache.TryGetValue(vorFromLeg, out var coord);
            Console.WriteLine($"\nResult: {(found ? "FOUND!" : "NOT FOUND")}");

            if (found)
            {
                Console.WriteLine($"  Lat={coord.Latitude}, Lon={coord.Longitude}");
            }

            // Also test Equals directly
            Console.WriteLine($"\nDirect Equals test:");
            Console.WriteLine($"  vorFromProcessVor.Equals(vorFromLeg) = {vorFromProcessVor.Equals(vorFromLeg)}");
            Console.WriteLine($"  vorFromLeg.Equals(vorFromProcessVor) = {vorFromLeg.Equals(vorFromProcessVor)}");

            // Test with 'V' char literal
            var vorWithCharV = new SimConnectFacilityService.FixId("MUN", "ED", 'V');
            Console.WriteLine($"\nTesting with char 'V' (={(int)'V'}):");
            Console.WriteLine($"  FixId(MUN, ED, Type='V') hash: {vorWithCharV.GetHashCode()}");
            Console.WriteLine($"  Equals with Type=1: {vorWithCharV.Equals(vorFromProcessVor)}");
            Console.WriteLine($"  Equals with Type=86: {vorWithCharV.Equals(vorFromLeg)}");

            // Test all VOR type values
            Console.WriteLine($"\n=== VOR Type Values ===");
            Console.WriteLine($"  'V' as int: {(int)'V'}");
            Console.WriteLine($"  86 == 'V': {86 == 'V'}");
            Console.WriteLine($"  1 (SimConnect enum for VOR)");

            Console.WriteLine("\n=== TEST COMPLETE ===");
        }
    }
}
