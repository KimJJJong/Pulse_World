#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class RoomUiAutoBinder
{
    [MenuItem("RhythmRPG/UI/Auto Bind RoomUiController (Safe)")]
    public static void AutoBindSafe() => AutoBind(force: false);

    [MenuItem("RhythmRPG/UI/Auto Bind RoomUiController (Force Rebind)")]
    public static void AutoBindForce() => AutoBind(force: true);

    static void AutoBind(bool force)
    {
        var controller = FindMonoByFullName("NetClient.Room.UI.RoomUiController");
        if (controller == null)
        {
            Debug.LogError("RoomUiController를 씬에서 찾지 못했어요.");
            return;
        }

        var so = new SerializedObject(controller);

        // -----------------------------
        // apiProvider 자동 바인드 (있으면)
        // -----------------------------
        // 같은 오브젝트/부모/씬 전체에서 ApiClientProvider를 찾아서 꽂음
        var apiProvider = FindMonoInScope(controller, "NetClient.Room.UI.ApiClientProvider");
        SetProp(so, "apiProvider", apiProvider, force);

        // -----------------------------
        // Panels
        // -----------------------------
        SetProp(so, "panelRoomList", FindGO(controller.transform, "Panel_RoomList"), force);
        SetProp(so, "panelCreateModal", FindGO(controller.transform, "Panel_CreateRoomModal"), force);
        SetProp(so, "panelWaitingRoom", FindGO(controller.transform, "Panel_WaitingRoom"), force);

        // -----------------------------
        // RoomList TopBar
        // -----------------------------
        SetProp(so, "btnRefresh", FindComp<Button>(controller.transform, "Btn_Refresh"), force);
        SetProp(so, "btnCreate", FindComp<Button>(controller.transform, "Btn_Create"), force);
        SetProp(so, "btnClose", FindComp<Button>(controller.transform, "Btn_Close"), force);
        SetTextLike(so, "txtStatus", controller.transform, "Txt_Status", force);

        // -----------------------------
        // RoomList Scroll content
        // -----------------------------
        SetProp(so, "roomListContent",
            FindTransform(controller.transform, "Scroll_Rooms/Viewport/Content")
            ?? FindTransform(controller.transform, "Scroll_Rooms/Content"),
            force);

        // roomItemPrefab (프리팹 자동 검색)
        SetPrefabByTypeOrName(
            so,
            fieldName: "roomItemPrefab",
            requiredComponentTypeFullName: "NetClient.Room.UI.RoomListItemView",
            fallbackNameContains: "RoomListItem",
            force: force
        );

        // -----------------------------
        // Create Modal
        // -----------------------------
        SetInputLike(so, "inputRoomId", controller.transform, "Input_RoomId", force);
        SetInputLike(so, "inputMapId", controller.transform, "Input_MapId", force);
        SetInputLike(so, "inputMaxPlayers", controller.transform, "Input_MaxPlayers", force);
        SetProp(so, "btnCreateConfirm", FindComp<Button>(controller.transform, "Btn_CreateConfirm"), force);
        SetProp(so, "btnCreateCancel", FindComp<Button>(controller.transform, "Btn_CreateCancel"), force);

        // -----------------------------
        // WaitingRoom
        // -----------------------------
        SetTextLike(so, "txtRoomTitle", controller.transform, "Txt_RoomTitle", force);
        SetProp(so, "btnLeave", FindComp<Button>(controller.transform, "Btn_Leave"), force);
        SetProp(so, "btnReady", FindComp<Button>(controller.transform, "Btn_Ready"), force);
        SetProp(so, "btnStart", FindComp<Button>(controller.transform, "Btn_Start"), force);
        SetTextLike(so, "txtWarn", controller.transform, "Txt_Warn", force);

        SetProp(so, "memberListContent",
            FindTransform(controller.transform, "Scroll_Members/Viewport/Content")
            ?? FindTransform(controller.transform, "Scroll_Members/Content"),
            force);

        // memberItemPrefab (프리팹 자동 검색)
        SetPrefabByTypeOrName(
            so,
            fieldName: "memberItemPrefab",
            requiredComponentTypeFullName: "NetClient.Room.UI.MemberItemView",
            fallbackNameContains: "MemberItem",
            force: force
        );

        // apply
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        Debug.Log(force
            ? "RoomUiController FORCE 자동 바인드 완료!"
            : "RoomUiController SAFE 자동 바인드 완료! (기존 값은 유지)");

        Selection.activeObject = controller.gameObject;
    }

    // ======================================================================
    // Prefab auto-search
    // ======================================================================

    static void SetPrefabByTypeOrName(
        SerializedObject so,
        string fieldName,
        string requiredComponentTypeFullName,
        string fallbackNameContains,
        bool force)
    {
        var p = so.FindProperty(fieldName);
        if (p == null) return;
        if (p.propertyType != SerializedPropertyType.ObjectReference) return;
        if (!force && p.objectReferenceValue != null) return;

        // 1) 타입이 존재하면 "그 컴포넌트가 붙은 프리팹"을 우선 검색
        var requiredType = FindType(requiredComponentTypeFullName);

        string[] prefabGuids;

        if (requiredType != null)
        {
            // 프리팹 자체에 해당 컴포넌트가 붙어있는 애 찾기 (정확)
            prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // prefab root에 컴포넌트 있거나 자식에 있으면 OK
                if (HasComponentInPrefab(prefab, requiredType))
                {
                    p.objectReferenceValue = prefab;
                    return;
                }
            }
        }

        // 2) fallback: 이름에 키워드 포함된 prefab 찾기
        prefabGuids = AssetDatabase.FindAssets($"t:Prefab {fallbackNameContains}");
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            p.objectReferenceValue = prefab;
            return;
        }

        // 3) 마지막 fallback: 생성기 기본 경로 (있으면)
        var defaultPath = fieldName == "roomItemPrefab"
            ? "Assets/0.MainProject/02_UI/RoomUI/Prefabs/RoomListItem.prefab"
            : "Assets/0.MainProject/02_UI/RoomUI/Prefabs/MemberItem.prefab";

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath);
        if (asset != null)
        {
            p.objectReferenceValue = asset;
            return;
        }

        Debug.LogWarning($"[{fieldName}] 프리팹 자동 탐색 실패. 직접 인스펙터에서 지정해줘. (force={force})");
    }

    static bool HasComponentInPrefab(GameObject prefab, Type componentType)
    {
        try
        {
            // prefab root + children 포함 검색
            var comps = prefab.GetComponentsInChildren(componentType, true);
            return comps != null && comps.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    // ======================================================================
    // Find helpers
    // ======================================================================

    static MonoBehaviour FindMonoByFullName(string fullName)
    {
        foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            if (mb.GetType().FullName == fullName)
                return mb;
        }
        return null;
    }

    static MonoBehaviour FindMonoInScope(MonoBehaviour owner, string fullName)
    {
        // 1) 같은 오브젝트
        var same = owner.GetComponents<MonoBehaviour>()
            .FirstOrDefault(mb => mb != null && mb.GetType().FullName == fullName);
        if (same != null) return same;

        // 2) 부모에서
        var inParents = owner.GetComponentsInParent<MonoBehaviour>(true)
            .FirstOrDefault(mb => mb != null && mb.GetType().FullName == fullName);
        if (inParents != null) return inParents;

        // 3) 자식에서
        var inChildren = owner.GetComponentsInChildren<MonoBehaviour>(true)
            .FirstOrDefault(mb => mb != null && mb.GetType().FullName == fullName);
        if (inChildren != null) return inChildren;

        // 4) 씬 전체에서 (마지막)
        return FindMonoByFullName(fullName);
    }

    static Transform FindTransform(Transform root, string path)
    {
        if (!root) return null;

        // 1) exact path
        var t = root.Find(path);
        if (t) return t;

        // 2) fallback by last segment
        var last = path.Contains("/") ? path.Split('/').Last() : path;
        return FindByName(root, last);
    }

    static GameObject FindGO(Transform root, string nameOrPath)
    {
        var t = FindTransform(root, nameOrPath);
        return t ? t.gameObject : null;
    }

    static T FindComp<T>(Transform root, string nameOrPath) where T : Component
    {
        var t = FindTransform(root, nameOrPath);
        return t ? t.GetComponent<T>() : null;
    }

    static Transform FindByName(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = null;
            try { t = asm.GetType(fullName, throwOnError: false); }
            catch { }
            if (t != null) return t;
        }
        return null;
    }

    // ======================================================================
    // Serialized binding
    // ======================================================================

    static void SetProp(SerializedObject so, string fieldName, UnityEngine.Object value, bool force)
    {
        if (value == null) return;
        var p = so.FindProperty(fieldName);
        if (p == null) return;
        if (p.propertyType != SerializedPropertyType.ObjectReference) return;

        if (!force && p.objectReferenceValue != null) return;
        p.objectReferenceValue = value;
    }

    static void SetTextLike(SerializedObject so, string fieldName, Transform root, string nameOrPath, bool force)
    {
        var p = so.FindProperty(fieldName);
        if (p == null) return;
        if (p.propertyType != SerializedPropertyType.ObjectReference) return;
        if (!force && p.objectReferenceValue != null) return;

        var t = FindTransform(root, nameOrPath);
        if (!t) return;

        // 1) TMP_Text
        var tmpTextType = FindType("TMPro.TMP_Text");
        if (tmpTextType != null)
        {
            var tmp = t.GetComponent(tmpTextType);
            if (tmp != null)
            {
                p.objectReferenceValue = tmp as UnityEngine.Object;
                return;
            }
        }

        // 2) legacy Text
        var legacy = t.GetComponent<Text>();
        if (legacy != null)
            p.objectReferenceValue = legacy;
    }

    static void SetInputLike(SerializedObject so, string fieldName, Transform root, string nameOrPath, bool force)
    {
        var p = so.FindProperty(fieldName);
        if (p == null) return;
        if (p.propertyType != SerializedPropertyType.ObjectReference) return;
        if (!force && p.objectReferenceValue != null) return;

        var t = FindTransform(root, nameOrPath);
        if (!t) return;

        // 1) TMP_InputField
        var tmpInputType = FindType("TMPro.TMP_InputField");
        if (tmpInputType != null)
        {
            var tmp = t.GetComponent(tmpInputType);
            if (tmp != null)
            {
                p.objectReferenceValue = tmp as UnityEngine.Object;
                return;
            }
        }

        // 2) legacy InputField
        var legacy = t.GetComponent<InputField>();
        if (legacy != null)
            p.objectReferenceValue = legacy;
    }
}
#endif
