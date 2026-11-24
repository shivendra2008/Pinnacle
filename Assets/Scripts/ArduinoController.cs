
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Threading;

[DefaultExecutionOrder(-1000)]      // read the serial port before any gameplay script asks for input this frame
public class ArduinoController : MonoBehaviour
{
    [Header("Connection")]
    [Tooltip("Tried first. It can stay as it is on another computer: when this port is not there, the script looks for the " +
             "board itself (Windows: the COM ports; Mac: /dev/cu.usbmodem* and /dev/cu.usbserial*).")]
    public string portName = "/dev/cu.usbmodem1101";
    public int baudRate = 115200;
    [Tooltip("Look for the board on every USB serial port, and keep looking every 2 s until one sends controller lines, so " +
             "the board can be plugged in after the game starts. Untick to use Port Name only.")]
    public bool autoDetect = true;

    private SerialPort serial;
    private string buffer = "";
    private bool skipPartialLine;       // the first text read from a port can start in the middle of a line

    [Header("Left Joystick")]
    public float leftX;
    public float leftY;
    public bool leftButton;

    [Header("Right Joystick")]
    public float rightX;
    public float rightY;
    public bool rightButton;

    [Header("Option Button (D4)")]
    [Tooltip("True while the D4 button is held. It is wired with INPUT_PULLUP, so the board sends 0 when pressed and 1 when released.")]
    public bool optionButton;

    [Header("Status")]
    public bool isConnected;
    [Tooltip("The port the board was found on.")]
    public string connectedPort = "";

    [Header("Gameplay settings (used by GameInput / PriyaRoomPlayer)")]
    [Tooltip("Right stick camera speed at full deflection, degrees per second.")]
    public float lookSpeed = 120f;
    [Tooltip("Upward speed (m/s) when the left button is pressed. 5 = about a 1.3 m jump; around 3.5 is more natural.")]
    public float jumpSpeed = 5f;
    [Tooltip("Tick if pushing the RIGHT stick up makes you look down.")]
    public bool invertLookY = false;
    [Tooltip("Tick if pushing the LEFT stick up makes you walk backwards.")]
    public bool invertMoveY = false;
    [Tooltip("Ignore a second press of the same button within this many seconds (switch bounce).")]
    public float pressLockout = 0.12f;
    [HideInInspector] public float lastPacketTime = -999f;     // Time.unscaledTime of the last valid line from the board

    // Finding the board. A port counts as the controller once it sends a line in the controller's format. Opening a port and
    // waiting for that line takes up to ProbeMs, so ports are tried on a background thread and no frame waits for it; the
    // thread hands the open port to Update through foundPort. Opening a port restarts most boards (an Uno does), so a port
    // that opened but sent no controller lines is not opened again until it is unplugged: another device on a USB serial
    // port is restarted at most once, not every few seconds.
    const float RescanSeconds = 2f;     // while there is no board: how often to look again
    const int ProbeMs = 3000;           // how long an opened port has to send a controller line. An Uno restarts when its
                                        // port opens and sends its first line about 1.5 s later.

    readonly object scanLock = new object();
    readonly HashSet<string> silentPorts = new HashSet<string>();     // opened, sent no controller lines (guarded by scanLock)
    volatile bool scanning;
    volatile bool shuttingDown;
    SerialPort foundPort;               // guarded by scanLock
    string foundPortName;               // guarded by scanLock
    string scanReport;                  // guarded by scanLock: why the last pass found nothing
    float nextScanTime;
    string lastMissingReport;

    void Start()
    {
        StartScan();
    }

    void Update()
    {
        if (serial == null)
        {
            TakeScanResult();
            if (serial == null)
            {
                if (!scanning && Time.unscaledTime >= nextScanTime)
                    StartScan();
                return;
            }
        }

        try
        {
            buffer += serial.ReadExisting();

            // Prevent an endlessly growing buffer
            if (buffer.Length > 2048)
            {
                buffer = "";
                skipPartialLine = true;
            }

            int newline;

            while ((newline = buffer.IndexOf('\n')) >= 0)
            {
                string line = buffer.Substring(0, newline).Trim();
                buffer = buffer.Substring(newline + 1);

                if (skipPartialLine)
                {
                    skipPartialLine = false;
                    continue;
                }

                ProcessData(line);
            }
        }
        catch (Exception e)
        {
            // An unplugged board ends up here. Let go of the port and look again, so plugging it back in works.
            Debug.LogWarning("Arduino read error on " + connectedPort + ": " + e.Message + ". Looking for the controller again.");
            CloseArduino();
            nextScanTime = Time.unscaledTime + RescanSeconds;
        }
    }

