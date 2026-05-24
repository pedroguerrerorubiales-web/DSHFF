using UnityEngine;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Corre en las Quest (Android).
/// Envia la posicion y rotacion del CenterEyeAnchor al PC por UDP.
/// </summary>
public class QuestPoseSender : MonoBehaviour
{
    [Header("Referencia a la cabeza")]
    public Transform headTransform; // CenterEyeAnchor

    [Header("Red - IP del PC del profesor")]
    public string pcIP   = "192.168.1.150";
    public int    pcPort = 47778;

    [Header("Frecuencia de envio")]
    [Tooltip("Cuantas veces por segundo se envia la pose (30 = 30fps)")]
    public int sendRate = 30;

    private UdpClient _udp;
    private float     _interval;
    private float     _timer;
    private bool      _ready = false;

    void Awake()
    {
        // Solo activo en las Quest
        if (Application.platform != RuntimePlatform.Android)
        {
            enabled = false;
            return;
        }
        Debug.Log("[QuestPoseSender] Awake en Android OK");
    }

    void Start()
    {
        if (!enabled) return;

        // Si no esta asignado desde el inspector, buscar por nombre
        if (headTransform == null)
        {
            GameObject go = GameObject.Find("CenterEyeAnchor");
            if (go != null)
            {
                headTransform = go.transform;
                Debug.Log("[QuestPoseSender] CenterEyeAnchor encontrado por nombre");
            }
            else
            {
                Debug.LogError("[QuestPoseSender] No se encontro CenterEyeAnchor.");
                return;
            }
        }

        _interval = 1f / Mathf.Max(1, sendRate);

        try
        {
            _udp = new UdpClient();
            _ready = true;
            Debug.Log($"[QuestPoseSender] Listo. Enviando pose a {pcIP}:{pcPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[QuestPoseSender] Error al crear UdpClient: " + e.Message);
        }
    }

    void Update()
    {
        if (!_ready || headTransform == null) return;

        _timer += Time.deltaTime;
        if (_timer < _interval) return;
        _timer = 0f;

        PoseData data = new PoseData
        {
            px = headTransform.position.x,
            py = headTransform.position.y,
            pz = headTransform.position.z,
            rx = headTransform.rotation.x,
            ry = headTransform.rotation.y,
            rz = headTransform.rotation.z,
            rw = headTransform.rotation.w
        };

        string json  = JsonUtility.ToJson(data);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        try { _udp.Send(bytes, bytes.Length, pcIP, pcPort); }
        catch (System.Exception e) { Debug.LogWarning("[QuestPoseSender] Error al enviar: " + e.Message); }
    }

    void OnDisable() { _udp?.Close(); }
}
