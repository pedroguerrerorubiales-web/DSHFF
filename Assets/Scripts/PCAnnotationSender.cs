using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// LADO PC – PROFESOR
/// ==================
/// Dibuja trazos con el ratón sobre el modelo 3D del laboratorio.
///
/// MODO LOCAL (Oculus Link por cable):
///   - Activa "Local Mode" en el Inspector.
///   - Conecta las Quest por USB → habilita Meta/Oculus Link en las gafas → Play.
///   - Los trazos aparecen directamente en la escena, que las gafas renderizan
///     vía Link. Sin red, sin builds aparte.
///
/// MODO RED (dos PCs, WiFi):
///   - Desactiva "Local Mode", pon la IP de las Quest en questIP.
///   - Las Quest deben tener la escena de alumno en ejecución (XRDrawReceiver).
///
/// SETUP EN UNITY:
///  1. Crea un GameObject vacío "PCAnnotationSender" y añade este script.
///  2. virtualCamera → la cámara del PC con la que el profesor ve el lab
///                     (añade una Camera aparte; NO uses la cámara XR/VR).
///  3. labAnchor     → un GameObject vacío en la raíz del modelo del laboratorio.
///  4. linePrefab    → Assets/Prefabs/LinePrefab.
/// </summary>
public class PCAnnotationSender : MonoBehaviour
{
    // ── Referencias ────────────────────────────────────────────────────────────
    [Header("Cámara y Anclaje del Laboratorio")]
    [Tooltip("Cámara del PC que visualiza el laboratorio 3D (NO la cámara XR)")]
    public Camera virtualCamera;

    [Tooltip("Transform de anclaje en la raíz del modelo del laboratorio")]
    public Transform labAnchor;

    [Header("Prefab de Línea")]
    [Tooltip("Prefab con componente LineRenderer (Assets/Prefabs/LinePrefab)")]
    public GameObject linePrefab;

    // ── Modo ───────────────────────────────────────────────────────────────────
    [Header("Modo de Uso")]
    [Tooltip("TRUE  → Oculus Link por cable (sin red, los trazos van directo a la escena).\n" +
             "FALSE → Dos PCs por WiFi (envía UDP a la IP de las Quest).")]
    public bool localMode = true;

    // ── Red (solo si localMode = false) ───────────────────────────────────────
    [Header("Red  (solo si Local Mode = false)")]
    [Tooltip("IP de las Oculus Quest en la red local (p.ej. 192.168.1.X)")]
    public string questIP   = "192.168.1.100";
    [Tooltip("Puerto UDP – debe coincidir con 'listenPort' de XRDrawReceiver")]
    public int    questPort = 47777;

    // ── Dibujo ────────────────────────────────────────────────────────────────
    [Header("Opciones de Dibujo")]
    [Range(0.001f, 0.5f)]
    public float lineWidth = 0.05f;

    [Tooltip("Distancia a la que se dibuja en modo aire.")]
    public float airDrawDistance = 3f;

    [Tooltip("Mantén esta tecla pulsada para dibujar en el aire.\n" +
             "Sin la tecla → solo dibuja sobre geometría física.")]
    public KeyCode airDrawKey = KeyCode.LeftAlt;

    // Estado privado
    private Color         _color       = Color.red;
    private LineRenderer  _currentLine;
    private List<Vector3> _points      = new List<Vector3>();
    private UdpClient     _udp;

    // ── Unity lifecycle ────────────────────────────────────────────────────────
    void Start()
    {
        if (!localMode) _udp = new UdpClient();
        if (virtualCamera == null) virtualCamera = Camera.main;
    }

