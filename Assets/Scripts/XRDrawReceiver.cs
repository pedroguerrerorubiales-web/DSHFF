using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

public class XRDrawReceiver : MonoBehaviour
{
    [Header("Referencias de Dibujo")]
    public Transform qrAnchorReal; // El objeto que se ancla al código QR físico
    public GameObject linePrefab;  // El MISMO prefab que tienes tú en el PC

    [Header("Red")]
    public int listenPort = 47777; // Debe ser el mismo puerto que envíe el PC

    private UdpClient _udpListener;
    private CancellationTokenSource _cancellationTokenSource;
    
    // Cola segura para pasar los JSON del hilo de red al hilo principal de Unity
    private ConcurrentQueue<string> _jsonQueue = new ConcurrentQueue<string>();

    void OnEnable()
    {
        _udpListener = new UdpClient(listenPort);
        _cancellationTokenSource = new CancellationTokenSource();
        ListenForDrawingsAsync(_cancellationTokenSource.Token);
        Debug.Log("[Equipo A] Escuchando trazos en el puerto " + listenPort);
    }

    private async void ListenForDrawingsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _udpListener.ReceiveAsync();
                string jsonMessage = Encoding.UTF8.GetString(result.Buffer);
                
                // En lugar de usar un Dispatcher externo, metemos el JSON en la cola
                _jsonQueue.Enqueue(jsonMessage);
            }
            catch (System.Exception e)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogWarning("Error al recibir trazo: " + e.Message);
            }
        }
    }

    // Unity ejecuta el Update siempre en el hilo principal
    void Update()
    {
        // Revisamos si hay trazos nuevos esperando en la cola para dibujarlos
        while (_jsonQueue.TryDequeue(out string jsonMessage))
        {
            ProcessIncomingJSON(jsonMessage);
        }
    }

    void ProcessIncomingJSON(string jsonMessage)
    {
        // 1. Desempaquetar el texto usando la clase que hemos incluido abajo
        StrokeData data = JsonUtility.FromJson<StrokeData>(jsonMessage);
        if (data == null || data.points == null) return;

        // 2. Generar el holograma
        GameObject newLine = Instantiate(linePrefab, qrAnchorReal);
        LineRenderer lr = newLine.GetComponent<LineRenderer>();
        
        lr.useWorldSpace = false; 
        lr.startColor = data.color;
        lr.endColor = data.color;
        lr.startWidth = data.width;
        lr.endWidth = data.width;

        lr.positionCount = data.points.Length;
        lr.SetPositions(data.points);
    }

    void OnDisable()
    {
        _cancellationTokenSource?.Cancel();
        _udpListener?.Close();
    }
}

// Incluimos la clase StrokeData aquí mismo para que el Equipo A no tenga que crear otro archivo
[System.Serializable]
public class StrokeData 
{
    public Vector3[] points;
    public Color color;
    public float width;
    public string id;
}