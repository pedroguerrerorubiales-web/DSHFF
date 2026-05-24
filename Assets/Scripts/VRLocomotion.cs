using UnityEngine;

/// <summary>
/// Locomotion para Quest con CharacterController (solución fiable).
///
/// SETUP:
///  1. Crea un GameObject vacío "VRPlayer" en la raíz de la escena.
///  2. Añade este script a "VRPlayer".
///  3. Añade también un componente "Character Controller" a "VRPlayer"
///     (Add Component → Character Controller). Deja los valores por defecto.
///  4. Mete [BuildingBlock] Camera Rig como hijo de "VRPlayer".
///  5. Asigna el CenterEyeAnchor en el campo headTransform.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class VRLocomotion : MonoBehaviour
{
    [Header("Referencia a la cabeza (CenterEyeAnchor)")]
    public Transform headTransform;

    [Header("Movimiento")]
    public float moveSpeed = 2f;

    [Header("Giro (snap rotation)")]
    public float snapAngle = 45f;

    private CharacterController _cc;
    private bool _snapReady = true;
    private float _gravity = -9.81f;
    private float _verticalVelocity = 0f;

    void Start()
    {
        _cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    void HandleMovement()
    {
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        // Gravedad
        if (_cc.isGrounded)
            _verticalVelocity = -1f;
        else
            _verticalVelocity += _gravity * Time.deltaTime;

        Vector3 moveDir = Vector3.up * _verticalVelocity;

        if (stick.magnitude > 0.15f && headTransform != null)
        {
            Vector3 forward = headTransform.forward; forward.y = 0f; forward.Normalize();
            Vector3 right   = headTransform.right;   right.y   = 0f; right.Normalize();
            moveDir += (forward * stick.y + right * stick.x) * moveSpeed;
        }

        _cc.Move(moveDir * Time.deltaTime);
    }

    void HandleRotation()
    {
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        if (Mathf.Abs(stick.x) > 0.5f && _snapReady)
        {
            transform.Rotate(0f, snapAngle * Mathf.Sign(stick.x), 0f);
            _snapReady = false;
        }

        if (Mathf.Abs(stick.x) < 0.25f)
            _snapReady = true;
    }
}
