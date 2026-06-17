using UnityEngine;
using Unity.Netcode;

// Va en el prefab del jugador, junto a un NetworkObject y un NetworkTransform
// (configurado como Owner Authoritative para que cada cliente mueva su propio jugador).
public class PlayerController : NetworkBehaviour
{
    public float speed = 5f;
    public float sprintMultiplier = 1.8f; // velocidad mientras se mantiene TAB

    void Awake()
    {
        // Evita que el jugador se vuelque al chocar o moverse.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Direccion relativa a hacia donde mira la camara (solo en el plano horizontal)
        float yaw = 0f;
        if (Camera.main != null)
        {
            CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
            if (cam != null) yaw = cam.Yaw;
        }
        Quaternion camRotation = Quaternion.Euler(0f, yaw, 0f);

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? speed * sprintMultiplier : speed;

        Vector3 move = camRotation * new Vector3(h, 0f, v) * currentSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);
    }
}