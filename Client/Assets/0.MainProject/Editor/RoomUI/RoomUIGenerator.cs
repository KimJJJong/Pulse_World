#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmRPG.EditorTools
{
    public static class RoomUIGenerator
    {
        // ---- Paths ----
        const string RootDir = "Assets/0.MainProject/02_UI/RoomUI";
        const string PrefabDir = RootDir + "/Prefabs";

        const string RoomItemPrefabPath = PrefabDir + "/RoomListItem.prefab";
        const string MemberItemPrefabPath = PrefabDir + "/MemberItem.prefab";

        [MenuItem("Tools/UI/Generate Room UI")]
        public static void Generate()
        {
            EnsureFolders();

            EnsureEventSystem();

            // Canvas 찾거나 생성
            var canvas = FindOrCreateCanvas();

            // Prefabs 만들기
            var roomItemPrefab = CreateRoomListItemPrefab(RoomItemPrefabPath);
            var memberItemPrefab = CreateMemberItemPrefab(MemberItemPrefabPath);

            // 씬에 UI 루트 생성
            var root = CreateRoomUIRoot(canvas.transform);

            // 컨트롤러 붙이기 (사용자 프로젝트에 있는 타입)
            var controllerType = FindType("NetClient.Room.UI.RoomUiController");
            if (controllerType == null)
            {
                Debug.LogError("RoomUiController 타입을 찾지 못했어요. 네임스페이스/클래스명을 확인해줘: NetClient.Room.UI.RoomUiController");
                Selection.activeObject = root;
                return;
            }

            var controller = root.GetComponent(controllerType) ?? root.AddComponent(controllerType);

            // RoomUiRoot 붙이기
            var uiRootType = FindType("NetClient.Room.UI.RoomUiRoot");
            if (uiRootType != null)
            {
                if (root.GetComponent(uiRootType) == null)
                    root.AddComponent(uiRootType);
            }

            // 패널/요소 만들기
            var panelRoomList = CreatePanel(root.transform, "Panel_RoomList");
            var panelCreate = CreatePanel(root.transform, "Panel_CreateRoomModal");
            var panelWaiting = CreatePanel(root.transform, "Panel_WaitingRoom");

            panelCreate.SetActive(false);
            panelWaiting.SetActive(false);

            // --- RoomList UI ---
            var topBar = CreateHorizontal(panelRoomList.transform, "TopBar");
            var btnRefresh = CreateButton(topBar.transform, "Btn_Refresh", "Refresh");
            var btnCreate = CreateButton(topBar.transform, "Btn_Create", "Create");
            var btnClose = CreateButton(topBar.transform, "Btn_Close", "Close");

            var txtStatus = CreateText(panelRoomList.transform, "Txt_Status", "Status");

            var scrollRooms = CreateScroll(panelRoomList.transform, "Scroll_Rooms");
            var roomsContent = scrollRooms.content;
            EnsureVerticalLayout(roomsContent.gameObject);

            // --- Create Modal ---
            var modalTitle = CreateText(panelCreate.transform, "Txt_CreateTitle", "Create Room");

            var inputRoomId = CreateInput(panelCreate.transform, "Input_RoomId", "RoomId (optional)");
            var inputMapId = CreateInput(panelCreate.transform, "Input_MapId", "MapId");
            var inputMax = CreateInput(panelCreate.transform, "Input_MaxPlayers", "MaxPlayers");
            inputMax.textComponent.text = "4";

            var modalBtns = CreateHorizontal(panelCreate.transform, "ModalButtons");
            var btnCreateConfirm = CreateButton(modalBtns.transform, "Btn_CreateConfirm", "Create");
            var btnCreateCancel = CreateButton(modalBtns.transform, "Btn_CreateCancel", "Cancel");

            // --- WaitingRoom ---
            var wrTop = CreateHorizontal(panelWaiting.transform, "TopBar");
            var txtRoomTitle = CreateText(wrTop.transform, "Txt_RoomTitle", "Room");
            var btnLeave = CreateButton(wrTop.transform, "Btn_Leave", "Leave");

            var scrollMembers = CreateScroll(panelWaiting.transform, "Scroll_Members");
            var membersContent = scrollMembers.content;
            EnsureVerticalLayout(membersContent.gameObject);

            var wrBtns = CreateHorizontal(panelWaiting.transform, "Actions");
            var btnReady = CreateButton(wrBtns.transform, "Btn_Ready", "Ready");
            var btnStart = CreateButton(wrBtns.transform, "Btn_Start", "Start");

            var txtWarn = CreateText(panelWaiting.transform, "Txt_Warn", "");

            // --- Prefab references must be from Assets, not scene instances ---
            var roomItemViewType = FindType("NetClient.Room.UI.RoomListItemView");
            var memberItemViewType = FindType("NetClient.Room.UI.MemberItemView");

            // 자동 바인딩 (필드명 기준)
            SetField(controller, "panelRoomList", panelRoomList);
            SetField(controller, "panelCreateModal", panelCreate);
            SetField(controller, "panelWaitingRoom", panelWaiting);

            SetField(controller, "btnRefresh", btnRefresh.GetComponent<Button>());
            SetField(controller, "btnCreate", btnCreate.GetComponent<Button>());
            SetField(controller, "btnClose", btnClose.GetComponent<Button>());
            SetField(controller, "txtStatus", txtStatus);

            SetField(controller, "roomListContent", roomsContent);
            if (roomItemViewType != null)
            {
                var roomPrefabAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RoomItemPrefabPath);
                SetField(controller, "roomItemPrefab", roomPrefabAsset);
            }

            SetField(controller, "inputRoomId", inputRoomId);
            SetField(controller, "inputMapId", inputMapId);
            SetField(controller, "inputMaxPlayers", inputMax);

            SetField(controller, "btnCreateConfirm", btnCreateConfirm.GetComponent<Button>());
            SetField(controller, "btnCreateCancel", btnCreateCancel.GetComponent<Button>());

            SetField(controller, "txtRoomTitle", txtRoomTitle);
            SetField(controller, "btnLeave", btnLeave.GetComponent<Button>());
            SetField(controller, "btnReady", btnReady.GetComponent<Button>());
            SetField(controller, "btnStart", btnStart.GetComponent<Button>());
            SetField(controller, "txtWarn", txtWarn);

            SetField(controller, "memberListContent", membersContent);
            if (memberItemViewType != null)
            {
                var memberPrefabAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MemberItemPrefabPath);
                SetField(controller, "memberItemPrefab", memberPrefabAsset);
            }

            // 마무리
            Selection.activeObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Room UI 생성 완료! (RoomUIRoot + Prefabs + Controller wiring)");
        }

        // -----------------------
        // Prefab creation
        // -----------------------
        static UnityEngine.Object CreateRoomListItemPrefab(string path)
        {
            // 이미 있으면 그대로 사용
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing != null) return existing;

            var go = new GameObject("RoomListItem");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900, 80);

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            var txtRoom = CreateText(go.transform, "Txt_RoomName", "RoomName");
            var txtMap = CreateText(go.transform, "Txt_Map", "Map: map01");
            var txtPlayers = CreateText(go.transform, "Txt_Players", "Players: 0/4");
            var btnJoin = CreateButton(go.transform, "Btn_Join", "Join");

            // RoomListItemView 붙이기 (있으면)
            var viewType = FindType("NetClient.Room.UI.RoomListItemView");
            if (viewType != null)
            {
                var view = go.AddComponent(viewType);

                SetField(view, "txtRoomName", txtRoom);
                SetField(view, "txtMap", txtMap);
                SetField(view, "txtPlayers", txtPlayers);
                SetField(view, "btnJoin", btnJoin.GetComponent<Button>());
            }
            else
            {
                Debug.LogWarning("RoomListItemView 타입을 못 찾아서 프리팹에 View 연결은 생략했어.");
            }

            EnsureFolders();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        }

        static UnityEngine.Object CreateMemberItemPrefab(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing != null) return existing;

            var go = new GameObject("MemberItem");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900, 60);

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.05f);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var txtName = CreateText(go.transform, "Txt_Name", "name");
            var txtReady = CreateText(go.transform, "Txt_Ready", "NOT READY");

            var viewType = FindType("NetClient.Room.UI.MemberItemView");
            if (viewType != null)
            {
                var view = go.AddComponent(viewType);
                SetField(view, "txtName", txtName);
                SetField(view, "txtReady", txtReady);
            }
            else
            {
                Debug.LogWarning("MemberItemView 타입을 못 찾아서 프리팹에 View 연결은 생략했어.");
            }

            EnsureFolders();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        }

        // -----------------------
        // Scene UI creation
        // -----------------------
        static GameObject CreateRoomUIRoot(Transform canvas)
        {
            // 이미 있으면 재사용
            var existing = GameObject.Find("RoomUIRoot");
            if (existing != null) return existing;

            var root = new GameObject("RoomUIRoot");
            root.transform.SetParent(canvas, false);

            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 배경(선택)
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.35f);

            return root;
        }

        static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(60, 60);
            rt.offsetMax = new Vector2(-60, -60);

            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.25f);

            return go;
        }

        static GameObject CreateHorizontal(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 60);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            return go;
        }

        // ScrollView 기본 생성 (UGUI)
        static (GameObject root, RectTransform content) CreateScroll(Transform parent, string name)
        {
            var scrollGO = new GameObject(name);
            scrollGO.transform.SetParent(parent, false);
            var rt = scrollGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(12, 12);
            rt.offsetMax = new Vector2(-12, 260);
            rt.sizeDelta = new Vector2(0, 600);

            var img = scrollGO.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.03f);

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGO.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;

            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            viewport.AddComponent<Image>().color = new Color(1, 1, 1, 0.02f);

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewport.transform, false);
            var cRT = contentGO.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 1);
            cRT.anchorMax = new Vector2(1, 1);
            cRT.pivot = new Vector2(0.5f, 1);
            cRT.offsetMin = new Vector2(0, 0);
            cRT.offsetMax = new Vector2(0, 0);
            cRT.sizeDelta = new Vector2(0, 200);

            scroll.viewport = vpRT;
            scroll.content = cRT;

            return (scrollGO, cRT);
        }

        static void EnsureVerticalLayout(GameObject go)
        {
            var v = go.GetComponent<VerticalLayoutGroup>() ?? go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12);
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            var fitter = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        // -----------------------
        // UI elements
        // -----------------------
        static TMPTextShim CreateText(Transform parent, string name, string text)
        {
            // TextMeshPro가 있으면 TMP, 없으면 legacy Text
            var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType != null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(260, 40);

                var tmp = go.AddComponent(tmpType);
                tmpType.GetProperty("text")?.SetValue(tmp, text);

                return new TMPTextShim(go, tmp, isTmp: true);
            }
            else
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(260, 40);

                var t = go.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.text = text;
                t.color = Color.white;

                return new TMPTextShim(go, t, isTmp: false);
            }
        }

        static GameObject CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(150, 44);

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.15f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var txt = CreateText(go.transform, "Label", label);
            var txtRT = txt.GameObject.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            return go;
        }

        static InputField CreateInput(Transform parent, string name, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(520, 48);

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);

            var input = go.AddComponent<InputField>();

            // Text
            var text = CreateLegacyText(go.transform, "Text", "");
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            StretchRect(text.rectTransform);
            input.textComponent = text;

            // Placeholder
            var ph = CreateLegacyText(go.transform, "Placeholder", placeholder);
            ph.color = new Color(1, 1, 1, 0.5f);
            ph.alignment = TextAnchor.MiddleLeft;
            StretchRect(ph.rectTransform);
            input.placeholder = ph;

            return input;
        }


        static Text CreateLegacyText(Transform parent, string name, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(520, 48);

            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            return t;
        }

        static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 6);
            rt.offsetMax = new Vector2(-12, -6);
        }

        // -----------------------
        // Utilities
        // -----------------------
        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootDir))
                Directory.CreateDirectory(RootDir);
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                Directory.CreateDirectory(PrefabDir);

            AssetDatabase.Refresh();
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        static Canvas FindOrCreateCanvas()
        {
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas != null) return canvas;

            var go = new GameObject("Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == fullName);
        }

        static void SetField(object target, string fieldName, object value)
        {
            if (target == null || value == null) return;

            var t = target.GetType();

            // field
            var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                if (value is TMPTextShim shim)
                {
                    // TMP 또는 Text 어느 쪽이든 넣어주기
                    if (f.FieldType.FullName == "TMPro.TMP_Text" && shim.IsTmp) { f.SetValue(target, shim.Raw); return; }
                    if (f.FieldType == typeof(Text) && !shim.IsTmp) { f.SetValue(target, shim.Raw); return; }
                }
                else if (value is TMPInputShim inShim)
                {
                    if (f.FieldType == typeof(InputField)) { f.SetValue(target, inShim.Raw); return; }
                    // TMP_InputField은 여기서 지원 안 함(필요하면 확장)
                }
                else
                {
                    if (f.FieldType.IsAssignableFrom(value.GetType()))
                    {
                        f.SetValue(target, value);
                        return;
                    }
                    // prefab asset Object
                    if (value is UnityEngine.Object uo && typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                    {
                        f.SetValue(target, uo);
                        return;
                    }
                }
                return;
            }

            // property
            var p = t.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite)
            {
                if (p.PropertyType.IsAssignableFrom(value.GetType()))
                    p.SetValue(target, value);
            }
        }

        // -------- small shims (TMP optional) --------
        sealed class TMPTextShim
        {
            public GameObject GameObject { get; }
            public object Raw { get; }
            public bool IsTmp { get; }
            public TMPTextShim(GameObject go, object raw, bool isTmp) { GameObject = go; Raw = raw; IsTmp = isTmp; }
            public static implicit operator TMPTextShim(Text t) => new TMPTextShim(t.gameObject, t, false);
        }

        sealed class TMPInputShim
        {
            public GameObject GameObject { get; }
            public InputField Raw { get; }
            public TMPInputShim(GameObject go, InputField raw) { GameObject = go; Raw = raw; }
        }
    }
}
#endif
