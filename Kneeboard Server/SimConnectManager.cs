using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.FlightSimulator.SimConnect;

namespace Kneeboard_Server
{
    public class SimConnectManager : IDisposable
    {
        private SimConnect simConnect;
        private IntPtr windowHandle;
        private Thread reconnectThread;
        private volatile bool isConnected = false;
        private volatile bool isRunning = true;
        private const int WM_USER_SIMCONNECT = 0x0402;

        // Thread-safe Position Storage
        private readonly object dataLock = new object();
        private AircraftPosition? latestPosition = null;

        // SimConnect Enums
        private enum DATA_REQUESTS { AIRCRAFT_POSITION }
        private enum DEFINITIONS { AircraftPosition }

        // Event IDs for commands
        private enum EVENTS
        {
            PAUSE_ON,
            PAUSE_OFF,
            COM1_RADIO_SET_HZ,
            COM1_RADIO_SWAP,
            COM2_RADIO_SET_HZ,
            COM2_RADIO_SWAP,
            NAV1_RADIO_SET_HZ,
            NAV1_RADIO_SWAP,
            NAV2_RADIO_SET_HZ,
            NAV2_RADIO_SWAP,
            ADF1_RADIO_SET,
            ADF1_RADIO_SWAP,
            ADF2_RADIO_SET,
            ADF2_RADIO_SWAP
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct AircraftPosition
        {
            public double Latitude;             // PLANE LATITUDE (degrees)
            public double Longitude;            // PLANE LONGITUDE (degrees)
            public double Altitude;             // PLANE ALTITUDE (feet)
            public double Heading;              // PLANE HEADING DEGREES TRUE (degrees)
            public double GroundSpeed;          // GROUND VELOCITY (knots)
            public double IndicatedAirspeed;    // AIRSPEED INDICATED (knots)
            public double WindDirection;        // AMBIENT WIND DIRECTION (degrees)
            public double WindSpeed;            // AMBIENT WIND VELOCITY (knots)
        }

        public SimConnectManager(IntPtr handle)
        {
            windowHandle = handle;
        }

        public void Start()
        {
            isRunning = true;
            reconnectThread = new Thread(ReconnectionLoop);
            reconnectThread.IsBackground = true;
            reconnectThread.Start();
        }

        public void Stop()
        {
            isRunning = false;
            Disconnect();
            reconnectThread?.Join(2000);
        }

        private void ReconnectionLoop()
        {
            while (isRunning)
            {
                if (!isConnected)
                {
                    try
                    {
                        Connect();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SimConnect] Connection attempt failed: {ex.Message}");
                    }
                }
                Thread.Sleep(5000); // Retry every 5 seconds
            }
        }

        private void Connect()
        {
            try
            {
                simConnect = new SimConnect("Kneeboard Server", windowHandle, WM_USER_SIMCONNECT, null, 0);

                // Event handlers
                simConnect.OnRecvOpen += OnRecvOpen;
                simConnect.OnRecvQuit += OnRecvQuit;
                simConnect.OnRecvException += OnRecvException;
                simConnect.OnRecvSimobjectData += OnRecvSimobjectData;

                Console.WriteLine("[SimConnect] Connecting to MSFS...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnect] Connect failed: {ex.Message}");
                simConnect = null;
                throw;
            }
        }

        private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("[SimConnect] Connected to " + data.szApplicationName);
            isConnected = true;
            SetupDataDefinitions();
        }

        private void SetupDataDefinitions()
        {
            // Define Aircraft Position structure
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "GROUND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "AIRSPEED INDICATED", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "AMBIENT WIND DIRECTION", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "AMBIENT WIND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);

            simConnect.RegisterDataDefineStruct<AircraftPosition>(DEFINITIONS.AircraftPosition);

            // Request data every second
            simConnect.RequestDataOnSimObject(DATA_REQUESTS.AIRCRAFT_POSITION,
                DEFINITIONS.AircraftPosition, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

            // Map Events for commands
            simConnect.MapClientEventToSimEvent(EVENTS.PAUSE_ON, "PAUSE_ON");
            simConnect.MapClientEventToSimEvent(EVENTS.PAUSE_OFF, "PAUSE_OFF");
            simConnect.MapClientEventToSimEvent(EVENTS.COM1_RADIO_SET_HZ, "COM_STBY_RADIO_SET_HZ");
            simConnect.MapClientEventToSimEvent(EVENTS.COM1_RADIO_SWAP, "COM_STBY_RADIO_SWAP");
            simConnect.MapClientEventToSimEvent(EVENTS.COM2_RADIO_SET_HZ, "COM2_STBY_RADIO_SET_HZ");
            simConnect.MapClientEventToSimEvent(EVENTS.COM2_RADIO_SWAP, "COM2_RADIO_SWAP");
            simConnect.MapClientEventToSimEvent(EVENTS.NAV1_RADIO_SET_HZ, "NAV1_STBY_SET_HZ");
            simConnect.MapClientEventToSimEvent(EVENTS.NAV1_RADIO_SWAP, "NAV1_RADIO_SWAP");
            simConnect.MapClientEventToSimEvent(EVENTS.NAV2_RADIO_SET_HZ, "NAV2_STBY_SET_HZ");
            simConnect.MapClientEventToSimEvent(EVENTS.NAV2_RADIO_SWAP, "NAV2_RADIO_SWAP");
            simConnect.MapClientEventToSimEvent(EVENTS.ADF1_RADIO_SET, "ADF_STBY_SET");
            simConnect.MapClientEventToSimEvent(EVENTS.ADF1_RADIO_SWAP, "ADF1_RADIO_SWAP");
            simConnect.MapClientEventToSimEvent(EVENTS.ADF2_RADIO_SET, "ADF2_STBY_SET");
            simConnect.MapClientEventToSimEvent(EVENTS.ADF2_RADIO_SWAP, "ADF2_RADIO_SWAP");

            Console.WriteLine("[SimConnect] Data subscription configured (1 Hz)");
        }

