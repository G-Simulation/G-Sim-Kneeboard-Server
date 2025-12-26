using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace Kneeboard_Server.Navigraph.BGL
{
    /// <summary>
    /// SQLite database for navdata - EXACTLY like atools little_navmap_msfs24.sqlite
    /// Stores all airports, procedures, waypoints loaded via SimConnect Facility API
    /// </summary>
    public class NavdataDatabase : IDisposable
    {
        private SQLiteConnection _connection;
        private readonly string _dbPath;
        private bool _disposed;

        public bool IsInitialized { get; private set; }
        public int AirportCount { get; private set; }
        public int ProcedureCount { get; private set; }

        public NavdataDatabase(string dataFolder)
        {
            _dbPath = Path.Combine(dataFolder, "msfs_navdata.sqlite");
        }

        #region Database Setup - like atools create_ap_schema.sql

        public void Initialize()
        {
            try
            {
                bool dbExists = File.Exists(_dbPath);

                _connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
                _connection.Open();

                if (!dbExists)
                {
                    CreateSchema();
                    Console.WriteLine($"[NavdataDB] Created new database at {_dbPath}");
                }
                else
                {
                    // Check if tables exist
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='airport'", _connection))
                    {
                        var result = cmd.ExecuteScalar();
                        if (Convert.ToInt32(result) == 0)
                        {
                            CreateSchema();
                        }
                    }
                    Console.WriteLine($"[NavdataDB] Opened existing database at {_dbPath}");
                }

                UpdateCounts();
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavdataDB] Error initializing: {ex.Message}");
            }
        }

        private void CreateSchema()
        {
            // Schema EXACTLY like atools create_ap_schema.sql
            string schema = @"
                -- Airport table - like atools
                CREATE TABLE IF NOT EXISTS airport (
                    airport_id INTEGER PRIMARY KEY,
                    ident VARCHAR(10) NOT NULL,
                    region VARCHAR(4),
                    name VARCHAR(64),
                    latitude DOUBLE,
                    longitude DOUBLE,
                    altitude INTEGER,
                    num_approaches INTEGER DEFAULT 0,
                    num_departures INTEGER DEFAULT 0,
                    num_arrivals INTEGER DEFAULT 0,
                    loaded INTEGER DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_airport_ident ON airport(ident);

                -- Approach/SID/STAR table - like atools
                CREATE TABLE IF NOT EXISTS approach (
                    approach_id INTEGER PRIMARY KEY,
                    airport_id INTEGER NOT NULL,
                    type VARCHAR(25) NOT NULL,
                    suffix VARCHAR(1),
                    name VARCHAR(10),
                    runway_name VARCHAR(10),
                    FOREIGN KEY(airport_id) REFERENCES airport(airport_id)
                );

                CREATE INDEX IF NOT EXISTS idx_approach_airport ON approach(airport_id);
                CREATE INDEX IF NOT EXISTS idx_approach_name ON approach(name);

                -- Transition table - like atools
                CREATE TABLE IF NOT EXISTS transition (
                    transition_id INTEGER PRIMARY KEY,
                    approach_id INTEGER NOT NULL,
                    type VARCHAR(25) NOT NULL,
                    name VARCHAR(10),
                    runway_number INTEGER,
                    runway_designator INTEGER,
                    FOREIGN KEY(approach_id) REFERENCES approach(approach_id)
                );

                CREATE INDEX IF NOT EXISTS idx_transition_approach ON transition(approach_id);

                -- Approach/Transition leg table - like atools
                CREATE TABLE IF NOT EXISTS approach_leg (
                    leg_id INTEGER PRIMARY KEY,
                    approach_id INTEGER,
                    transition_id INTEGER,
                    sequence INTEGER NOT NULL,
                    leg_type INTEGER,
                    fix_ident VARCHAR(8),
                    fix_region VARCHAR(4),
                    fix_type INTEGER,
                    fly_over INTEGER,
                    turn_direction INTEGER,
                    course DOUBLE,
                    distance DOUBLE,
                    altitude1 DOUBLE,
                    altitude2 DOUBLE,
                    alt_descriptor INTEGER,
                    speed_limit INTEGER,
                    vertical_angle DOUBLE,
                    arc_center_ident VARCHAR(8),
                    arc_center_region VARCHAR(4),
                    arc_center_type INTEGER,
                    radius DOUBLE,
                    theta DOUBLE,
                    rho DOUBLE,
                    is_iaf INTEGER,
                    is_if INTEGER,
                    is_faf INTEGER,
                    is_map INTEGER,
                    rnp DOUBLE,
                    FOREIGN KEY(approach_id) REFERENCES approach(approach_id),
                    FOREIGN KEY(transition_id) REFERENCES transition(transition_id)
                );

                CREATE INDEX IF NOT EXISTS idx_leg_approach ON approach_leg(approach_id);
                CREATE INDEX IF NOT EXISTS idx_leg_transition ON approach_leg(transition_id);

                -- Waypoint table - like atools
                CREATE TABLE IF NOT EXISTS waypoint (
                    waypoint_id INTEGER PRIMARY KEY,
                    ident VARCHAR(8) NOT NULL,
                    region VARCHAR(4),
                    type INTEGER,
                    latitude DOUBLE NOT NULL,
                    longitude DOUBLE NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_waypoint_ident ON waypoint(ident);
                CREATE INDEX IF NOT EXISTS idx_waypoint_ident_region ON waypoint(ident, region);

                -- VOR table - like atools
                CREATE TABLE IF NOT EXISTS vor (
                    vor_id INTEGER PRIMARY KEY,
                    ident VARCHAR(8) NOT NULL,
                    region VARCHAR(4),
                    name VARCHAR(64),
                    type INTEGER,
                    frequency DOUBLE,
                    range DOUBLE,
                    latitude DOUBLE NOT NULL,
                    longitude DOUBLE NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_vor_ident ON vor(ident);

                -- NDB table - like atools
                CREATE TABLE IF NOT EXISTS ndb (
                    ndb_id INTEGER PRIMARY KEY,
                    ident VARCHAR(8) NOT NULL,
                    region VARCHAR(4),
                    name VARCHAR(64),
                    type INTEGER,
                    frequency INTEGER,
                    range DOUBLE,
                    latitude DOUBLE NOT NULL,
                    longitude DOUBLE NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_ndb_ident ON ndb(ident);

                -- Metadata table
                CREATE TABLE IF NOT EXISTS metadata (
                    key VARCHAR(50) PRIMARY KEY,
                    value TEXT
                );
            ";

            using (var cmd = new SQLiteCommand(schema, _connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Insert metadata
            using (var cmd = new SQLiteCommand("INSERT OR REPLACE INTO metadata (key, value) VALUES ('version', '1.0'), ('created', datetime('now'))", _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateCounts()
        {
            try
            {
                using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM airport", _connection))
                {
                    AirportCount = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM approach", _connection))
                {
                    ProcedureCount = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch { }
        }

        /// <summary>
        /// Refresh counts from database after bulk loading
        /// </summary>
        public void RefreshCounts()
        {
            UpdateCounts();
        }

        #endregion

        #region Airport Operations

        public void InsertAirport(string ident, string region, string name, double lat, double lon, int altitude,
            int numApproaches, int numDepartures, int numArrivals)
        {
            const string sql = @"INSERT OR REPLACE INTO airport
                (ident, region, name, latitude, longitude, altitude, num_approaches, num_departures, num_arrivals, loaded)
                VALUES (@ident, @region, @name, @lat, @lon, @alt, @numApp, @numDep, @numArr, 0)";

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@ident", ident);
                cmd.Parameters.AddWithValue("@region", region ?? "");
                cmd.Parameters.AddWithValue("@name", name ?? "");
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lon", lon);
                cmd.Parameters.AddWithValue("@alt", altitude);
                cmd.Parameters.AddWithValue("@numApp", numApproaches);
                cmd.Parameters.AddWithValue("@numDep", numDepartures);
                cmd.Parameters.AddWithValue("@numArr", numArrivals);
                cmd.ExecuteNonQuery();
            }
        }

        public long GetAirportId(string ident)
        {
            using (var cmd = new SQLiteCommand("SELECT airport_id FROM airport WHERE ident = @ident", _connection))
            {
                cmd.Parameters.AddWithValue("@ident", ident);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt64(result) : -1;
            }
        }

        public void MarkAirportLoaded(string ident)
        {
            using (var cmd = new SQLiteCommand("UPDATE airport SET loaded = 1 WHERE ident = @ident", _connection))
            {
                cmd.Parameters.AddWithValue("@ident", ident);
                cmd.ExecuteNonQuery();
            }
        }

        public bool IsAirportLoaded(string ident)
        {
            using (var cmd = new SQLiteCommand("SELECT loaded FROM airport WHERE ident = @ident", _connection))
            {
                cmd.Parameters.AddWithValue("@ident", ident);
                var result = cmd.ExecuteScalar();
                return result != null && Convert.ToInt32(result) == 1;
            }
        }

        #endregion

        #region Procedure Operations

        public long InsertProcedure(long airportId, string type, string suffix, string name, string runwayName)
        {
            const string sql = @"INSERT INTO approach (airport_id, type, suffix, name, runway_name)
                VALUES (@airportId, @type, @suffix, @name, @runway)";

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@airportId", airportId);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@suffix", suffix ?? "");
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@runway", runwayName ?? "");
                cmd.ExecuteNonQuery();
            }

            return _connection.LastInsertRowId;
        }

        public long InsertTransition(long approachId, string type, string name, int runwayNumber, int runwayDesignator)
        {
            const string sql = @"INSERT INTO transition (approach_id, type, name, runway_number, runway_designator)
                VALUES (@approachId, @type, @name, @rwNum, @rwDes)";

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@approachId", approachId);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@name", name ?? "");
                cmd.Parameters.AddWithValue("@rwNum", runwayNumber);
                cmd.Parameters.AddWithValue("@rwDes", runwayDesignator);
                cmd.ExecuteNonQuery();
            }

            return _connection.LastInsertRowId;
        }

        public void InsertLeg(long? approachId, long? transitionId, int sequence,
            int legType, string fixIdent, string fixRegion, int fixType,
            int flyOver, int turnDirection, double course, double distance,
            double altitude1, double altitude2, int altDescriptor,
            int speedLimit, double verticalAngle,
            string arcCenterIdent, string arcCenterRegion, int arcCenterType, double radius,
            double theta, double rho, int isIaf, int isIf, int isFaf, int isMap, double rnp)
        {
            const string sql = @"INSERT INTO approach_leg
                (approach_id, transition_id, sequence, leg_type, fix_ident, fix_region, fix_type,
                 fly_over, turn_direction, course, distance, altitude1, altitude2, alt_descriptor,
                 speed_limit, vertical_angle, arc_center_ident, arc_center_region, arc_center_type, radius,
                 theta, rho, is_iaf, is_if, is_faf, is_map, rnp)
                VALUES (@appId, @transId, @seq, @legType, @fixIdent, @fixRegion, @fixType,
                 @flyOver, @turn, @course, @dist, @alt1, @alt2, @altDesc,
                 @speed, @vAngle, @arcIdent, @arcRegion, @arcType, @radius,
                 @theta, @rho, @isIaf, @isIf, @isFaf, @isMap, @rnp)";

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@appId", approachId.HasValue ? (object)approachId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@transId", transitionId.HasValue ? (object)transitionId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@seq", sequence);
                cmd.Parameters.AddWithValue("@legType", legType);
                cmd.Parameters.AddWithValue("@fixIdent", fixIdent ?? "");
                cmd.Parameters.AddWithValue("@fixRegion", fixRegion ?? "");
                cmd.Parameters.AddWithValue("@fixType", fixType);
                cmd.Parameters.AddWithValue("@flyOver", flyOver);
                cmd.Parameters.AddWithValue("@turn", turnDirection);
                cmd.Parameters.AddWithValue("@course", course);
                cmd.Parameters.AddWithValue("@dist", distance);
                cmd.Parameters.AddWithValue("@alt1", altitude1);
                cmd.Parameters.AddWithValue("@alt2", altitude2);
                cmd.Parameters.AddWithValue("@altDesc", altDescriptor);
                cmd.Parameters.AddWithValue("@speed", speedLimit);
                cmd.Parameters.AddWithValue("@vAngle", verticalAngle);
                cmd.Parameters.AddWithValue("@arcIdent", arcCenterIdent ?? "");
                cmd.Parameters.AddWithValue("@arcRegion", arcCenterRegion ?? "");
                cmd.Parameters.AddWithValue("@arcType", arcCenterType);
                cmd.Parameters.AddWithValue("@radius", radius);
                cmd.Parameters.AddWithValue("@theta", theta);
                cmd.Parameters.AddWithValue("@rho", rho);
                cmd.Parameters.AddWithValue("@isIaf", isIaf);
                cmd.Parameters.AddWithValue("@isIf", isIf);
                cmd.Parameters.AddWithValue("@isFaf", isFaf);
                cmd.Parameters.AddWithValue("@isMap", isMap);
                cmd.Parameters.AddWithValue("@rnp", rnp);
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Navaid Operations

        public void InsertWaypoint(string ident, string region, int type, double lat, double lon)
        {
            const string sql = @"INSERT OR IGNORE INTO waypoint (ident, region, type, latitude, longitude)
                VALUES (@ident, @region, @type, @lat, @lon)";

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@ident", ident);
                cmd.Parameters.AddWithValue("@region", region ?? "");
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lon", lon);
                cmd.ExecuteNonQuery();
            }
        }

        public void InsertVor(string ident, string region, string name, int type, double frequency, double range, double lat, double lon)
        {
            const string sql = @"INSERT OR IGNORE INTO vor (ident, region, name, type, frequency, range, latitude, longitude)
                VALUES (@ident, @region, @name, @type, @freq, @range, @lat, @lon)";

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@ident", ident);
                cmd.Parameters.AddWithValue("@region", region ?? "");
                cmd.Parameters.AddWithValue("@name", name ?? "");
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@freq", frequency);
                cmd.Parameters.AddWithValue("@range", range);
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lon", lon);
                cmd.ExecuteNonQuery();
            }
        }

        public void InsertNdb(string ident, string region, string name, int type, int frequency, double range, double lat, double lon)
        {
            const string sql = @"INSERT OR IGNORE INTO ndb (ident, region, name, type, frequency, range, latitude, longitude)
                VALUES (@ident, @region, @name, @type, @freq, @range, @lat, @lon)";

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@ident", ident);
                cmd.Parameters.AddWithValue("@region", region ?? "");
                cmd.Parameters.AddWithValue("@name", name ?? "");
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@freq", frequency);
                cmd.Parameters.AddWithValue("@range", range);
                cmd.Parameters.AddWithValue("@lat", lat);
                cmd.Parameters.AddWithValue("@lon", lon);
                cmd.ExecuteNonQuery();
            }
        }

        public (double lat, double lon)? GetNavaidCoords(string ident, string region, int type)
        {
            // Try waypoint table first (most common for SID/STAR fixes)
            string[] tables = { "waypoint", "vor", "ndb" };

            foreach (var table in tables)
            {
                string sql = $"SELECT latitude, longitude FROM {table} WHERE ident = @ident";
                if (!string.IsNullOrEmpty(region))
                    sql += " AND region = @region";

                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@ident", ident);
                    if (!string.IsNullOrEmpty(region))
                        cmd.Parameters.AddWithValue("@region", region);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return (reader.GetDouble(0), reader.GetDouble(1));
                        }
                    }
                }
            }

            Console.WriteLine($"[NavdataDB] WARNING: No coords found for {ident} (region={region}, type={type})");
            return null;
        }

        #endregion

        #region Query Operations

        public List<ProcedureSummary> GetSIDs(string airportIdent)
        {
            var result = new List<ProcedureSummary>();
            const string sql = @"SELECT a.approach_id, a.name, a.runway_name
                FROM approach a
                JOIN airport ap ON a.airport_id = ap.airport_id
                WHERE ap.ident = @ident AND a.type = 'SID'";

            try
            {
                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@ident", airportIdent);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new ProcedureSummary
                            {
                                Identifier = reader.GetString(1),
                                Type = ProcedureType.SID,
                                Airport = airportIdent
                            });
                        }
                    }
                }
                Console.WriteLine($"[NavdataDB] GetSIDs({airportIdent}): Found {result.Count} SIDs");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavdataDB] GetSIDs error: {ex.Message}");
            }
            return result;
        }

        public List<ProcedureSummary> GetSTARs(string airportIdent)
        {
            var result = new List<ProcedureSummary>();
            const string sql = @"SELECT a.approach_id, a.name, a.runway_name
                FROM approach a
                JOIN airport ap ON a.airport_id = ap.airport_id
                WHERE ap.ident = @ident AND a.type = 'STAR'";

            try
            {
                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@ident", airportIdent);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new ProcedureSummary
                            {
                                Identifier = reader.GetString(1),
                                Type = ProcedureType.STAR,
                                Airport = airportIdent
                            });
                        }
                    }
                }
                Console.WriteLine($"[NavdataDB] GetSTARs({airportIdent}): Found {result.Count} STARs");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavdataDB] GetSTARs error: {ex.Message}");
            }
            return result;
        }

        public List<ProcedureWaypoint> GetProcedureLegs(string airportIdent, string procedureName, ProcedureType type)
        {
            var result = new List<ProcedureWaypoint>();
            string typeStr = type == ProcedureType.SID ? "SID" : "STAR";

            // UNION Query: Common Legs (approach_id) + Transition Legs (transition_id)
            // Common Legs haben approach_id gesetzt, Transition Legs haben nur transition_id
            const string sql = @"
                SELECT l.sequence, l.leg_type, l.fix_ident, l.fix_region, l.fix_type,
                    l.fly_over, l.turn_direction, l.course, l.distance, l.altitude1, l.altitude2,
                    l.alt_descriptor, l.speed_limit, l.vertical_angle, l.arc_center_ident, l.radius
                FROM approach_leg l
                JOIN approach a ON l.approach_id = a.approach_id
                JOIN airport ap ON a.airport_id = ap.airport_id
                WHERE ap.ident = @ident AND a.name = @name AND a.type = @type

                UNION ALL

                SELECT l.sequence, l.leg_type, l.fix_ident, l.fix_region, l.fix_type,
                    l.fly_over, l.turn_direction, l.course, l.distance, l.altitude1, l.altitude2,
                    l.alt_descriptor, l.speed_limit, l.vertical_angle, l.arc_center_ident, l.radius
                FROM approach_leg l
                JOIN transition t ON l.transition_id = t.transition_id
                JOIN approach a ON t.approach_id = a.approach_id
                JOIN airport ap ON a.airport_id = ap.airport_id
                WHERE ap.ident = @ident AND a.name = @name AND a.type = @type

                ORDER BY 1";

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@ident", airportIdent);
                cmd.Parameters.AddWithValue("@name", procedureName);
                cmd.Parameters.AddWithValue("@type", typeStr);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var wp = new ProcedureWaypoint
                        {
                            Sequence = reader.GetInt32(0),
                            Identifier = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            IsFlyOver = reader.GetInt32(5) == 1,
                            TurnDirection = reader.GetInt32(6) == 1 ? "L" : (reader.GetInt32(6) == 2 ? "R" : null),
                            MagneticCourse = reader.IsDBNull(7) ? null : (double?)reader.GetDouble(7),
                            RouteDistance = reader.IsDBNull(8) ? null : (double?)reader.GetDouble(8),
                            Altitude1 = reader.IsDBNull(9) ? null : (int?)reader.GetDouble(9),
                            Altitude2 = reader.IsDBNull(10) ? null : (int?)reader.GetDouble(10),
                            SpeedLimit = reader.IsDBNull(12) ? null : (int?)reader.GetInt32(12),
                            VerticalAngle = reader.IsDBNull(13) ? null : (double?)reader.GetDouble(13),
                            CenterWaypoint = reader.IsDBNull(14) ? null : reader.GetString(14),
                            ArcRadius = reader.IsDBNull(15) ? null : (double?)reader.GetDouble(15)
                        };

                        // Get coordinates from navaid tables
                        string fixIdent = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        string fixRegion = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        int fixType = reader.IsDBNull(4) ? 1 : reader.GetInt32(4);

                        if (!string.IsNullOrEmpty(fixIdent))
                        {
                            var coords = GetNavaidCoords(fixIdent, fixRegion, fixType);
                            if (coords.HasValue)
                            {
                                wp.Latitude = coords.Value.lat;
                                wp.Longitude = coords.Value.lon;
                            }
                        }

                        result.Add(wp);
                    }
                }
            }

            Console.WriteLine($"[NavdataDB] GetProcedureLegs({airportIdent}, {procedureName}, {typeStr}): Found {result.Count} legs");
            return result;
        }

        #endregion

        #region Transaction Support

        public void BeginTransaction()
        {
            using (var cmd = new SQLiteCommand("BEGIN TRANSACTION", _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void CommitTransaction()
        {
            using (var cmd = new SQLiteCommand("COMMIT", _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void RollbackTransaction()
        {
            using (var cmd = new SQLiteCommand("ROLLBACK", _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Maintenance

        public void ClearAll()
        {
            string[] tables = { "approach_leg", "transition", "approach", "waypoint", "vor", "ndb", "airport" };
            foreach (var table in tables)
            {
                using (var cmd = new SQLiteCommand($"DELETE FROM {table}", _connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            UpdateCounts();
        }

        public void Vacuum()
        {
            using (var cmd = new SQLiteCommand("VACUUM", _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Close();
                _connection?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}