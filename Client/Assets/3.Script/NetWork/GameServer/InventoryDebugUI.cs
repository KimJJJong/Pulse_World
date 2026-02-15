using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryDebugUI : MonoBehaviour
{
    public static InventoryDebugUI Instance { get; private set; }

    private bool _show = false;
    private Canvas _canvas;
    private GameObject _panelGo;
    private TextMeshProUGUI _contentParams;

    // References
    private InventoryManager Inv => InventoryManager.Instance;

    // Cheat Inputs
    private string _inputTid = "101";
    private string _inputAmt = "1";

    private void Awake()
    {
        Instance = this;
        CreateCanvasAndUI();
        SetVisible(false);
    }

    private void Start()
    {
        if (Inv != null)
        {
            Inv.OnInventoryUpdated += RefreshUI;
        }
    }

    private void OnDestroy()
    {
        if (Inv != null)
        {
            Inv.OnInventoryUpdated -= RefreshUI;
        }
    }



    public void SetVisible(bool show)
    {
        _show = show;
        if (_canvas != null) _canvas.enabled = show;
        if (show) RefreshUI();
    }

    private void RefreshUI()
    {
        if (!_show || _contentParams == null || Inv == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b><size=120%>Inventory (Toggle: I)</size></b>");
        sb.AppendLine($"<color=yellow>Items: {Inv.Items.Count}</color>");
        
        foreach (var item in Inv.Items)
        {
            sb.AppendLine($"- [{item.SlotIndex}] TID:{item.TemplateId} Amt:{item.Amount} (ID:{item.InstanceId})");
        }

        sb.AppendLine();
        sb.AppendLine($"<color=green>Equipments: {Inv.Equipments.Count}</color>");

        foreach (var equip in Inv.Equipments)
        {
            string status = equip.IsEquipped ? "<color=cyan>[E]</color>" : "[ ]";
            sb.AppendLine($"{status} [{equip.SlotIndex}] TID:{equip.TemplateId} +{equip.EnhancementLevel} (ID:{equip.InstanceId})");
        }

        sb.AppendLine();
        sb.AppendLine("<i>Press 'E' + InstanceID (in logic) to test equip... (UI button TODO)</i>");
        
        _contentParams.text = sb.ToString();
    }

    // Temporary Simple UI Construction
    private void CreateCanvasAndUI()
    {
        var canvasGo = new GameObject("InventoryDebugCanvas");
        canvasGo.layer = LayerMask.NameToLayer("UI");
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGo);

        var panelGo = new GameObject("InvPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var img = panelGo.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.8f);

        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.7f, 0.1f);
        rect.anchorMax = new Vector2(0.98f, 0.9f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _panelGo = panelGo;

        var textGo = new GameObject("ContentText");
        textGo.transform.SetParent(panelGo.transform, false);
        _contentParams = textGo.AddComponent<TextMeshProUGUI>();
        _contentParams.fontSize = 24;
        _contentParams.color = Color.white;
        _contentParams.alignment = TextAlignmentOptions.TopLeft;
        
        var textRect = _contentParams.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
    }

    private Vector2 _scrollPos = Vector2.zero;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            Debug.Log($"[InventoryDebugUI] I key pressed. Show: {!_show}");
            SetVisible(!_show);
            if (_show && Inv != null)
            {
                Inv.RequestRefresh();
            }
            else if (_show && Inv == null)
            {
                Debug.LogError("[InventoryDebugUI] InventoryManager is NULL");
            }
        }
    }

    private void OnGUI()
    {
        if (!_show) return;
        if (Inv == null)
        {
            GUILayout.Label("InventoryManager is NULL");
            return;
        }

        try
        {
            // Styles
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 20;
        btnStyle.fixedHeight = 40;
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 20;
        labelStyle.normal.textColor = Color.white;
        labelStyle.wordWrap = false;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 24;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.yellow;

        // Center-Right side of screen, larger area
        float width = 800;
        float height = Screen.height * 0.9f;
        float x = Screen.width * 0.5f - (width / 2);
        
        // Background Box
        GUI.Box(new Rect(x, 40, width, height), "");

        GUILayout.BeginArea(new Rect(x + 20, 60, width - 40, height - 40));
        
        // Title with UID
        string uidObj = SessionContext.Instance?.Uid ?? "NoAuth";
        GUILayout.Label($"Inventory Debug (UID: {uidObj})", titleStyle);
        GUILayout.Label($"Items: {Inv.Items.Count}, Equips: {Inv.Equipments.Count}", labelStyle);

        if (GUILayout.Button("Refresh Inventory", btnStyle))
        {
            Inv.RequestRefresh();
        }
        
        GUILayout.Space(20);
        
        // Scroll View Start
        _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Width(width - 40), GUILayout.Height(height - 250));

        // 1. Items Section
        GUILayout.Label("--- Items ---", titleStyle);
        if (Inv.Items.Count == 0) GUILayout.Label("No Items", labelStyle);
        foreach (var item in Inv.Items)
        {
             GUILayout.BeginVertical("box");
             GUILayout.BeginHorizontal();
             
             var tmpl = Client.Content.Item.ItemDataManager.Instance?.Get(item.TemplateId);
             string name = tmpl != null ? tmpl.name : "Unknown";
             string grade = tmpl != null ? tmpl.grade : "-";
             
             GUILayout.Label($"[ID:{item.InstanceId}] {name} ({grade}) x{item.Amount}", labelStyle);
             
             if (GUILayout.Button("X", btnStyle, GUILayout.Width(40)))
             {
                 Inv.RequestDestroyItem(item.InstanceId, item.Amount);
             }
             GUILayout.EndHorizontal();
             
             if (tmpl != null && !string.IsNullOrEmpty(tmpl.description))
                GUILayout.Label($"<size=18><color=#cccccc>{tmpl.description}</color></size>", labelStyle);
                
             GUILayout.EndVertical();
        }

        GUILayout.Space(20);

        // 2. Equipments Section
        GUILayout.Label("--- Equipments ---", titleStyle);
        if (Inv.Equipments.Count == 0) GUILayout.Label("No Equipments", labelStyle);
        
        foreach (var equip in Inv.Equipments)
        {
            GUILayout.BeginHorizontal("box");
            
            var tmpl = Client.Content.Item.ItemDataManager.Instance?.GetEquipment(equip.TemplateId);
            string name = tmpl != null ? tmpl.name : "Unknown";
            string grade = tmpl != null ? tmpl.grade : "-";
            string slot = tmpl != null ? tmpl.equip_slot : "-";
            
            string status = equip.IsEquipped ? "<color=green>[E]</color>" : "[ ]";
            string info = $"{status} [ID:{equip.InstanceId}] {name} ({grade}/{slot}) +{equip.EnhancementLevel}\n" +
                          $"    <size=80%>Stats: {equip.BaseStats}</size>\n" +
                          $"    <size=80%>Opts: {equip.RandomOptions}</size>";
            
            GUILayout.Label(info, labelStyle, GUILayout.Width(450));
            
            string btnText = equip.IsEquipped ? "Unequip" : "Equip";
            if (GUILayout.Button(btnText, btnStyle, GUILayout.Width(90), GUILayout.Height(60)))
            {
                Inv.RequestEquip(equip.InstanceId, !equip.IsEquipped);
            }
             if (GUILayout.Button("X", btnStyle, GUILayout.Width(40), GUILayout.Height(60)))
            {
                 Inv.RequestDestroyItem(equip.InstanceId, 1);
            }
            GUILayout.EndHorizontal();
        }

        
        
        GUILayout.Space(20);
        
        // 3. Cheat Section (Inside Scroll for safety)
        GUILayout.Label("--- Cheat: Add Item ---", titleStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("TID:", labelStyle, GUILayout.Width(50));
        _inputTid = GUILayout.TextField(_inputTid, 20, GUILayout.Width(100), GUILayout.Height(30));
        GUILayout.Label("Amt:", labelStyle, GUILayout.Width(50));
        _inputAmt = GUILayout.TextField(_inputAmt, 20, GUILayout.Width(100), GUILayout.Height(30));
        
        if (GUILayout.Button("Add", btnStyle, GUILayout.Width(100)))
        {
            if (int.TryParse(_inputTid, out int tid) && int.TryParse(_inputAmt, out int amt))
            {
                Inv.RequestCheat(1, tid, amt);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView(); // End Scroll Here
        
        GUILayout.EndArea();
        }
        catch (System.Exception ex)
        {
            GUILayout.Label($"<color=red>Error: {ex.GetType().Name}</color>");
            GUILayout.Label(ex.Message);
        }
    }
}
