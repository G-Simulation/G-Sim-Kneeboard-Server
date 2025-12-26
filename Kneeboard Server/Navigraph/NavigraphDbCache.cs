using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Kneeboard_Server.Navigraph
{
    /// <summary>
    /// SQLite database cache for Navigraph DFD navdata
    /// </summary>
    public class NavigraphDbCache : IDisposable
    {
        #region Fields

        private SQLiteConnection _connection;
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the database is open and ready for queries
        /// </summary>
        public bool IsOpen => _connection?.State == System.Data.ConnectionState.Open;

        #endregion

        #region Connection Management

        /// <summary>
        /// Open a SQLite database file
        /// </summary>
        public void Open(string databasePath)
        {
            Close();

            string connectionString = $"Data Source={databasePath};Version=3;Read Only=True;";
            _connection = new SQLiteConnection(connectionString);
            _connection.Open();

            Console.WriteLine($"[NavigraphDB] Opened: {databasePath}");
        }

        /// <summary>
        /// Close the database connection
        /// </summary>
        public void Close()
        {
            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }

        #endregion

        #region SID Queries

        /// <summary>
        /// Get list of SIDs for an airport
        /// </summary>
        public List<ProcedureSummary> GetSIDsForAirport(string airportIcao)
        {
            var result = new List<ProcedureSummary>();
            if (!IsOpen) return result;

            try
            {
                // Query for distinct SID procedures with their transitions
                string sql = @"
                    SELECT DISTINCT procedure_identifier, transition_identifier
                    FROM tbl_sids
                    WHERE airport_identifier = @icao
                    ORDER BY procedure_identifier, transition_identifier";

                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@icao", airportIcao.ToUpper());

                    var procedures = new Dictionary<string, ProcedureSummary>();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string procId = reader["procedure_identifier"]?.ToString();
                            string transId = reader["transition_identifier"]?.ToString();

                            if (string.IsNullOrEmpty(procId)) continue;

                            if (!procedures.ContainsKey(procId))
                            {
                                procedures[procId] = new ProcedureSummary
                                {
                                    Identifier = procId,
                                    Airport = airportIcao.ToUpper(),
                                    Type = ProcedureType.SID,
                                    Transitions = new List<string>(),
                                    Runways = new List<string>()
                                };
                            }

                            if (!string.IsNullOrEmpty(transId) && transId != "ALL" &&
                                !procedures[procId].Transitions.Contains(transId))
                            {
                                procedures[procId].Transitions.Add(transId);
                            }
                        }
                    }

                    // Get runways for each SID
                    foreach (var proc in procedures.Values)
                    {
                        proc.Runways = GetProcedureRunways(airportIcao, proc.Identifier, "tbl_sids");
                        result.Add(proc);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigraphDB] SID query error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region STAR Queries

        /// <summary>
        /// Get list of STARs for an airport
        /// </summary>
        public List<ProcedureSummary> GetSTARsForAirport(string airportIcao)
        {
            var result = new List<ProcedureSummary>();
            if (!IsOpen) return result;

            try
            {
                string sql = @"
                    SELECT DISTINCT procedure_identifier, transition_identifier
                    FROM tbl_stars
                    WHERE airport_identifier = @icao
                    ORDER BY procedure_identifier, transition_identifier";

                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@icao", airportIcao.ToUpper());

                    var procedures = new Dictionary<string, ProcedureSummary>();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string procId = reader["procedure_identifier"]?.ToString();
                            string transId = reader["transition_identifier"]?.ToString();

                            if (string.IsNullOrEmpty(procId)) continue;

                            if (!procedures.ContainsKey(procId))
                            {
                                procedures[procId] = new ProcedureSummary
                                {
                                    Identifier = procId,
                                    Airport = airportIcao.ToUpper(),
                                    Type = ProcedureType.STAR,
                                    Transitions = new List<string>(),
                                    Runways = new List<string>()
                                };
                            }

                            if (!string.IsNullOrEmpty(transId) && transId != "ALL" &&
                                !procedures[procId].Transitions.Contains(transId))
                            {
                                procedures[procId].Transitions.Add(transId);
                            }
                        }
                    }

                    foreach (var proc in procedures.Values)
                    {
                        proc.Runways = GetProcedureRunways(airportIcao, proc.Identifier, "tbl_stars");
                        result.Add(proc);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigraphDB] STAR query error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Approach Queries

        /// <summary>
        /// Get list of approaches for an airport
        /// </summary>
        public List<ApproachSummary> GetApproachesForAirport(string airportIcao)
        {
            var result = new List<ApproachSummary>();
            if (!IsOpen) return result;

            try
            {
                string sql = @"
                    SELECT DISTINCT procedure_identifier, transition_identifier
                    FROM tbl_iaps
                    WHERE airport_identifier = @icao
                    ORDER BY procedure_identifier, transition_identifier";

                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@icao", airportIcao.ToUpper());

                    var procedures = new Dictionary<string, ApproachSummary>();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string procId = reader["procedure_identifier"]?.ToString();
                            string transId = reader["transition_identifier"]?.ToString();

                            if (string.IsNullOrEmpty(procId)) continue;

                            if (!procedures.ContainsKey(procId))
                            {
                                var approach = new ApproachSummary
                                {
                                    Identifier = procId,
                                    Airport = airportIcao.ToUpper(),
                                    Type = ProcedureType.Approach,
                                    Transitions = new List<string>(),
                                    Runways = new List<string>()
                                };

                                // Determine approach type from identifier
                                approach.ApproachType = DetermineApproachType(procId);
                                approach.Runway = ExtractRunwayFromApproach(procId);

                                procedures[procId] = approach;
                            }

                            if (!string.IsNullOrEmpty(transId) && transId != "ALL" &&
                                !procedures[procId].Transitions.Contains(transId))
                            {
                                procedures[procId].Transitions.Add(transId);
                            }
                        }
                    }

                    result.AddRange(procedures.Values);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigraphDB] Approach query error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Waypoint Queries

        /// <summary>
        /// Get waypoints for a procedure
        /// </summary>
        public List<ProcedureWaypoint> GetProcedureWaypoints(string airportIcao, string procedureId,
            string transition = null, ProcedureType type = ProcedureType.SID)
        {
            var result = new List<ProcedureWaypoint>();
            if (!IsOpen) return result;

            string tableName = type == ProcedureType.SID ? "tbl_sids" :
                              type == ProcedureType.STAR ? "tbl_stars" : "tbl_iaps";

            try
            {
                string sql = $@"
                    SELECT seqno, waypoint_identifier, waypoint_latitude, waypoint_longitude,
                           waypoint_description_code, turn_direction, rnp, path_termination,
                           recommanded_navaid, recommanded_navaid_latitude, recommanded_navaid_longitude,
                           arc_radius, theta, rho, magnetic_course,
                           route_distance_holding_distance_time, distance_time,
                           altitude_description, altitude1, altitude2,
                           speed_limit_description, speed_limit,
                           vertical_angle, center_waypoint,
                           center_waypoint_latitude, center_waypoint_longitude
                    FROM {tableName}
                    WHERE airport_identifier = @icao
                      AND procedure_identifier = @procId
                      AND (transition_identifier = @trans OR transition_identifier IS NULL
                           OR transition_identifier = '' OR @trans IS NULL)
                    ORDER BY seqno";

                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@icao", airportIcao.ToUpper());
                    cmd.Parameters.AddWithValue("@procId", procedureId);
                    cmd.Parameters.AddWithValue("@trans", string.IsNullOrEmpty(transition) ? (object)DBNull.Value : transition);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var waypoint = new ProcedureWaypoint
                            {
                                Sequence = GetInt(reader, "seqno"),
                                Identifier = reader["waypoint_identifier"]?.ToString(),
                                Latitude = GetDouble(reader, "waypoint_latitude"),
                                Longitude = GetDouble(reader, "waypoint_longitude"),
                                PathTermination = reader["path_termination"]?.ToString(),
                                TurnDirection = reader["turn_direction"]?.ToString(),
                                Rnp = GetNullableDouble(reader, "rnp"),
                                MagneticCourse = GetNullableDouble(reader, "magnetic_course"),
                                RouteDistance = GetNullableDouble(reader, "route_distance_holding_distance_time"),
                                AltitudeDescription = reader["altitude_description"]?.ToString(),
                                Altitude1 = GetNullableInt(reader, "altitude1"),
                                Altitude2 = GetNullableInt(reader, "altitude2"),
                                SpeedLimitDescription = reader["speed_limit_description"]?.ToString(),
                                SpeedLimit = GetNullableInt(reader, "speed_limit"),
                                VerticalAngle = GetNullableDouble(reader, "vertical_angle"),
                                ArcRadius = GetNullableDouble(reader, "arc_radius"),
                                CenterWaypoint = reader["center_waypoint"]?.ToString(),
                                CenterLatitude = GetNullableDouble(reader, "center_waypoint_latitude"),
                                CenterLongitude = GetNullableDouble(reader, "center_waypoint_longitude"),
                                RecommendedNavaid = reader["recommanded_navaid"]?.ToString(),
                                RecommendedNavaidLatitude = GetNullableDouble(reader, "recommanded_navaid_latitude"),
                                RecommendedNavaidLongitude = GetNullableDouble(reader, "recommanded_navaid_longitude")
                            };

                            // Determine fly-over from waypoint description code
                            string descCode = reader["waypoint_description_code"]?.ToString();
                            waypoint.IsFlyOver = descCode?.Contains("Y") ?? false;

                            result.Add(waypoint);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigraphDB] Waypoint query error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region ILS Queries

        /// <summary>
        /// Get ILS/Localizer data for an airport
        /// </summary>
        public List<ILSData> GetILSForAirport(string airportIcao)
        {
            var result = new List<ILSData>();
            if (!IsOpen) return result;

            try
            {
                string sql = @"
                    SELECT llz_identifier, llz_frequency, llz_latitude, llz_longitude,
                           llz_bearing, gs_angle, gs_threshold_crossing_height,
                           ils_mls_gls_category, runway_identifier
                    FROM tbl_localizers_glideslopes
                    WHERE airport_identifier = @icao";

                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@icao", airportIcao.ToUpper());

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var ils = new ILSData
                            {
                                Airport = airportIcao.ToUpper(),
                                Identifier = reader["llz_identifier"]?.ToString(),
                                Frequency = GetDouble(reader, "llz_frequency"),
                                Latitude = GetDouble(reader, "llz_latitude"),
                                Longitude = GetDouble(reader, "llz_longitude"),
                                LocalizerBearing = GetDouble(reader, "llz_bearing"),
                                GlideSlopeAngle = GetNullableDouble(reader, "gs_angle"),
                                ThresholdCrossingHeight = GetNullableDouble(reader, "gs_threshold_crossing_height"),
                                Category = reader["ils_mls_gls_category"]?.ToString(),
                                Runway = reader["runway_identifier"]?.ToString()
                            };

                            result.Add(ils);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigraphDB] ILS query error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Runway Queries

        /// <summary>
        /// Get runway data for an airport
        /// </summary>
        public List<RunwayData> GetRunwaysForAirport(string airportIcao)
        {
            var result = new List<RunwayData>();
            if (!IsOpen) return result;

            try
            {
                string sql = @"
                    SELECT runway_identifier, runway_latitude, runway_longitude,
                           runway_magnetic_bearing, runway_length, runway_width,
                           landing_threshold_elevation, threshold_crossing_height,
                           llz_identifier, llz_frequency
                    FROM tbl_runways
                    WHERE airport_identifier = @icao";

                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@icao", airportIcao.ToUpper());

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var runway = new RunwayData
                            {
                                Airport = airportIcao.ToUpper(),
                                RunwayIdentifier = reader["runway_identifier"]?.ToString(),
                                Latitude = GetDouble(reader, "runway_latitude"),
                                Longitude = GetDouble(reader, "runway_longitude"),
                                Heading = GetDouble(reader, "runway_magnetic_bearing"),
                                Length = GetInt(reader, "runway_length"),
                                Width = GetInt(reader, "runway_width"),
                                Elevation = GetNullableDouble(reader, "landing_threshold_elevation"),
                                ThresholdCrossingHeight = GetNullableDouble(reader, "threshold_crossing_height"),
                                ILSIdentifier = reader["llz_identifier"]?.ToString(),
                                ILSFrequency = GetNullableDouble(reader, "llz_frequency")
                            };

                            result.Add(runway);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigraphDB] Runway query error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private List<string> GetProcedureRunways(string airportIcao, string procedureId, string tableName)
        {
            var runways = new List<string>();

            try
            {
                // Extract runway from transition identifiers that look like runway names
                string sql = $@"
                    SELECT DISTINCT transition_identifier
                    FROM {tableName}
                    WHERE airport_identifier = @icao
                      AND procedure_identifier = @procId
                      AND transition_identifier LIKE 'RW%'";

                using (var cmd = new SQLiteCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@icao", airportIcao.ToUpper());
                    cmd.Parameters.AddWithValue("@procId", procedureId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string rwy = reader[0]?.ToString();
                            if (!string.IsNullOrEmpty(rwy))
                            {
                                // Convert RW25L to 25L
                                runways.Add(rwy.Replace("RW", ""));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavigraphDB] Runway extraction error: {ex.Message}");
            }

            return runways;
        }

        private ApproachType DetermineApproachType(string procedureId)
        {
            if (string.IsNullOrEmpty(procedureId)) return ApproachType.Unknown;

            string upper = procedureId.ToUpper();

            if (upper.StartsWith("I")) return ApproachType.ILS;
            if (upper.StartsWith("L")) return ApproachType.LOC;
            if (upper.StartsWith("R")) return ApproachType.RNAV;
            if (upper.StartsWith("H")) return ApproachType.RNP;
            if (upper.StartsWith("V")) return ApproachType.VOR;
            if (upper.StartsWith("D")) return ApproachType.VORDME;
            if (upper.StartsWith("N")) return ApproachType.NDB;
            if (upper.StartsWith("G")) return ApproachType.GPS;
            if (upper.StartsWith("X")) return ApproachType.LDA;
            if (upper.StartsWith("U")) return ApproachType.SDF;

            return ApproachType.Unknown;
        }

        private string ExtractRunwayFromApproach(string procedureId)
        {
            if (string.IsNullOrEmpty(procedureId) || procedureId.Length < 2)
                return null;

            // Approach identifiers usually have format like "I25L" or "R13"
            // Skip the first character (approach type) and extract the runway
            string remaining = procedureId.Substring(1);

            // Find numeric portion
            int start = -1;
            int end = remaining.Length;

            for (int i = 0; i < remaining.Length; i++)
            {
                if (char.IsDigit(remaining[i]))
                {
                    if (start == -1) start = i;
                }
                else if (start >= 0)
                {
                    // Check if it's a runway designator (L, R, C)
                    if (remaining[i] == 'L' || remaining[i] == 'R' || remaining[i] == 'C')
                    {
                        end = i + 1;
                    }
                    else
                    {
                        end = i;
                    }
                    break;
                }
            }

            if (start >= 0)
            {
                return remaining.Substring(start, end - start);
            }

            return null;
        }

        private int GetInt(SQLiteDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        private int? GetNullableInt(SQLiteDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? (int?)null : reader.GetInt32(ordinal);
        }

        private double GetDouble(SQLiteDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0.0 : reader.GetDouble(ordinal);
        }

        private double? GetNullableDouble(SQLiteDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? (double?)null : reader.GetDouble(ordinal);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }
        }

        #endregion
    }
}
