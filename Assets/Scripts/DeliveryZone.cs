using UnityEngine;
using Unity.Netcode;


public class DeliveryZone : NetworkBehaviour
{
    [Header("Audio")]
    public AudioSource deliverySound;

    [Header("Visual")]
    public Color zoneColor = Color.yellow;
    public float lightIntensity = 3f;
    public float lightRange = 6f;

    void Start()
    {
        SetupVisual(); 
    }

    
    private void SetupVisual()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            rend.material.color = zoneColor;
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", zoneColor * 1.2f);
        }

        GameObject lightGO = new GameObject("DeliveryZoneLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = Vector3.up * 1f;
        Light l = lightGO.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = zoneColor;
        l.intensity = lightIntensity;
        l.range = lightRange;
    }

    private void OnTriggerEnter(UnityEngine.Collider other)
    {
        if (!IsServer) return;

        PlayerData player = other.GetComponentInParent<PlayerData>();
        if (player == null) return;
        if (!player.hasObject.Value) return;

        player.hasObject.Value = false;
        player.score.Value += 1;
        PlayDeliverySoundClientRpc();
    }

    [ClientRpc]
    private void PlayDeliverySoundClientRpc()
    {
        if (deliverySound != null) deliverySound.Play();
    }
}