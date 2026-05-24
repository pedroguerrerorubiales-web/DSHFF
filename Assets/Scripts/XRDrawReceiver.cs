using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;

public class XRDrawReceiver : MonoBehaviour
{
    [Header("Referencias de Dibujo")]
    public Transform qrAnchorReal;
    public GameObject linePrefab;

    [Header("Red")]
    public int listenPort = 47777;

    [Header("Vida de las líneas")]
    [Tooltip("Segundos que se muestra la línea antes de empezar a desvanecerse")]
    public float displayTime = 20f;
    [Tooltip("Segundos que tarda en desvanecerse hasta desaparecer")]
    public float fadeDuration = 2f;

    private UdpClient _udpListener;
    private CancellationTokenSource _cancellationTokenSource;
    private ConcurrentQueue<string> _jsonQueue = new ConcurrentQueue<string>();

    void OnEnable()
    {
        _udpListener = new UdpClient(listenPort);
        _cancellationTokenSource = new CancellationTokenSource();
        ListenForDrawingsAsync(_cancellationTokenSource.Token);
        Debug.Log("[XRDrawReceiver] Escuchando trazos en el puerto " + listenPort);
    }

    private async void ListenForDrawingsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _udpListener.ReceiveAsync();
                string jsonMessage = Encoding.UTF8.GetString(result.Buffer);
                _jsonQueue.Enqueue(jsonMessage);
            }
            catch (System.Exception e)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogWarning("Error al recibir trazo: " + e.Message);
            }
        }
    }

    void Update()
    {
        while (_jsonQueue.TryDequeue(out string jsonMessage))
            ProcessIncomingJSON(jsonMessage);
    }

    void ProcessIncomingJSON(string jsonMessage)
    {
        StrokeData data = JsonUtility.FromJson<StrokeData>(jsonMessage);
        if (data == null || data.points == null) return;

        GameObject newLine = Instantiate(linePrefab, qrAnchorReal);
        LineRenderer lr = newLine.GetComponent<LineRenderer>();

        lr.useWorldSpace = false;
        lr.startColor = data.color;
        lr.endColor   = data.color;
        lr.startWidth = data.width;
        lr.endWidth   = data.width;
        lr.positionCount = data.points.Length;
        lr.SetPositions(data.points);

        // Iniciar temporizador de desvanecimiento
        StartCoroutine(FadeAndDestroy(newLine, lr, data.color));
    }

    private IEnumerator FadeAndDestroy(GameObject lineObj, LineRenderer lr, Color originalColor)
    {
        // Esperar el tiempo de visualización
        yield return new WaitForSeconds(displayTime);

        // Desvanecer poco a poco
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            if (lineObj == null) yield break;

            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

            Color c = originalColor;
            c.a = alpha;
            lr.startColor = c;
            lr.endColor   = c;

            yield return null;
        }

        if (lineObj != null) Destroy(lineObj);
    }

    void OnDisable()
    {
        _cancellationTokenSource?.Cancel();
        _udpListener?.Close();
    }
}
// StrokeData está definida en Assets/Scripts/StrokeData.cs