        // Public Command Methods

        public void SetPause(bool paused)
        {
            if (!isConnected || simConnect == null) return;

            try
            {
                simConnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    paused ? EVENTS.PAUSE_ON : EVENTS.PAUSE_OFF, 0,
                    (Enum)(object)1,
                    SIMCONNECT_EVENT_FLAG.DEFAULT);
                Console.WriteLine($"[SimConnect] Pause set to {paused}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnect] SetPause error: {ex.Message}");
            }
        }

        public void Teleport(double lat, double lng, double? altitude = null, double? heading = null, double? speed = null)
        {
            if (!isConnected || simConnect == null) return;

            try
            {
                // Pause first
                SetPause(true);
                Thread.Sleep(100);

                // Set position
                simConnect.SetDataOnSimObject(DEFINITIONS.AircraftPosition,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_DATA_SET_FLAG.DEFAULT,
                    new AircraftPosition
                    {
                        Latitude = lat,
                        Longitude = lng,
                        Altitude = altitude ?? 1000.0,
                        Heading = heading ?? 0.0,
                        GroundSpeed = speed ?? 0.0
                    });

                Console.WriteLine($"[SimConnect] Teleported to {lat:F6}, {lng:F6}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnect] Teleport error: {ex.Message}");
            }
        }

        public void SetRadioFrequency(string radio, uint frequencyHz)
        {
            if (!isConnected || simConnect == null) return;

            try
            {
                EVENTS setEvent, swapEvent;

                switch (radio.ToUpper())
                {
                    case "COM1_ACTIVE":
                    case "COM1_STANDBY":
                        setEvent = EVENTS.COM1_RADIO_SET_HZ;
                        swapEvent = EVENTS.COM1_RADIO_SWAP;
                        break;
                    case "COM2_ACTIVE":
                    case "COM2_STANDBY":
                        setEvent = EVENTS.COM2_RADIO_SET_HZ;
                        swapEvent = EVENTS.COM2_RADIO_SWAP;
                        break;
                    case "NAV1_ACTIVE":
                    case "NAV1_STANDBY":
                        setEvent = EVENTS.NAV1_RADIO_SET_HZ;
                        swapEvent = EVENTS.NAV1_RADIO_SWAP;
                        break;
                    case "NAV2_ACTIVE":
                    case "NAV2_STANDBY":
                        setEvent = EVENTS.NAV2_RADIO_SET_HZ;
                        swapEvent = EVENTS.NAV2_RADIO_SWAP;
                        break;
                    case "ADF1_ACTIVE":
                    case "ADF1_STANDBY":
                        setEvent = EVENTS.ADF1_RADIO_SET;
                        swapEvent = EVENTS.ADF1_RADIO_SWAP;
                        break;
                    case "ADF2_ACTIVE":
                    case "ADF2_STANDBY":
                        setEvent = EVENTS.ADF2_RADIO_SET;
                        swapEvent = EVENTS.ADF2_RADIO_SWAP;
                        break;
                    default:
                        Console.WriteLine($"[SimConnect] Unknown radio: {radio}");
                        return;
                }

                // Set standby frequency
                simConnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    setEvent, frequencyHz,
                    (Enum)(object)1,
                    SIMCONNECT_EVENT_FLAG.DEFAULT);

                // Swap to active if needed
                if (radio.EndsWith("_ACTIVE"))
                {
                    Thread.Sleep(50);
                    simConnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        swapEvent, 1,
                        (Enum)(object)1,
                        SIMCONNECT_EVENT_FLAG.DEFAULT);
                }

                Console.WriteLine($"[SimConnect] Set {radio} to {frequencyHz} Hz");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimConnect] SetRadioFrequency error: {ex.Message}");
            }
        }

        private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if (data.dwRequestID == (uint)DATA_REQUESTS.AIRCRAFT_POSITION)
            {
                var position = (AircraftPosition)data.dwData[0];

                lock (dataLock)
                {
                    latestPosition = position;
                }
            }
        }

        private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine("[SimConnect] Simulator closed");
            Disconnect();
        }

        private void OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Console.WriteLine($"[SimConnect] Exception: {(SIMCONNECT_EXCEPTION)data.dwException}");
        }

        private void Disconnect()
        {
            if (simConnect != null)
            {
                simConnect.Dispose();
                simConnect = null;
            }
            isConnected = false;
            latestPosition = null;
        }

        public void HandleWindowMessage(ref Message m)
        {
            if (m.Msg == WM_USER_SIMCONNECT && simConnect != null)
            {
                try
                {
                    simConnect.ReceiveMessage();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SimConnect] Message handling error: {ex.Message}");
                }
            }
        }

        // Public API
        public bool IsConnected => isConnected;

        public AircraftPosition? GetLatestPosition()
        {
            lock (dataLock)
            {
                return latestPosition;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
