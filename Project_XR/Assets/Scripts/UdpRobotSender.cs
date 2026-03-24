using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UdpRobotSender : MonoBehaviour
{
    public string robotIp = "172.20.10.2";
    public int robotPort = 4210;

    [Header("Handshake")]
    public float commandTimeoutSeconds = 3f;
    public int maxRetriesPerCommand = 2;

    [Header("Control")]
    public string stopCommand = "STOP";

    [Header("UI / Feedback (optional)")]
    public RobotFeedbackUI robotFeedbackUI;   // arrastra aquí tu RobotFeedbackUI en el inspector

    private UdpClient _udp;
    private bool _gotDone;
    private Coroutine _receiveLoop;

    // Plan state
    private Coroutine _sendRoutine;
    private List<string> _activePlan;
    private int _currentIndex;
    private bool _paused;
    private bool _aborted;

    public bool IsRunning => _sendRoutine != null;
    public bool IsPaused => _paused;

    private void Awake()
    {
        _udp = new UdpClient(0);
        _udp.Connect(robotIp, robotPort);
        _receiveLoop = StartCoroutine(ReceiveLoop());
    }

    private static bool IsMergeable(string cmd)
    {
        return cmd == "FORWARD" || cmd == "BACK" || cmd == "LEFT" || cmd == "RIGHT";
    }

    private static List<string> CompactCommands(List<string> commands)
    {
        var result = new List<string>();
        if (commands == null || commands.Count == 0) return result;

        string prev = commands[0].Trim();
        int count = 1;

        for (int i = 1; i < commands.Count; i++)
        {
            string cur = commands[i].Trim();

            if (cur == prev && IsMergeable(cur))
            {
                count++;
            }
            else
            {
                result.Add(IsMergeable(prev) ? $"{prev} {count}" : prev);
                prev = cur;
                count = 1;
            }
        }

        result.Add(IsMergeable(prev) ? $"{prev} {count}" : prev);
        return result;
    }

    private IEnumerator ReceiveLoop()
    {
        while (_udp != null)
        {
            if (_udp.Available > 0)
            {
                try
                {
                    System.Net.IPEndPoint remoteEndPoint = null;
                    var data = _udp.Receive(ref remoteEndPoint);
                    string msg = Encoding.UTF8.GetString(data).Trim();
                    if (msg == "DONE") _gotDone = true;
                }
                catch { }
            }

            yield return null;
        }
    }

    public void SendPlan(List<string> commands)
    {
        if (commands == null || commands.Count == 0) return;

        var compacted = CompactCommands(commands);

        _activePlan = compacted;
        _currentIndex = 0;
        _paused = false;
        _aborted = false;

        // ✅ avisar a UI/Planner de que empieza el plan
        if (robotFeedbackUI != null) robotFeedbackUI.NotifyPlanStarted();
        if (PathPlanner.Instance != null) PathPlanner.Instance.NotifyPlanStarted();

        if (_sendRoutine != null) StopCoroutine(_sendRoutine);
        _sendRoutine = StartCoroutine(SendPlanHandshake());
    }

    public void PausePlanAndStopRobot()
    {
        _paused = true;
        Send(stopCommand);
    }

    public void ResumePlan()
    {
        _paused = false;
    }

    // Si algún día quieres abortar definitivo (sin resume)
    public void AbortPlan()
    {
        _aborted = true;
        _paused = false;
        Send(stopCommand);

        if (_sendRoutine != null) StopCoroutine(_sendRoutine);
        _sendRoutine = null;

        // ✅ quitar highlight / estado
        if (robotFeedbackUI != null) robotFeedbackUI.OnRobotStoppedOrFinished();
        if (PathPlanner.Instance != null) PathPlanner.Instance.NotifyPlanStopped();
    }

    private IEnumerator SendPlanHandshake()
    {
        if (_activePlan == null || _activePlan.Count == 0)
        {
            _sendRoutine = null;
            yield break;
        }

        for (int i = _currentIndex; i < _activePlan.Count; i++)
        {
            _currentIndex = i;

            while (_paused) yield return null;
            if (_aborted) break;

            string cmd = _activePlan[i];
            bool success = false;

            for (int attempt = 0; attempt <= maxRetriesPerCommand; attempt++)
            {
                _gotDone = false;
                Send(cmd);

                float t = 0f;
                while (!_gotDone && t < commandTimeoutSeconds)
                {
                    while (_paused) yield return null;
                    if (_aborted) break;

                    t += Time.deltaTime;
                    yield return null;
                }

                if (_aborted) break;

                if (_gotDone)
                {
                    success = true;

                    // ✅ aquí: el robot ha terminado este comando -> actualiza UI
                    ApplyFeedbackForCommand(cmd);

                    break;
                }
            }

            if (!success)
            {
                _sendRoutine = null;

                // ✅ quitar highlight / estado si falló
                if (robotFeedbackUI != null) robotFeedbackUI.OnRobotStoppedOrFinished();
                if (PathPlanner.Instance != null) PathPlanner.Instance.NotifyPlanStopped();

                yield break;
            }
        }

        _sendRoutine = null;

        // ✅ plan terminado (aunque el último sea STOP, ya acabó)
        if (robotFeedbackUI != null) robotFeedbackUI.OnRobotStoppedOrFinished();
        if (PathPlanner.Instance != null) PathPlanner.Instance.NotifyPlanStopped();
    }

    // ===========================
    // FEEDBACK POR COMANDO
    // ===========================
    private void ApplyFeedbackForCommand(string cmdRaw)
    {
        if (robotFeedbackUI == null) return;

        // cmdRaw puede venir como "FORWARD 3"
        string cmd = cmdRaw.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        string[] parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string main = parts[0].Trim().ToUpperInvariant();

        int count = 1;
        if (parts.Length >= 2)
        {
            int.TryParse(parts[1], out count);
            if (count <= 0) count = 1;
        }

        switch (main)
        {
            case "FORWARD":
                // si viene compactado, avanzamos varias celdas
                for (int k = 0; k < count; k++)
                    robotFeedbackUI.NotifyMovedOneCell();
                break;

            case "LIFTUP":
                robotFeedbackUI.NotifyPickedBox();
                break;

            case "LIFTDOWN":
                robotFeedbackUI.NotifyDroppedBox();
                if (PathPlanner.Instance != null) PathPlanner.Instance.NotifySegmentCompleted();
                break;

            // giros / stop no afectan a CM por celda (si quieres, lo añadimos)
            case "LEFT":
            case "RIGHT":
            case "BACK":
            case "STOP":
            default:
                break;
        }
    }

    public void Send(string cmd)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(cmd + "\n");
            _udp.Send(data, data.Length);
        }
        catch { }
    }

    private void OnDestroy()
    {
        if (_receiveLoop != null) StopCoroutine(_receiveLoop);
        if (_sendRoutine != null) StopCoroutine(_sendRoutine);

        _udp?.Close();
        _udp = null;
    }
}
