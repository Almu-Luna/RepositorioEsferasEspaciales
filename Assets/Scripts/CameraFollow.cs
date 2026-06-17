using UnityEngine;
using Unity.Netcode;

// Va en la Main Camera de la escena Game.
// Sigue al jugador local, con zoom (rueda del mouse) y
// rotación orbital instantánea según el movimiento del mouse en X.
public class CameraFollow : MonoBehaviour
{
    [Header("Distancia y altura")]
    public float height = 6f;
    public float distance = 6f;
    public float minDistance = 3f;
    public float maxDistance = 15f;

    [Header("Velocidades")]
    public float zoomSpeed = 4f;
    public float rotationSpeed = 150f;

    private Transform target;
    private float yaw;

    public float Yaw => yaw;

    void LateUpdate()
    {
        if (target == null)
        {
            if (NetworkManager.Singleton == null) return;

            var localClient = NetworkManager.Singleton.LocalClient;
            if (localClient == null || localClient.PlayerObject == null) return;

            target = localClient.PlayerObject.transform;
        }

        distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        yaw += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;

        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 dir = rot * new Vector3(0f, 0f, -1f);

        transform.position = target.position + dir * distance + Vector3.up * height;
        transform.LookAt(target.position + Vector3.up * (height * 0.3f));
    }
}