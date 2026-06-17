using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("Colores por jugador")]
    public Color[] playerColors = new Color[]
    {
        new Color(0.3f, 0.6f, 1f),
        new Color(1f, 0.35f, 0.35f)
    };

    [Header("Textos")]
    public string timerLabel = "TIEMPO RESTANTE";
    public string scoresLabel = "PUNTAJES";
    public string playerPrefix = "Jugador";
    public string winnerPrefix = "¡GANADOR!";
    public string tiePrefix = "¡EMPATE!";
    public string restartText = "JUGAR DE NUEVO";
    public string menuText = "VOLVER AL MENÚ";

    private Text timerText;
    private Text[] scoreTexts = new Text[2];   
    private Text winnerText;
    private GameObject gameOverPanel;
    private bool gameOverShown;

    void Start() => BuildUI();

    // ─── BUILD ────────────────────────────────────────────────────────────────

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("HUDCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        GameObject timerPanel = MakePanel(canvasGO.transform,
            new Vector2(340, 130), new Vector2(0, 1), new Vector2(20, -20));

        
        MakeText(timerPanel.transform, timerLabel, 20,
            new Vector2(0, 0.52f), new Vector2(1, 1),
            new Vector2(6, 0), new Vector2(-6, 0),
            TextAnchor.MiddleCenter);

        
        timerText = MakeText(timerPanel.transform, "00:00", 52,
            new Vector2(0, 0), new Vector2(1, 0.50f),
            new Vector2(6, 4), new Vector2(-6, 0),
            TextAnchor.MiddleCenter);

        
        GameObject scoresPanel = MakePanel(canvasGO.transform,
            new Vector2(320, 190), new Vector2(1, 1), new Vector2(-20, -20));

     
        MakeText(scoresPanel.transform, scoresLabel, 20,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(6, -42), new Vector2(-6, 0),
            TextAnchor.MiddleCenter);

        // Jugador 1 — franja fija
        scoreTexts[0] = MakeText(scoresPanel.transform, $"<color=#{Hex(0)}>{playerPrefix} 1</color>: -", 34,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(12, -106), new Vector2(-12, -44),
            TextAnchor.MiddleLeft);
        scoreTexts[0].supportRichText = true;

        // Jugador 2 — franja fija
        scoreTexts[1] = MakeText(scoresPanel.transform, $"<color=#{Hex(1)}>{playerPrefix} 2</color>: -", 34,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(12, -168), new Vector2(-12, -106),
            TextAnchor.MiddleLeft);
        scoreTexts[1].supportRichText = true;

        // ── Game Over ─────────────────────────────────────────────────────────
        gameOverPanel = MakePanel(canvasGO.transform,
            new Vector2(520, 360), new Vector2(0.5f, 0.5f), Vector2.zero);
        gameOverPanel.SetActive(false);

        winnerText = MakeText(gameOverPanel.transform, "", 40,
            new Vector2(0, 0.5f), new Vector2(1, 1),
            new Vector2(0, 0), new Vector2(0, 0),
            TextAnchor.MiddleCenter);
        winnerText.supportRichText = true;

        MakeButton(gameOverPanel.transform, restartText, 24,
            new Vector2(320, 56), new Vector2(0, -60),
            new Color(0.2f, 0.65f, 0.35f), OnRestartClick);

        MakeButton(gameOverPanel.transform, menuText, 24,
            new Vector2(320, 56), new Vector2(0, -130),
            new Color(0.45f, 0.45f, 0.45f), OnMainMenuClick);
    }

    // ─── UPDATE ───────────────────────────────────────────────────────────────

    void Update()
    {
        if (GameManager.Instance == null) return;

        float t = GameManager.Instance.timeRemaining.Value;
        timerText.text = $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";

        UpdateScores();

        if (GameManager.Instance.gameOver.Value && !gameOverShown)
            ShowGameOver();
    }

    void UpdateScores()
    {
        var players = GetConnectedPlayers();
        for (int slot = 0; slot < 2; slot++)
        {
            if (slot >= players.Count) continue;
            scoreTexts[slot].text = $"<color=#{Hex(slot)}>{playerPrefix} {slot + 1}</color>: {players[slot].score.Value}";
        }
        
    }

    void ShowGameOver()
    {
        gameOverShown = true;
        gameOverPanel.SetActive(true);

        var players = GetConnectedPlayers();
        int bestScore = -1;
        int bestIdx = 0;
        int tiedCount = 0;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].score.Value > bestScore)
            {
                bestScore = players[i].score.Value;
                bestIdx = i;
                tiedCount = 1;
            }
            else if (players[i].score.Value == bestScore)
            {
                tiedCount++;
            }
        }

        winnerText.text = tiedCount > 1
            ? tiePrefix
            : $"{winnerPrefix}\n<color=#{Hex(bestIdx)}>{playerPrefix} {bestIdx + 1}</color>";
    }


    System.Collections.Generic.List<PlayerData> GetConnectedPlayers()
    {
        var result = new System.Collections.Generic.List<PlayerData>();
        if (NetworkManager.Singleton == null) return result;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerData data = client.PlayerObject?.GetComponent<PlayerData>();
            if (data != null) result.Add(data);
        }
        return result;
    }

    string Hex(int i) => ColorUtility.ToHtmlStringRGB(playerColors[i % playerColors.Length]);

    // ─── CALLBACKS ────────────────────────────────────────────────────────────

    public void OnRestartClick()
    {
        GameManager.Instance.RequestRestartServerRpc();
    }

    public void OnMainMenuClick()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("MainMenu");
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────────

    GameObject MakePanel(Transform parent, Vector2 size, Vector2 anchor, Vector2 pos)
    {
        GameObject go = new GameObject("Panel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return go;
    }

 
    Text MakeText(Transform parent, string content, int fontSize,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        TextAnchor alignment)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.alignment = alignment;
        txt.color = Color.white;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return txt;
    }

    void MakeButton(Transform parent, string label, int fontSize,
        Vector2 size, Vector2 pos, Color color,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(label + "Btn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.AddComponent<Button>().onClick.AddListener(onClick);
        MakeText(go.transform, label, fontSize,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            TextAnchor.MiddleCenter);
    }


    private Sprite roundedSprite;
    private Sprite GetRoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;

        int size = 64;
        int radius = 20;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, IsInsideRoundedRect(x, y, size, radius) ? Color.white : Color.clear);
            }
        }
        tex.Apply();

        roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return roundedSprite;
    }

    private bool IsInsideRoundedRect(int x, int y, int size, int radius)
    {
        int max = size - 1;
        if (x < radius && y < radius)
            return Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) <= radius;
        if (x < radius && y > max - radius)
            return Vector2.Distance(new Vector2(x, y), new Vector2(radius, max - radius)) <= radius;
        if (x > max - radius && y < radius)
            return Vector2.Distance(new Vector2(x, y), new Vector2(max - radius, radius)) <= radius;
        if (x > max - radius && y > max - radius)
            return Vector2.Distance(new Vector2(x, y), new Vector2(max - radius, max - radius)) <= radius;
        return true;
    }
}