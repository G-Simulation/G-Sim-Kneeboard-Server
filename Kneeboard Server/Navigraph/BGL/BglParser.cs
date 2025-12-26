using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kneeboard_Server.Navigraph.BGL
{
    /// <summary>
    /// BGL file parser for extracting SID/STAR procedures from MSFS navigation data
    /// Ported from Little Navmap atools/src/fs/bgl/bglfile.cpp
    /// </summary>
    public class BglParser : IDisposable
    {
        #region Constants

        // BGL Header constants (from atools header.h/cpp)
        private const int HEADER_SIZE = 0x38; // 56 bytes
        private const uint BGL_MAGIC_1 = 0x19920201;
        private const uint BGL_MAGIC_2 = 0x08051803;

        // Header offsets
        private const int OFFSET_HEADER_SIZE = 0x04;
        private const int OFFSET_MAGIC_2 = 0x10;
        private const int OFFSET_NUM_SECTIONS = 0x14; // 20 decimal - numSections

        // Section types
        private const uint SECTION_AIRPORT = 0x03;

        #endregion

        #region Fields

        private readonly string _filePath;
        private FileStream _stream;
        private BinaryReader _reader;
        private bool _parsed;
        private bool _disposed;

        private readonly List<BglSidStar> _procedures = new List<BglSidStar>();
        private readonly Dictionary<string, List<BglSidStar>> _proceduresByAirport
            = new Dictionary<string, List<BglSidStar>>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        /// <summary>
        /// Path to the BGL file
        /// </summary>
        public string FilePath => _filePath;

        /// <summary>
        /// All parsed procedures
        /// </summary>
        public IReadOnlyList<BglSidStar> Procedures => _procedures;

        /// <summary>
        /// Whether parsing has been completed
        /// </summary>
        public bool IsParsed => _parsed;

        #endregion

        #region Constructor

        public BglParser(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("BGL file not found", filePath);

            _filePath = filePath;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Parse the BGL file and extract all SID/STAR procedures
        /// </summary>
        public void Parse()
        {
            if (_parsed) return;

            try
            {
                _stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _reader = new BinaryReader(_stream, Encoding.UTF8);

                // Check minimum file size
                if (_stream.Length < HEADER_SIZE)
                {
                    Console.WriteLine($"[BglParser] File too small: {_filePath}");
                    return;
                }

                // Read and validate header
                if (!ReadHeader())
                {
                    return;
                }

                // Read sections and subsections
                ReadSectionsAndSubsections();

                _parsed = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BglParser] Error parsing {_filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all SIDs for an airport
        /// </summary>
        public List<BglSidStar> GetSIDs(string airportIcao)
        {
            if (!_parsed) Parse();

            if (_proceduresByAirport.TryGetValue(airportIcao.ToUpper(), out var procs))
            {
                return procs.FindAll(p => p.IsSid);
            }
            return new List<BglSidStar>();
        }

        /// <summary>
        /// Get all STARs for an airport
        /// </summary>
        public List<BglSidStar> GetSTARs(string airportIcao)
        {
            if (!_parsed) Parse();

            if (_proceduresByAirport.TryGetValue(airportIcao.ToUpper(), out var procs))
            {
                return procs.FindAll(p => !p.IsSid);
            }
            return new List<BglSidStar>();
        }

        /// <summary>
        /// Get all airports with procedures in this file
        /// </summary>
        public IEnumerable<string> GetAirportIcaos()
        {
            if (!_parsed) Parse();
            return _proceduresByAirport.Keys;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Read and validate BGL header
        /// Based on atools header.cpp Header constructor
        /// </summary>
        private bool ReadHeader()
        {
            _stream.Seek(0, SeekOrigin.Begin);

            // Read header structure (see atools header.cpp)
            uint magic1 = _reader.ReadUInt32();         // offset 0x00
            uint headerSize = _reader.ReadUInt32();     // offset 0x04
            uint lowDateTime = _reader.ReadUInt32();    // offset 0x08
            uint highDateTime = _reader.ReadUInt32();   // offset 0x0C
            uint magic2 = _reader.ReadUInt32();         // offset 0x10
            uint numSections = _reader.ReadUInt32();    // offset 0x14

            // Validate magic numbers (MSFS may have different values)
            if (magic1 != BGL_MAGIC_1)
            {
                // MSFS uses different magic numbers - skip validation
            }

            // Validate header size (MSFS may have different sizes)
            if (headerSize < HEADER_SIZE && headerSize != 0)
            {
                // Some files have different header sizes - skip validation
            }

            // Validate section count
            if (numSections == 0 || numSections > 100)
            {
                Console.WriteLine($"[BglParser] Invalid section count: {numSections}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Read sections and find airport records
        /// </summary>
        private void ReadSectionsAndSubsections()
        {
            // Position at section count (offset 0x14)
            _stream.Seek(OFFSET_NUM_SECTIONS, SeekOrigin.Begin);
            uint numSections = _reader.ReadUInt32();

            // Skip QMIDs (8 * 4 bytes) to first section pointer (offset 0x38)
            _stream.Seek(HEADER_SIZE, SeekOrigin.Begin);

            // Read section pointers (see atools section.cpp)
            // Each section is 20 bytes:
            //   type (4), sizeFlag (4), numSubsections (4), firstSubsectionOffset (4), totalSubsectionSize (4)
            var sections = new List<(uint type, uint subsectionOffset, uint numSubsections)>();

            for (int i = 0; i < numSections; i++)
            {
                uint sectionType = _reader.ReadUInt32();          // type
                uint sizeFlag = _reader.ReadUInt32();              // sizeFlag - for subsection size calc
                uint numSubsections = _reader.ReadUInt32();        // numSubsections
                uint firstSubsectionOffset = _reader.ReadUInt32(); // firstSubsectionOffset
                uint totalSubsectionSize = _reader.ReadUInt32();   // totalSubsectionSize

                sections.Add((sectionType, firstSubsectionOffset, numSubsections));
            }

            // Process AIRPORT sections
            foreach (var section in sections)
            {
                if (section.type == SECTION_AIRPORT)
                {
                    ReadAirportSubsections(section.subsectionOffset, section.numSubsections);
                }
            }
        }

        /// <summary>
        /// Read airport subsections
        /// Based on atools subsection.cpp - 16 bytes per subsection
        /// </summary>
        private void ReadAirportSubsections(uint offset, uint count)
        {
            // Validate offset
            if (offset >= _stream.Length)
            {
                return;
            }

            _stream.Seek(offset, SeekOrigin.Begin);

            for (int i = 0; i < count; i++)
            {
                // Read subsection header (16 bytes - see atools subsection.cpp)
                // id (4), numDataRecords (4), firstDataRecordOffset (4), dataSize (4)
                int id = _reader.ReadInt32();
                int numRecords = _reader.ReadInt32();
                int firstRecordOffset = _reader.ReadInt32();
                int dataSize = _reader.ReadInt32();

                long nextSubsection = _stream.Position;

                // Validate offset before seeking
                if (firstRecordOffset < 0 || firstRecordOffset >= _stream.Length)
                {
                    continue;
                }

                // Read records in this subsection
                _stream.Seek(firstRecordOffset, SeekOrigin.Begin);

                for (int j = 0; j < numRecords; j++)
                {
                    long recordStart = _stream.Position;

                    // Check we have enough space for record header (6 bytes: id(2) + size(4))
                    if (_stream.Position + 6 > _stream.Length)
                        break;

                    ushort recordId = _reader.ReadUInt16();
                    uint recSize = _reader.ReadUInt32();

                    // Validate record size
                    if (recSize == 0 || recSize > _stream.Length - recordStart)
                        break;

                    // IMPORTANT: Like atools bglfile.cpp - trust SECTION TYPE, not record ID!
                    // Section type 0x03 = AIRPORT section, so ALL records in this section are airports
                    // Record IDs vary: 0x003C (FSX), 0x0056 (MSFS 2020/2024), 0x0113 (Navigraph MSFS 2024)
                    // atools doesn't check record ID in AIRPORT sections, just calls Airport constructor
                    ParseAirportRecord(recordStart, recSize);

                    // Move to next record
                    _stream.Seek(recordStart + recSize, SeekOrigin.Begin);
                }


                // Return to subsection list
                _stream.Seek(nextSubsection, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// Parse airport record and extract SID/STAR procedures
        /// Based on atools airport.cpp:64-91
        ///
        /// Airport Record Structure (MSFS 2020, Record 0x003C):
        /// Offset  Bytes  Description
        /// 0       2      Record ID (0x003C)
        /// 2       4      Record Size
        /// 6       1      numRunways
        /// 7       1      numComs
        /// 8       1      numStarts
        /// 9       1      numApproaches
        /// 10      1      numAprons
        /// 11      1      numHelipads
        /// 12      12     position (BglPosition - lat/lon/alt)
        /// 24      12     towerPosition (BglPosition)
        /// 36      4      magVar (float)
        /// 40      4      ident (Base38-encoded ICAO) ← CORRECT OFFSET!
        /// 44      4      region
        /// 48      4      fuelFlags
        /// ...
        /// </summary>
        private void ParseAirportRecord(long recordStart, uint recordSize)
        {
            long recordEnd = recordStart + recordSize;

            // Read airport header (minimum 52 bytes for MSFS 2020)
            _stream.Seek(recordStart, SeekOrigin.Begin);

            if (recordSize < 52)
            {
                Console.WriteLine($"[BglParser] Record too small: {recordSize} bytes");
                return;
            }

            byte[] header = _reader.ReadBytes((int)Math.Min(64, recordSize));

            // ICAO is at offset +40 from record start (Base38-encoded)
            // This is AFTER: id(2) + size(4) + counts(6) + position(12) + towerPos(12) + magVar(4) = 40
            const int ICAO_OFFSET = 40;

            uint icaoPacked = BitConverter.ToUInt32(header, ICAO_OFFSET);
            string airportIcao = BglConverter.IntToIcao(icaoPacked);

            if (string.IsNullOrEmpty(airportIcao) || airportIcao.Length < 3)
            {
                // Try alternative offset for different record versions
                if (header.Length >= 48)
                {
                    icaoPacked = BitConverter.ToUInt32(header, 44);
                    airportIcao = BglConverter.IntToIcao(icaoPacked);
                }
            }

            if (string.IsNullOrEmpty(airportIcao) || airportIcao.Length < 3)
            {
                return;
            }

            Console.WriteLine($"[BglParser] Found airport: {airportIcao}");

            // Skip airport header to sub-records (header is ~52 bytes for MSFS 2020)
            // Sub-records start after the airport header
            _stream.Seek(recordStart + 52, SeekOrigin.Begin);

            // Parse sub-records looking for SID/STAR
            while (_stream.Position < recordEnd)
            {
                long subRecStart = _stream.Position;

                if (_stream.Position + 6 > recordEnd)
                    break;

                ushort subRecId = _reader.ReadUInt16();
                uint subRecSize = _reader.ReadUInt32();

                if (subRecSize == 0 || subRecSize > recordSize)
                    break;

                try
                {
                    if (subRecId == BglRecordTypes.MSFS_SID || subRecId == BglRecordTypes.MSFS_STAR)
                    {
                        var proc = BglSidStar.Parse(_reader, subRecId, subRecSize);
                        if (proc != null && proc.IsValid())
                        {
                            proc.AirportIcao = airportIcao;
                            _procedures.Add(proc);

                            if (!_proceduresByAirport.ContainsKey(airportIcao))
                            {
                                _proceduresByAirport[airportIcao] = new List<BglSidStar>();
                            }
                            _proceduresByAirport[airportIcao].Add(proc);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BglParser] Error parsing sub-record at {subRecStart}: {ex.Message}");
                }

                // Move to next sub-record
                _stream.Seek(subRecStart + subRecSize, SeekOrigin.Begin);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _stream?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