    // ---------------------------------------------------------------- finding the board

    static bool IsWindows =>
        Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

    void StartScan()
    {
        nextScanTime = Time.unscaledTime + RescanSeconds;
        bool windows = IsWindows;
        List<string> ports = Candidates(windows);
        bool anySilent;
        lock (scanLock)
        {
            silentPorts.IntersectWith(ports);       // a port that has gone (unplugged) is tried again when it comes back
            ports.RemoveAll(silentPorts.Contains);
            anySilent = silentPorts.Count > 0;
        }

        if (ports.Count == 0)
        {
            if (!anySilent)                         // a silent port was already reported by the pass that tried it
                ReportMissing(autoDetect ? "no USB serial port" : (portName ?? "") + " is not there");
            return;
        }

        int baud = baudRate;
        scanning = true;
        var thread = new Thread(() => Scan(ports, baud)) { IsBackground = true, Name = "Arduino port scan" };
        thread.Start();
    }

    // The saved port first, when it exists on this computer, then every USB serial port.
    List<string> Candidates(bool windows)
    {
        List<string> listed = autoDetect ? ListUsbSerialPorts(windows) : new List<string>();
        var ports = new List<string>();

        string saved = (portName ?? "").Trim();
        if (windows)
        {
            if (saved.StartsWith(@"\\.\", StringComparison.Ordinal)) saved = saved.Substring(4);      // Unity adds this itself and refuses a name that has it
            saved = saved.ToUpperInvariant();
        }
        bool savedHere = windows ? saved.StartsWith("COM", StringComparison.Ordinal) : saved.Length > 0 && File.Exists(saved);
        if (savedHere || listed.Contains(saved))
            ports.Add(saved);

        foreach (string port in listed)
            if (!ports.Contains(port))
                ports.Add(port);
        return ports;
    }

    static List<string> ListUsbSerialPorts(bool windows)
    {
        if (windows)
            return ListWindowsPorts();

        // Mac: usbmodem (an Uno and most boards), usbserial and wchusbserial (FTDI and CH340 adapters, as on many clones),
        // SLAB_USBtoUART (CP210x). Linux: ttyACM and ttyUSB. Bluetooth ports are left out.
        var ports = new List<string>();
        string[] patterns = { "cu.usbmodem*", "cu.usbserial*", "cu.wchusbserial*", "cu.SLAB_USBtoUART*", "ttyACM*", "ttyUSB*" };
        foreach (string pattern in patterns)
        {
            try
            {
                string[] found = Directory.GetFiles("/dev", pattern);
                Array.Sort(found, StringComparer.Ordinal);
                ports.AddRange(found);
            }
            catch (Exception) { }
        }
        return ports;
    }

    // HARDWARE\DEVICEMAP\SERIALCOMM names the device behind each COM port, e.g. \Device\USBSER000 -> COM3 for an Uno. USB
    // ports come first, then anything else, each in COM-number order. Bluetooth ports (\Device\BthModem0) are left out: opening
    // one makes Windows try to reach the paired device, which can take many seconds.
    static List<string> ListWindowsPorts()
    {
        var ranked = new List<KeyValuePair<int, string>>();
        bool readRegistry = false;
        try
        {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM"))
            {
                if (key != null)
                {
                    foreach (string device in key.GetValueNames())
                    {
                        string com = ((key.GetValue(device) as string) ?? "").Trim().ToUpperInvariant();
                        string d = device.ToUpperInvariant();
                        if (com.Length == 0 || d.Contains("BTH"))
                            continue;
                        bool usb = d.Contains("USB") || d.Contains("VCP") || d.Contains("SILAB") || d.Contains("CH34") || d.Contains("WCH");
                        ranked.Add(new KeyValuePair<int, string>(usb ? 0 : 1, com));
                    }
                }
                readRegistry = true;
            }
        }
        catch (Exception) { }

        if (!readRegistry)
        {
            try
            {
                foreach (string com in SerialPort.GetPortNames())
                    ranked.Add(new KeyValuePair<int, string>(1, com.Trim().ToUpperInvariant()));
            }
            catch (Exception) { }
        }

        ranked.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : ComNumber(a.Value).CompareTo(ComNumber(b.Value)));
        var ports = new List<string>();
        foreach (var entry in ranked)
            if (!ports.Contains(entry.Value))
                ports.Add(entry.Value);
        return ports;
    }

    static int ComNumber(string port)
    {
        return port.StartsWith("COM", StringComparison.Ordinal) && int.TryParse(port.Substring(3), out int n) ? n : int.MaxValue;
    }

    // Runs on the scan thread: no Unity calls in here.
    void Scan(List<string> ports, int baud)
    {
        SerialPort found = null;
        string foundName = null;
        var notes = new List<string>();
        var silent = new List<string>();

        foreach (string name in ports)
        {
            if (shuttingDown)
                break;

            SerialPort port;
            try
            {
                port = OpenPort(name, baud);
            }
            catch (Exception e)
            {
                notes.Add(name + ": " + e.Message);         // busy or refused: tried again on the next pass
                continue;
            }

            if (SendsControllerLines(port))
            {
                found = port;
                foundName = name;
                break;
            }

            Shut(port);
            silent.Add(name);
            notes.Add(name + " sent no controller lines (unplug it and plug it back in to try it again)");
        }

        lock (scanLock)
        {
            silentPorts.UnionWith(silent);
            if (found != null && shuttingDown)
            {
                Shut(found);
                found = null;
            }
            foundPort = found;
            foundPortName = foundName;
            scanReport = found == null && notes.Count > 0 ? string.Join("; ", notes) : null;
            scanning = false;
        }
    }

    static SerialPort OpenPort(string name, int baud)
    {
        var port = new SerialPort(name, baud);
        port.ReadTimeout = 10;
        port.NewLine = "\n";
        // Unity turns DTR off as it opens a port unless asked not to. An Uno R3 sends either way, but boards that run USB
        // themselves (Leonardo, Micro and some newer ones) send nothing while it is off. Turning it on restarts an Uno, which
        // opening the port already does on the Mac.
        port.DtrEnable = true;
        try
        {
            port.Open();
        }
        catch
        {
            port.Dispose();
            throw;
        }
        return port;
    }

    // Runs on the scan thread.
    bool SendsControllerLines(SerialPort port)
    {
        string text = "";
        bool wholeLines = false;                            // text before the first newline may be the end of a line
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            while (clock.ElapsedMilliseconds < ProbeMs && !shuttingDown)
            {
                text += port.ReadExisting();
                int newline;
                while ((newline = text.IndexOf('\n')) >= 0)
                {
                    string line = text.Substring(0, newline).Trim();
                    text = text.Substring(newline + 1);
                    if (wholeLines && TryParse(line, out _))
                        return true;
                    wholeLines = true;
                }
                if (text.Length > 2048)
                {
                    text = "";
                    wholeLines = false;
                }
                Thread.Sleep(20);
            }
        }
        catch (Exception) { }
        return false;
    }

