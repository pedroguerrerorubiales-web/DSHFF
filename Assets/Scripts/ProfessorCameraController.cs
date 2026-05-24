using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Control de cámara libre para el profesor (nuevo Input System).
///
/// Controles:
///   Botón derecho + mover ratón  → rotar la cámara
///   Botón derecho + WASD         → moverse
///   Botón derecho + Q / E        → bajar / subir
///   Botón derecho + Shift        → más rápido
///   Rueda del ratón              → acercarse / alejarse
///
/// El botón izquierdo queda libre para dibujar con PCAnnotationSender.
/// </summary>
public class ProfessorCameraController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float fastMultiplier = 3f;

    [Header("Rotación")]
    public float rotationSpeed = 0.2f;

    [Header("Zoom (rueda)")]
    public float scrollSpeed = 0.05f;

    private float _yaw;
    private float _pitch;

    void Start()
    {
        _yaw   = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        // En las Quest no hay ratón → el script queda inactivo
        if (Mouse.current == null) return;

        // ── Zoom con rueda ────────────────────────────────────────────────────
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.001f)
            transform.position += transform.forward * scroll * scrollSpeed;

        // ── Rotar + moverse con botón derecho ─────────────────────────────────
        if (!Mouse.current.rightButton.isPressed) return;

        // Rotación con delta del ratón
        Vector2 delta = Mouse.current.delta.ReadValue();
        _yaw   += delta.x * rotationSpeed;
        _pitch -= delta.y * rotationSpeed;
        _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
        transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);

        // Movimiento con teclado
        var kb    = Keyboard.current;
        if (kb == null) return;

        float speed = moveSpeed * (kb.leftShiftKey.isPressed ? fastMultiplier : 1f);
        Vector3 dir = Vector3.zero;

        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dir += transform.forward;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dir -= transform.forward;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  dir -= transform.right;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += transform.right;
        if (kb.eKey.isPressed)                               dir += Vector3.up;
        if (kb.qKey.isPressed)                               dir -= Vector3.up;

        transform.position += dir * speed * Time.deltaTime;
    }
}
