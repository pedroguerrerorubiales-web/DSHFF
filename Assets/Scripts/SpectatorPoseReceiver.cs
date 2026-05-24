using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

/// <summary>
/// Corre en el PC del profesor.
///
/// MODO LINK (cable o Air Link):
///   Asigna CenterEyeAnchor en el campo headTarget.
///   El SpectatorCam sigue directamente la cabeza sin necesidad de red.
///
/// MODO STANDALONE APK (Quest sin Link):
///   Deja headTarget vacio. Recibe la pose por UDP desde QuestPoseSender.
/// </summary>
public class SpectatorPoseReceiver : MonoBehaviour
{
    [Header("Referencia directa (Link / Editor)")]
    [Tooltip("Asigna CenterEyeAnchor aqui para modo Air Link o cable. Si esta vacio usa UDP.")]
    public Transform headTarget;

    [Header("Red (solo modo standalone APK)")]
    public int listenPort = 47778;

    private UdpClient               _udp;
    private Thread                  _thread;
    private bool                    _cancelled = false;
    private ConcurrentQueue<string> _queue     = new ConcurrentQueue<string>();

    void OnEnable()
    {
        // No hacer nada en las Quest
        if (Application.platform == RuntimePlatform.Android)
        {
            enabled = false;
            return;
        }

        // Si hay headTarget no necesitamos UDP
        if (headTarget != null)
        {
            Debug.Log("[SpectatorPoseReceiver] Modo Link: siguiendo " + headTarget.name);
            return;
        }

        // Modo UDP (APK standalone)
        _cancelled = false;
        _udp = new UdpClient(listenPort);
        _thread = new Thread(ListenThread);
        _thread.IsBackground = true;
        _thread.Start();
        Debug.Log("[SpectatorPoseReceiver] Modo UDP: escuchando en puerto " + listenPort);
    }

    private void ListenThread()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        while (!_cancelled)
        {
            try
            {
                byte[] bytes = _udp.Receive(ref ep);
                string json  = Encoding.UTF8.GetString(bytes);
                _queue.Enqueue(json);
            }
            catch (System.Exception e)
            {
                if (!_cancelled)
                    Debug.LogWarning("[SpectatorPoseReceiver] " + e.Message);
            }
        }
    }

    void Update()
    {
        // Modo Link: seguir directamente el headTarget
        if (headTarget != null)
        {
            transform.position = headTarget.position;
            transform.rotation = headTarget.rotation;
            return;
        }

        // Modo UDP: procesar mensajes de red
        while (_queue.TryDequeue(out string json))
        {
            PoseData data = JsonUtility.FromJson<PoseData>(json);
            if (data == null) continue;
            transform.position = new Vector3(data.px, data.py, data.pz);
            transform.rotation = new Quaternion(data.rx, data.ry, data.rz, data.rw);
        }
    }

    void OnDisable()
    {
        _cancelled = true;
        _udp?.Close();
        _thread?.Join(500);
    }
}
