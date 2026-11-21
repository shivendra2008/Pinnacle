
using UnityEngine;
using System;
using System.IO.Ports;
using System.Globalization;

[DefaultExecutionOrder(-1000)]      // read the serial port before any gameplay script asks for input this frame
public class ArduinoController : MonoBehaviour
{
    [Header("Connection")]
    public string portName = "/dev/cu.usbmodem1101";
    public int baudRate = 115200;

    private SerialPort serial;
    private string buffer = "";

    [Header("Left Joystick")]
    public float leftX;
    public float leftY;
    public bool leftButton;

    [Header("Right Joystick")]
    public float rightX;
    public float rightY;
    public bool rightButton;

    [Header("Status")]
    public bool isConnected;

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

    void Start()
    {
        ConnectArduino();
    }

    void ConnectArduino()
    {
        try
        {
            serial = new SerialPort(portName, baudRate);
            serial.ReadTimeout = 10;
            serial.NewLine = "\n";
            serial.Open();

            isConnected = true;
            Debug.Log("Arduino connected successfully!");
        }
        catch (Exception e)
        {
            isConnected = false;
            Debug.LogError("Arduino connection failed: " + e.Message);
        }
    }

    void Update()
    {
        if (serial == null || !serial.IsOpen)
            return;

        try
        {
            buffer += serial.ReadExisting();

            // Prevent an endlessly growing buffer
            if (buffer.Length > 2048)
                buffer = "";

            int newline;

            while ((newline = buffer.IndexOf('\n')) >= 0)
            {
                string line = buffer.Substring(0, newline).Trim();
                buffer = buffer.Substring(newline + 1);

                ProcessData(line);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Arduino read error: " + e.Message);
        }
    }

    void ProcessData(string data)
    {
        string[] values = data.Split(',');

        if (values.Length != 6)
            return;

        if (!float.TryParse(values[0], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float lx)) return;

        if (!float.TryParse(values[1], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float ly)) return;

        if (!int.TryParse(values[2], out int lb)) return;

        if (!float.TryParse(values[3], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float rx)) return;

        if (!float.TryParse(values[4], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float ry)) return;

        if (!int.TryParse(values[5], out int rb)) return;

        leftX = ApplyDeadzone((lx - 512f) / 512f);
        leftY = ApplyDeadzone((ly - 512f) / 512f);

        rightX = ApplyDeadzone((rx - 512f) / 512f);
        rightY = ApplyDeadzone((ry - 512f) / 512f);

        leftButton = lb == 0;
        rightButton = rb == 0;
        lastPacketTime = Time.unscaledTime;
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
        GUI.Box(new Rect(10, 10, 260, 170), "Arduino Controller");

        GUI.Label(new Rect(20, 40, 240, 25),
            "Status: " + (isConnected ? "CONNECTED" : "DISCONNECTED"));

        GUI.Label(new Rect(20, 70, 240, 25),
            $"Left: X={leftX:F2} Y={leftY:F2}");

        GUI.Label(new Rect(20, 95, 240, 25),
            $"Right: X={rightX:F2} Y={rightY:F2}");

        GUI.Label(new Rect(20, 120, 240, 25),
            $"Left Button: {leftButton}");

        GUI.Label(new Rect(20, 145, 240, 25),
            $"Right Button: {rightButton}");
    }

    void OnApplicationQuit()
    {
        CloseArduino();
    }

    void OnDestroy()
    {
        CloseArduino();
    }

    void CloseArduino()
    {
        isConnected = false;

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
