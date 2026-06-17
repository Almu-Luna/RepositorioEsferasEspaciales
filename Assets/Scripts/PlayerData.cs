using UnityEngine;
using Unity.Netcode;

public class PlayerData : NetworkBehaviour
{
    public NetworkVariable<int> score = new NetworkVariable<int>(0);
    public NetworkVariable<bool> hasObject = new NetworkVariable<bool>(false);
    public NetworkVariable<int> playerIndex = new NetworkVariable<int>(-1); 

    public Color player1Color = new Color(0.25f, 0.55f, 1f);  
    public Color player2Color = new Color(1f, 0.35f, 0.35f);  

    [Header("Luz del player")]
    public float lightIntensity = 3f;
    public float lightRange = 4f;

    [Header("Item cargado (visual)")]
    public float headHeight = 1.8f;
    public float carriedScale = 0.3f;
    public Color carriedItemColor = Color.magenta;
    public float carriedLightIntensity = 2f;
    public float carriedLightRange = 2.5f;

    private GameObject carriedSphere;
    private Light playerLight;

    public override void OnNetworkSpawn()
    {
        hasObject.OnValueChanged += OnHasObjectChanged;
        playerIndex.OnValueChanged += OnPlayerIndexChanged;

        SetupPlayerVisual();
        if (hasObject.Value) SpawnCarriedSphere();
    }

    public override void OnNetworkDespawn()
    {
        hasObject.OnValueChanged -= OnHasObjectChanged;
        playerIndex.OnValueChanged -= OnPlayerIndexChanged;
    }

    private void OnPlayerIndexChanged(int previous, int current)
    {
        ApplyColor();
    }

    private void SetupPlayerVisual()
    {
        if (playerLight == null)
        {
            GameObject lightGO = new GameObject("PlayerLight");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = Vector3.up * 0.5f;
            playerLight = lightGO.AddComponent<Light>();
            playerLight.type = LightType.Point;
            playerLight.range = lightRange;
            playerLight.intensity = lightIntensity;
        }
        ApplyColor();
    }

    private void ApplyColor()
    {
        Color c = playerIndex.Value == 1 ? player2Color : player1Color;

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            rend.material.color = c;
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", c * 0.8f);
        }

        if (playerLight != null) playerLight.color = c;
    }

    private void OnHasObjectChanged(bool previous, bool current)
    {
        if (current) SpawnCarriedSphere();
        else DestroyCarriedSphere();
    }

    private void SpawnCarriedSphere()
    {
        if (carriedSphere != null) return;

        carriedSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        carriedSphere.name = "CarriedItem";
        Destroy(carriedSphere.GetComponent<Collider>()); // solo visual, sin colision

        carriedSphere.transform.SetParent(transform, false);
        carriedSphere.transform.localPosition = Vector3.up * headHeight;
        carriedSphere.transform.localScale = Vector3.one * carriedScale;

        Renderer rend = carriedSphere.GetComponent<Renderer>();
        rend.material.color = carriedItemColor;
        rend.material.EnableKeyword("_EMISSION");
        rend.material.SetColor("_EmissionColor", carriedItemColor * 1.5f);

        GameObject lightGO = new GameObject("CarriedLight");
        lightGO.transform.SetParent(carriedSphere.transform, false);
        Light l = lightGO.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = carriedItemColor;
        l.intensity = carriedLightIntensity;
        l.range = carriedLightRange;
    }

    private void DestroyCarriedSphere()
    {
        if (carriedSphere == null) return;
        Destroy(carriedSphere);
        carriedSphere = null;
    }
}