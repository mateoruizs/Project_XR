using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR && !UNITY_ANDROID
using System.IO.Ports;

public class ArduinoSerialSender : MonoBehaviour
{
    [Header("Serial settings")]
    public string portName = "COM3";
    public int baudRate = 115200;
    public int readTimeoutMs = 200;

    [Header("Timing")]
    public float delayBetweenCommands = 0.25f;

    private SerialPort _port;

    public bool IsOpen => _port != null && _port.IsOpen;

    public void Connect()
    {
        if (IsOpen) return;

        _port = new SerialPort(portName, baudRate);
        _port.NewLine = "\n";
        _port.ReadTimeout = readTimeoutMs;

        try
        {
            _port.Open();
            Debug.Log($"Serial: conectado a {portName} @ {baudRate}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Serial: no se pudo abrir {portName}. {e.Message}");
            _port = null;
        }
    }

    public void Disconnect()
    {
        if (!IsOpen) return;
        try { _port.Close(); } catch { }
        _port = null;
    }

    public void SendPlan(List<string> commands)
    {
        if (commands == null || commands.Count == 0)
        {
            Debug.LogError("Serial: no hay comandos para enviar.");
            return;
        }

        if (!IsOpen) Connect();
        if (!IsOpen) return;

        StartCoroutine(SendCoroutine(commands));
    }

    private IEnumerator SendCoroutine(List<string> commands)
    {
        foreach (var cmd in commands)
        {
            try
            {
                _port.WriteLine(cmd);
                Debug.Log($">> {cmd}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Serial write error: {e.Message}");
                yield break;
            }

            try
            {
                string reply = _port.ReadLine();
                Debug.Log($"<< {reply}");
            }
            catch
            {
                Debug.LogWarning("Serial: sin respuesta (timeout).");
            }

            yield return new WaitForSeconds(delayBetweenCommands);
        }

        Debug.Log("Serial: plan enviado completo.");
    }

    private void OnDisable()
    {
        Disconnect();
    }
}
#else

// 🔒 Stub seguro para Android / Quest / otras plataformas
public class ArduinoSerialSender : MonoBehaviour
{
    public void SendPlan(List<string> commands)
    {
        Debug.LogWarning("ArduinoSerialSender: SerialPort no disponible en esta plataforma.");
    }
}

#endif