    static void Shut(SerialPort port)
    {
        try
        {
            if (port.IsOpen)
                port.Close();
            port.Dispose();
        }
        catch (Exception) { }
    }

    void TakeScanResult()
    {
        if (scanning)
            return;

        string report;
        lock (scanLock)
        {
            if (foundPort != null)
            {
                serial = foundPort;
                connectedPort = foundPortName;
                foundPort = null;
            }
            report = scanReport;
            scanReport = null;
        }

        if (serial != null)
        {
            buffer = "";
            skipPartialLine = true;
            isConnected = true;
            lastMissingReport = null;
            Debug.Log("Arduino connected on " + connectedPort);
        }
        else if (report != null)
        {
            ReportMissing(report);
        }
    }

    // Logged when the reason changes, not on every pass.
    void ReportMissing(string details)
    {
        if (details == lastMissingReport)
            return;
        lastMissingReport = details;
        Debug.LogWarning("Arduino controller not found (" + details + "). Still looking: plug the board in, and close any " +
                         "other program using it, such as the Arduino IDE's Serial Monitor. Keyboard, mouse and gamepad still work.");
    }

    // ---------------------------------------------------------------- reading the board

    struct Packet
    {
        public float lx, ly, rx, ry;
        public int lb, rb, ob;
    }

