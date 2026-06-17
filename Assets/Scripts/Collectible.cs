using UnityEngine;
using Unity.Netcode;


public class Collectible : NetworkBehaviour
{
    [Header("Audio")]
    public AudioSource pickupSound;

    [Header("Visual")]
    public Color itemColor = Color.magenta;
    public float lightIntensity = 2.5f;
    public float lightRange = 3f;

    public override void OnNetworkSpawn()
    {
        if (IsServer) GameManager.Instance.collectibleCount++;
        SetupVisual(); 
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) GameManager.Instance.collectibleCount--;
    }

  
    private void SetupVisual()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            rend.material.color = itemColor;
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", itemColor * 1.5f);
        }

        GameObject lightGO = new GameObject("ItemLight");
        lightGO.transform.SetParent(transform, false);
        Light l = lightGO.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = itemColor;
        l.intensity = lightIntensity;
        l.range = lightRange;
    }

    private void OnTriggerEnter(UnityEngine.Collider other)
    {
        if (!IsServer) return; 

      
        PlayerData player = other.GetComponentInParent<PlayerData>();
        if (player == null) return;
        if (player.hasObject.Value) return; 

        player.hasObject.Value = true;
        PlayPickupSoundClientRpc();
        NetworkObject.Despawn();
    }

    [ClientRpc]
    private void PlayPickupSoundClientRpc()
    {
        if (pickupSound == null || pickupSound.clip == null) return;

 
        GameObject soundGO = new GameObject("PickupSoundOneShot");
        soundGO.transform.position = transform.position;

        AudioSource src = soundGO.AddComponent<AudioSource>();
        src.clip = pickupSound.clip;
        src.volume = pickupSound.volume;
        src.pitch = pickupSound.pitch;
        src.spatialBlend = pickupSound.spatialBlend;
        src.outputAudioMixerGroup = pickupSound.outputAudioMixerGroup;
        src.Play();

        Destroy(soundGO, src.clip.length / Mathf.Max(src.pitch, 0.01f));
    }
}