using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

/// <summary>
/// Corre en el PC del profesor.
/// Recibe la pose de las Quest y mueve el SpectatorCam para que
/// la pantalla del profesor muestre lo que ve el alumno.
/// </summary>
public class SpectatorPoseReceiver : MonoBehaviour
{
    [Header("Red")]
    public int listenPort = 47778;

    private UdpClient                _udp;
    private CancellationTokenSource  _cts;
    private ConcurrentQueue<string>  _queue = new ConcurrentQueue<string>();

    void OnEnable()
    {
        // No hacer nada en las Quest
        if (Application.platform == RuntimePlatform.Android)
        {
            enabled = false;
            return;
        }

        _udp = new UdpClient(listenPort);
        _cts = new CancellationTokenSource();
        ListenAsync(_cts.Token);
        Debug.Log("[SpectatorPoseReceiver] Escuchando pose en puerto " + listenPort);
    }

    private async void ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _udp.ReceiveAsync();
                string json = Encoding.UTF8.GetString(result.Buffer);
                _queue.Enqueue(json);
            }
            catch (System.Exception e)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogWarning("[SpectatorPoseReceiver] " + e.Message);
            }
        }
    }

    void Update()
    {
        while (_queue.TryDequeue(out string json))
        {
            PoseData data = JsonUtility.FromJson<PoseData>(json);
            if (data == null) continue;

            Debug.Log($"[SpectatorPoseReceiver] Pose recibida: pos=({data.px:F2},{data.py:F2},{data.pz:F2})");
            transform.position = new Vector3(data.px, data.py, data.pz);
            transform.rotation = new Quaternion(data.rx, data.ry, data.rz, data.rw);
        }
    }

    void OnDisable()
    {
        _cts?.Cancel();
        _udp?.Close();
    }
}
