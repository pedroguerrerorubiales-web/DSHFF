using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Collections;

public class XRVideoSender : MonoBehaviour
{
    [Header("Configuración de Red (Carril de Vuelta)")]
    [Tooltip("La IP del Ordenador B (el tuyo)")]
    public string targetIP = "192.168.1.10"; 
    [Tooltip("El puerto por donde escuchas el vídeo. No uses el 47777.")]
    public int targetPort = 47778; 

    [Header("Configuración de Vídeo")]
    public Camera vrCamera;
    [Range(10, 100)] public int jpgQuality = 50;
    public float fpsLimit = 15f; 
    public int resolutionWidth = 640;
    public int resolutionHeight = 480;

    private UdpClient _udpClient;
    private RenderTexture _renderTexture;
    private Texture2D _texture2D;
    private float _timeSinceLastFrame;

    void Start()
    {
        _udpClient = new UdpClient();
        
        // Creamos la "tela" donde pintaremos la captura
        _renderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 16);
        _texture2D = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        
        if (vrCamera == null) vrCamera = Camera.main;
    }

    void Update()
    {
        _timeSinceLastFrame += Time.deltaTime;
        float frameInterval = 1f / fpsLimit;

        // Si ya ha pasado el tiempo necesario, sacamos una nueva captura
        if (_timeSinceLastFrame >= frameInterval)
        {
            StartCoroutine(CaptureAndSendFrame());
            _timeSinceLastFrame = 0f;
        }
    }

    private IEnumerator CaptureAndSendFrame()
    {
        // Esperamos al final del frame para no interrumpir el renderizado de la gafa
        yield return new WaitForEndOfFrame();

        // 1. Redirigimos la cámara para que pinte en nuestra textura interna
        RenderTexture currentRT = vrCamera.targetTexture;
        vrCamera.targetTexture = _renderTexture;
        vrCamera.Render();

        // 2. Leemos los píxeles de esa textura
        RenderTexture.active = _renderTexture;
        _texture2D.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
        _texture2D.Apply();

        // 3. Restauramos la cámara de inmediato para que el operario siga viendo su mundo
        vrCamera.targetTexture = currentRT;
        RenderTexture.active = null;

        // 4. Comprimimos a formato JPG
        byte[] jpgData = _texture2D.EncodeToJPG(jpgQuality);
        
        // UDP tiene un límite físico de tamaño por paquete (~65KB). 
        // Con 640x480 y calidad 50, suele pesar unos 30KB y caber perfectamente.
        if (jpgData.Length < 65000) 
        {
            _udpClient.SendAsync(jpgData, jpgData.Length, targetIP, targetPort);
        }
        else
        {
            Debug.LogWarning($"[XRVideoSender] Fotograma demasiado pesado ({jpgData.Length} bytes). Baja la calidad JPG o la resolución.");
        }
    }

    void OnDisable()
    {
        _udpClient?.Close();
    }
}
