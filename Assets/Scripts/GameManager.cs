using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;


public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Configuracion")]
    public float matchDuration = 120f;
    public GameObject collectiblePrefab;
    public Transform[] spawnPoints;
    public float spawnInterval = 4f;
    public int maxCollectibles = 5;

    [Header("Anti-solapamiento de spawn")]
    public float spawnRadius = 1.5f;   
    public int maxSpawnAttempts = 10;  

    [Header("Spawn de jugadores")]
    public Transform[] playerSpawnPoints;

    [Header("Respawn por caida")]
    public float fallThresholdY = -5f; 
    public float respawnCheckInterval = 0.5f;

    [Header("Audio")]
    public AudioSource victorySound;
    public AudioSource defeatSound;
    public AudioSource levelMusic;

    [Header("Cielo nocturno")]
    public Color skyColor = new Color(0.02f, 0.02f, 0.06f);   // azul casi negro
    public Color groundColor = new Color(0.01f, 0.01f, 0.02f);
    public float moonIntensity = 0.25f;
    public int starCount = 800;
    public float starFieldRadius = 400f;
    public float starMinSize = 1.5f;
    public float starMaxSize = 3.5f;

    public NetworkVariable<float> timeRemaining = new NetworkVariable<float>(0f);
    public NetworkVariable<bool> gameOver = new NetworkVariable<bool>(false);

    [HideInInspector] public int collectibleCount = 0;

    private bool resultSoundPlayed = false;
    private int nextSpawnIndex = 0; 
    private System.Collections.Generic.Dictionary<ulong, Vector3> assignedSpawn = new System.Collections.Generic.Dictionary<ulong, Vector3>();

    private void Awake()
    {
        Instance = this;
        SetupNightSky(); 
    }


    private void SetupNightSky()
    {
        Material skyMat = new Material(Shader.Find("Skybox/Procedural"));
        skyMat.SetFloat("_AtmosphereThickness", 0.4f);
        skyMat.SetFloat("_SunSize", 0.02f);
        skyMat.SetColor("_SkyTint", skyColor);
        skyMat.SetColor("_GroundColor", groundColor);
        skyMat.SetFloat("_Exposure", 0.5f);
        RenderSettings.skybox = skyMat;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = skyColor;

        Light sun = FindFirstObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.intensity = moonIntensity;
            sun.color = new Color(0.7f, 0.75f, 0.9f); // tinte azulado de luna
        }

        CreateStarField();
    }

    private void CreateStarField()
    {
        GameObject starsGO = new GameObject("StarField");
        ParticleSystem ps = starsGO.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.startLifetime = Mathf.Infinity;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(starMinSize, starMaxSize);
        main.startColor = Color.white;
        main.maxParticles = starCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = false; 

        var shape = ps.shape;
        shape.enabled = false; 

        var renderer = starsGO.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.SetColor("_Color", Color.white);

        var emitParams = new ParticleSystem.EmitParams();
        for (int i = 0; i < starCount; i++)
        {
            Vector3 dir = Random.onUnitSphere; // punto aleatorio en una esfera
            emitParams.position = dir * starFieldRadius;
            emitParams.startSize = Random.Range(starMinSize, starMaxSize);
            ps.Emit(emitParams, 1);
        }
    }

    public override void OnNetworkSpawn()
    {
        PlayLevelMusic(); 

        if (!IsServer) return;

        timeRemaining.Value = matchDuration;
        gameOver.Value = false;
        resultSoundPlayed = false;
        nextSpawnIndex = 0;
        assignedSpawn.Clear();
        StartCoroutine(CountdownRoutine());
        StartCoroutine(SpawnRoutine());

        ResetAllPlayers(); 
        StartCoroutine(FallRespawnRoutine());

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }


    private void PlayLevelMusic()
    {
        if (levelMusic == null) return;

        levelMusic.loop = true;
        levelMusic.Stop();  
        levelMusic.Play();   
    }


    [ServerRpc(RequireOwnership = false)]
    public void RequestRestartServerRpc()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }


    private void AssignSpawn(ulong clientId, PlayerData data)
    {
        int index = nextSpawnIndex % playerSpawnPoints.Length;
        nextSpawnIndex++;

        Vector3 spawnPos = playerSpawnPoints[index].position;
        assignedSpawn[clientId] = spawnPos;

        if (data != null) data.playerIndex.Value = index;

        RepositionPlayerClientRpc(spawnPos,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
    }


    private void ResetAllPlayers()
    {
        if (playerSpawnPoints.Length == 0) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList.OrderBy(c => c.ClientId))
        {
            if (client.PlayerObject == null) continue;

            PlayerData data = client.PlayerObject.GetComponent<PlayerData>();
            if (data != null)
            {
                data.score.Value = 0;
                data.hasObject.Value = false;
            }

            AssignSpawn(client.ClientId, data);
        }
    }

    
    private IEnumerator FallRespawnRoutine()
    {
        while (!gameOver.Value)
        {
            yield return new WaitForSeconds(respawnCheckInterval);

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if (client.PlayerObject.transform.position.y >= fallThresholdY) continue;

                if (!assignedSpawn.TryGetValue(client.ClientId, out Vector3 spawnPos))
                {
                    if (playerSpawnPoints.Length == 0) continue;
                    spawnPos = playerSpawnPoints[0].position;
                }

                PlayerData data = client.PlayerObject.GetComponent<PlayerData>();
                if (data != null) data.hasObject.Value = false; // pierde el item cargado al caer

                RepositionPlayerClientRpc(spawnPos,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } } });
            }
        }
    }

    [ClientRpc]
    private void RepositionPlayerClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
    {
        var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (playerObj != null) playerObj.transform.position = position;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    void Update()
    {

        if (gameOver.Value && !resultSoundPlayed)
        {
            resultSoundPlayed = true;
            PlayLocalResultSound();
        }
    }

    private void PlayLocalResultSound()
    {
        if (NetworkManager.Singleton == null) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(localId, out var localClient)) return;
        if (localClient.PlayerObject == null) return;

        PlayerData localData = localClient.PlayerObject.GetComponent<PlayerData>();
        if (localData == null) return;

        int bestScore = -1;
        int winnersAtBest = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerData data = client.PlayerObject?.GetComponent<PlayerData>();
            if (data == null) continue;

            if (data.score.Value > bestScore) { bestScore = data.score.Value; winnersAtBest = 1; }
            else if (data.score.Value == bestScore) winnersAtBest++;
        }

        bool isWinner = localData.score.Value == bestScore && winnersAtBest == 1;
        AudioSource clip = isWinner ? victorySound : defeatSound;
        if (clip != null) clip.Play();
    }

    private void OnClientConnected(ulong clientId)
    {
        StartCoroutine(SpawnPlayerDelayed(clientId));
    }

    private IEnumerator SpawnPlayerDelayed(ulong clientId)
    {
        if (playerSpawnPoints.Length == 0) yield break;

        NetworkClient client = null;
        float timeout = 3f;
        while (timeout > 0f)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out client) && client.PlayerObject != null)
                break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (client == null || client.PlayerObject == null) yield break;

        PlayerData data = client.PlayerObject.GetComponent<PlayerData>();
        AssignSpawn(clientId, data);
    }

    private IEnumerator CountdownRoutine()
    {
        while (timeRemaining.Value > 0f)
        {
            yield return null;
            timeRemaining.Value -= Time.deltaTime;
        }

        timeRemaining.Value = 0f;
        gameOver.Value = true;
    }

    private IEnumerator SpawnRoutine()
    {
        while (!gameOver.Value)
        {
            if (collectibleCount < maxCollectibles && spawnPoints.Length > 0)
            {
                SpawnCollectible();
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnCollectible()
    {
        if (TryGetFreeSpawnPosition(out Vector3 position))
        {
            GameObject obj = Instantiate(collectiblePrefab, position, Quaternion.identity);
            obj.GetComponent<NetworkObject>().Spawn();
        }
      
    }

    private bool TryGetFreeSpawnPosition(out Vector3 result)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Transform candidate = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (!IsPositionOccupied(candidate.position))
            {
                result = candidate.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    private bool IsPositionOccupied(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(position, spawnRadius);
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<Collectible>() != null)
                return true;
        }
        return false;
    }
}