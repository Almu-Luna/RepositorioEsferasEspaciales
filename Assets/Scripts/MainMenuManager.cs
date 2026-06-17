using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;


public class MainMenuManager : MonoBehaviour
{
    [Header("Textos - Panel principal")]
    public string gameTitle = "RECOLECCI�N";
    public string studentName = "Nombre y Apellido";
    public string hostButtonText = "SER HOST";
    public string joinButtonText = "UNIRSE (CLIENTE)";
    public string quitButtonText = "SALIR";

    [Header("Textos - Panel de conexi�n")]
    public string joinPanelTitle = "UNIRSE A PARTIDA";
    public string ipLabelText = "IP DEL HOST";
    public string defaultIP = "127.0.0.1";
    public string connectButtonText = "CONECTAR";
    public string backButtonText = "VOLVER";

    [Header("Textos - Panel de instrucciones")]
    public string instructionsTitle = "CONTROLES";
    [TextArea]
    public string instructionsText =
        "TAB para CORRER\nRUEDITA para MANEJAR ZOOM\nRECOLECTA ESFERAS DE LUZ PARA GANAR!";

    [Header("Conexi�n")]
    public string gameSceneName = "Game";
    public ushort port = 7777;

    [Header("Tama�os de fuente")]
    public int titleFontSize = 30;
    public int subtitleFontSize = 18;
    public int inputFontSize = 22;
    public int buttonFontSize = 22;

    [Header("Tama�os de elementos (ancho, alto)")]
    public Vector2 panelSize = new Vector2(380, 320);
    public Vector2 textSize = new Vector2(340, 40);
    public Vector2 inputSize = new Vector2(260, 40);
    public Vector2 buttonSize = new Vector2(260, 50);

    private InputField ipInputField;
    private GameObject mainPanel;
    private GameObject joinPanel;

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("MenuCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        BuildMainPanel(canvasGO.transform);
        BuildJoinPanel(canvasGO.transform);
        BuildInstructionsPanel(canvasGO.transform);

        joinPanel.SetActive(false);
    }


    void BuildInstructionsPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, panelSize);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(panelSize.x + 30, 0); // a la derecha del panel principal

        CreateText(panel.transform, instructionsTitle, titleFontSize, textSize, new Vector2(0, 120));
        CreateText(panel.transform, instructionsText, subtitleFontSize, new Vector2(panelSize.x - 40, 180), new Vector2(0, 10));
    }

    void BuildMainPanel(Transform parent)
    {
        mainPanel = CreatePanel(parent, panelSize);

        CreateText(mainPanel.transform, gameTitle, titleFontSize, textSize, new Vector2(0, 120));
        CreateText(mainPanel.transform, studentName, subtitleFontSize, textSize, new Vector2(0, 85));

        CreateButton(mainPanel.transform, hostButtonText, buttonFontSize, buttonSize, new Vector2(0, 15),
            new Color(0.2f, 0.5f, 0.9f), OnHostClick);
        CreateButton(mainPanel.transform, joinButtonText, buttonFontSize, buttonSize, new Vector2(0, -50),
            new Color(0.25f, 0.7f, 0.4f), () => SwitchPanel(mainPanel, joinPanel));
        CreateButton(mainPanel.transform, quitButtonText, buttonFontSize, buttonSize, new Vector2(0, -115),
            new Color(0.5f, 0.5f, 0.5f), OnQuitClick);
    }

    void BuildJoinPanel(Transform parent)
    {
        joinPanel = CreatePanel(parent, panelSize);

        CreateText(joinPanel.transform, joinPanelTitle, titleFontSize, textSize, new Vector2(0, 120));
        CreateText(joinPanel.transform, ipLabelText, subtitleFontSize, textSize, new Vector2(0, 70));

        ipInputField = CreateInputField(joinPanel.transform, defaultIP, inputFontSize, inputSize, new Vector2(0, 25));

        CreateButton(joinPanel.transform, connectButtonText, buttonFontSize, buttonSize, new Vector2(0, -45),
            new Color(0.25f, 0.7f, 0.4f), OnJoinClick);
        CreateButton(joinPanel.transform, backButtonText, buttonFontSize, buttonSize, new Vector2(0, -110),
            new Color(0.5f, 0.5f, 0.5f), () => SwitchPanel(joinPanel, mainPanel));
    }

    void SwitchPanel(GameObject from, GameObject to)
    {
        from.SetActive(false);
        to.SetActive(true);
    }

    void OnQuitClick()
    {
        Application.Quit();
    }

    public void OnHostClick()
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("0.0.0.0", port);

        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            Debug.LogError("No se pudo iniciar el Host (�el puerto " + port + " ya est� en uso por otra instancia?).");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public void OnJoinClick()
    {
        string ip = string.IsNullOrEmpty(ipInputField.text) ? defaultIP : ipInputField.text;

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, port);

        NetworkManager.Singleton.StartClient();
    }

   

    GameObject CreatePanel(Transform parent, Vector2 size)
    {
        GameObject go = new GameObject("Panel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    Text CreateText(Transform parent, string content, int fontSize, Vector2 size, Vector2 anchoredPos)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;   // nunca esconde el texto, aunque no entre en el alto fijo
        txt.resizeTextForBestFit = true;                    // si el contenido es largo, achica la fuente sola
        txt.resizeTextMinSize = 8;
        txt.resizeTextMaxSize = fontSize;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return txt;
    }

    Button CreateButton(Transform parent, string label, int fontSize, Vector2 size, Vector2 anchoredPos, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        Text txt = CreateText(go.transform, label, fontSize, size, Vector2.zero);
        RectTransform textRT = txt.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.anchoredPosition = Vector2.zero;

        return btn;
    }

    InputField CreateInputField(Transform parent, string defaultValue, int fontSize, Vector2 size, Vector2 anchoredPos)
    {
        GameObject go = new GameObject("InputField", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = Color.white;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        InputField input = go.AddComponent<InputField>();

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        Text txt = textGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = Color.black;
        txt.alignment = TextAnchor.MiddleLeft;
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10, 0);
        textRT.offsetMax = new Vector2(-10, 0);

        input.textComponent = txt;
        input.text = defaultValue;

        return input;
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