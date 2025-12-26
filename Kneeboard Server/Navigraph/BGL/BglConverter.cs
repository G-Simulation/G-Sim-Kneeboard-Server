using System;
using System.Text;

namespace Kneeboard_Server.Navigraph.BGL
{
    /// <summary>
    /// Converter utilities for BGL data - ported from Little Navmap atools/src/fs/bgl/converter.cpp
    /// </summary>
    public static class BglConverter
    {
        /// <summary>
        /// Convert packed integer to ICAO identifier string (5 characters max)
        /// Ported from atools converter.cpp intToIcao()
        /// </summary>
        /// <param name="icao">Packed ICAO value</param>
        /// <param name="noBitShift">If true, don't apply bit shift</param>
        /// <returns>ICAO identifier string</returns>
        public static string IntToIcao(uint icao, bool noBitShift = true)
        {
            return IntToIcaoInternal(icao, 5, noBitShift ? 0 : 5);
        }

        /// <summary>
        /// Convert packed long integer to ICAO identifier string (8 characters max) - for MSFS 2024
        /// Ported from atools converter.cpp intToIcaoLong()
        /// </summary>
        public static string IntToIcaoLong(ulong icao, bool noBitShift = true)
        {
            return IntToIcaoInternal(icao, 8, noBitShift ? 0 : 6);
        }

        /// <summary>
        /// Internal implementation of ICAO decoding
        /// Base-38 encoding: 0-9 = codes 2-11, A-Z = codes 12-37
        /// </summary>
        private static string IntToIcaoInternal(ulong icao, int numChars, int bitShift)
        {
            ulong value = icao >> bitShift;

            if (value == 0)
                return string.Empty;

            // Array for decoded values (max 8 characters for MSFS 2024)
            ulong[] codedArr = new ulong[8];
            int idx = 0;

            // Extract the coded/compressed values using base-38 decomposition
            if (value > 37)
            {
                while (value > 37 && idx < codedArr.Length)
                {
                    ulong coded = value % 38;
                    codedArr[idx++] = coded;
                    value = (value - coded) / 38;

                    if (value < 38 && idx < codedArr.Length)
                    {
                        codedArr[idx++] = value;
                    }
                }
            }
            else
            {
                codedArr[idx++] = value;
            }

            // Convert the decompressed bytes to characters
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < numChars && i < idx; i++)
            {
                uint codedChar = (uint)codedArr[i];
                if (codedChar == 0)
                    break;

                char c;
                if (codedChar > 1 && codedChar < 12)
                {
                    // Digits 0-9: codes 2-11
                    c = (char)('0' + (codedChar - 2));
                }
                else
                {
                    // Letters A-Z: codes 12-37
                    c = (char)('A' + (codedChar - 12));
                }
                result.Insert(0, c);
            }

            return result.ToString();
        }

        /// <summary>
        /// Convert runway designator code to string
        /// Ported from atools converter.cpp designatorStr()
        /// </summary>
        public static string DesignatorToString(int designator)
        {
            switch (designator)
            {
                case 0: return "";
                case 1: return "L";
                case 2: return "R";
                case 3: return "C";
                case 4: return "W";
                case 5: return "A";
                case 6: return "B";
                default: return "";
            }
        }

        /// <summary>
        /// Convert runway number and designator to runway string (e.g., "25L", "08R")
        /// Ported from atools converter.cpp runwayToStr()
        /// </summary>
        public static string RunwayToString(int runwayNumber, int designator)
        {
            string result;

            if (runwayNumber < 10)
            {
                // Single digit with leading zero
                result = "0" + runwayNumber.ToString();
            }
            else if (runwayNumber > 36)
            {
                // Special runway codes (N, NE, E, SE, S, SW, W, NW)
                switch (runwayNumber)
                {
                    case 37: return "N";
                    case 38: return "NE";
                    case 39: return "E";
                    case 40: return "SE";
                    case 41: return "S";
                    case 42: return "SW";
                    case 43: return "W";
                    case 44: return "NW";
                    default: return runwayNumber.ToString();
                }
            }
            else
            {
                // Two digit runway number
                result = runwayNumber.ToString("D2");
            }

            return result + DesignatorToString(designator);
        }

        /// <summary>
        /// Convert packed latitude (24-bit) to degrees
        /// </summary>
        public static double UnpackLatitude(uint packed)
        {
            // Formula: lat = (packed * 360.0 / 0x1000000) - 90
            return (packed * 360.0 / 16777216.0) - 90.0;
        }

        /// <summary>
        /// Convert packed longitude (24-bit) to degrees
        /// </summary>
        public static double UnpackLongitude(uint packed)
        {
            // Formula: lon = (packed * 360.0 / 0x1000000) - 180
            return (packed * 360.0 / 16777216.0) - 180.0;
        }

        /// <summary>
        /// Convert BGL latitude (32-bit integer) to degrees
        /// Uses different formula than 24-bit packed version
        /// </summary>
        public static double BglLatitudeToDegrees(int bglLat)
        {
            // BGL uses 90 / (10001750.0 / 65536 / 65536) formula
            return bglLat * (90.0 / 10001750.0) * 65536.0 * 65536.0 / 65536.0 / 65536.0;
        }

        /// <summary>
        /// Convert BGL longitude (32-bit integer) to degrees
        /// </summary>
        public static double BglLongitudeToDegrees(int bglLon)
        {
            // BGL uses different range for longitude
            return bglLon * (360.0 / 0x100000000L);
        }
    }
}