    void Update()
    {
        // Mouse.current es null en las Quest (sin ratón) → el script queda inactivo en la APK
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)                  StartStroke();
        if (Mouse.current.leftButton.isPressed && _currentLine != null)    ContinueStroke();
        if (Mouse.current.leftButton.wasReleasedThisFrame)                 EndAndSendStroke();
    }

    // ── Lógica de dibujo ───────────────────────────────────────────────────────
    void StartStroke()
    {
        if (linePrefab == null || labAnchor == null)
        {
            Debug.LogWarning("[PCAnnotationSender] Asigna linePrefab y labAnchor en el Inspector.");
            return;
        }

        GameObject go = Instantiate(linePrefab, labAnchor);
        _currentLine = go.GetComponent<LineRenderer>();
        _currentLine.useWorldSpace = false;
        _currentLine.startWidth    = lineWidth;
        _currentLine.endWidth      = lineWidth;
        _currentLine.startColor    = _color;
        _currentLine.endColor      = _color;

        _points.Clear();
        ContinueStroke(); // primer punto inmediato
    }

    void ContinueStroke()
    {
        Vector3 localPt = ScreenToLabLocal(Mouse.current.position.ReadValue());
        if (localPt == Vector3.zero) return;

        // Añadimos solo si nos hemos movido lo suficiente (evita puntos duplicados)
        if (_points.Count == 0 ||
            Vector3.Distance(_points[_points.Count - 1], localPt) > 0.003f)
        {
            _points.Add(localPt);
            _currentLine.positionCount = _points.Count;
            _currentLine.SetPosition(_points.Count - 1, localPt);
        }
    }

    /// <summary>
    /// Convierte la posición del ratón en pantalla a coordenadas locales del labAnchor.
    ///
    /// Modo superficie (por defecto):
    ///   Physics.Raycast sobre la geometría del laboratorio.
    ///   Si no hay colisión, no dibuja nada.
    ///
    /// Modo aire (mantener airDrawKey):
    ///   Plano virtual perpendicular a la cámara a airDrawDistance metros.
    ///   Ignora la geometría — dibuja siempre en el aire.
    /// </summary>
    Vector3 ScreenToLabLocal(Vector2 screenPos)
    {
        Ray ray = virtualCamera.ScreenPointToRay(screenPos);

        bool airMode = Keyboard.current != null && Keyboard.current[Key.LeftAlt].isPressed;

        if (airMode)
        {
            // ── MODO AIRE ────────────────────────────────────────────────────
            // Usamos la distancia al objeto más cercano bajo el cursor.
            // Si no hay ningún objeto, usamos airDrawDistance como fallback.
            // Así el trazo queda justo delante de lo que se está mirando.
            float depth = airDrawDistance;
            if (Physics.Raycast(ray, out RaycastHit nearestHit))
                depth = nearestHit.distance;

            Vector3 center = virtualCamera.transform.position
                             + virtualCamera.transform.forward * depth;
            Plane airPlane = new Plane(-virtualCamera.transform.forward, center);

            if (airPlane.Raycast(ray, out float dist))
                return labAnchor.InverseTransformPoint(ray.GetPoint(dist));
        }
        else
        {
            // ── MODO SUPERFICIE ──────────────────────────────────────────────
            if (Physics.Raycast(ray, out RaycastHit hit))
                return labAnchor.InverseTransformPoint(hit.point);
        }

        return Vector3.zero; // Sin colisión → no dibuja
    }

    void EndAndSendStroke()
    {
        if (_points.Count > 1 && !localMode)
        {
            // Modo red: serializar y enviar por UDP a las Quest
            StrokeData stroke = new StrokeData
            {
                points = _points.ToArray(),
                color  = _color,
                width  = lineWidth,
                id     = System.Guid.NewGuid().ToString()
            };

            string json  = JsonUtility.ToJson(stroke);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                _udp.Send(bytes, bytes.Length, questIP, questPort);
                Debug.Log($"[PCAnnotationSender] ✓ Trazo enviado → {questIP}:{questPort}  " +
                          $"({bytes.Length} bytes | {_points.Count} puntos)");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[PCAnnotationSender] Error de red: " + ex.Message);
            }
        }

        // En Local Mode los trazos ya están en la escena desde StartStroke/ContinueStroke,
        // así que no hace falta hacer nada más aquí.
        _currentLine = null;
        _points.Clear();
    }

    // ── Métodos públicos para botones UI ───────────────────────────────────────
    public void SetColorRed()             { _color = Color.red; }
    public void SetColorGreen()           { _color = Color.green; }
    public void SetColorBlue()            { _color = Color.blue; }
    public void SetLineWidth(float width) { lineWidth = width; }

    void OnDisable() { if (!localMode) _udp?.Close(); }
}