    static bool TryParse(string data, out Packet p)
    {
        p = default;
        string[] values = data.Split(',');

        // lx,ly,lb,rx,ry,rb,optionButton. A line of six (the older sketch, without D4) is still read: D4 then counts
        // as released.
        if (values.Length != 7 && values.Length != 6)
            return false;

        if (!float.TryParse(values[0], NumberStyles.Float,
            CultureInfo.InvariantCulture, out p.lx)) return false;

        if (!float.TryParse(values[1], NumberStyles.Float,
            CultureInfo.InvariantCulture, out p.ly)) return false;

        if (!int.TryParse(values[2], out p.lb)) return false;

        if (!float.TryParse(values[3], NumberStyles.Float,
            CultureInfo.InvariantCulture, out p.rx)) return false;

        if (!float.TryParse(values[4], NumberStyles.Float,
            CultureInfo.InvariantCulture, out p.ry)) return false;

        if (!int.TryParse(values[5], out p.rb)) return false;

        p.ob = 1;                                         // released
        if (values.Length == 7 && !int.TryParse(values[6], out p.ob)) return false;

        // A button is only ever 0 or 1. Anything else is a line that arrived cut short, its values shifted: skip it.
        return IsButton(p.lb) && IsButton(p.rb) && IsButton(p.ob);
    }

    void ProcessData(string data)
    {
        if (!TryParse(data, out Packet p))
            return;

        leftX = ApplyDeadzone((p.lx - 512f) / 512f);
        leftY = ApplyDeadzone((p.ly - 512f) / 512f);

        rightX = ApplyDeadzone((p.rx - 512f) / 512f);
        rightY = ApplyDeadzone((p.ry - 512f) / 512f);

        leftButton = p.lb == 0;
        rightButton = p.rb == 0;
        optionButton = p.ob == 0;
        lastPacketTime = Time.unscaledTime;
    }

    static bool IsButton(int value)
    {
        return value == 0 || value == 1;
    }

    float ApplyDeadzone(float value)
    {
        return Mathf.Abs(value) < 0.08f ? 0f : value;
    }

    bool showPanel;

    void OnGUI()
    {
        // The status panel sat over the objective box in every act, saying DISCONNECTED to everyone without the
        // controller. It shows while a controller is connected; F3 shows or hides it either way.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (Event.current.type == EventType.Layout && kb != null && kb.f3Key.wasPressedThisFrame) showPanel = !showPanel;
        if (!(isConnected ^ showPanel)) return;
        GUI.Box(new Rect(10, 10, 260, 220), "Arduino Controller");

        GUI.Label(new Rect(20, 40, 240, 25),
            "Status: " + (isConnected ? "CONNECTED" : "DISCONNECTED (looking)"));

        GUI.Label(new Rect(20, 65, 240, 25),
            "Port: " + (isConnected ? connectedPort : "-"));

        GUI.Label(new Rect(20, 95, 240, 25),
            $"Left: X={leftX:F2} Y={leftY:F2}");

        GUI.Label(new Rect(20, 120, 240, 25),
            $"Right: X={rightX:F2} Y={rightY:F2}");

        GUI.Label(new Rect(20, 145, 240, 25),
            $"Left Button: {leftButton}");

        GUI.Label(new Rect(20, 170, 240, 25),
            $"Right Button: {rightButton}");

        GUI.Label(new Rect(20, 195, 240, 25),
            $"Option Button (D4): {optionButton}");
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        Shutdown();
    }

    // Stops the scan thread from handing over a port, closes one it already handed over, and closes the open port.
    void Shutdown()
    {
        shuttingDown = true;
        lock (scanLock)
        {
            if (foundPort != null)
            {
                Shut(foundPort);
                foundPort = null;
            }
        }
        CloseArduino();
    }

    void CloseArduino()
    {
        isConnected = false;
        connectedPort = "";

        if (serial != null)
        {
            try
            {
                if (serial.IsOpen)
                    serial.Close();

                serial.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error closing Arduino: " + e.Message);
            }

            serial = null;
        }
    }
}
