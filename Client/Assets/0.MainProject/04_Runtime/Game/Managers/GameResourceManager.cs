using UnityEngine;
using RhythmRPG.Data;
using System.Collections.Generic;
using System.IO;

namespace RhythmRPG.Managers
{
    /// <summary>
    /// ID 기반 통합 리소스 매니저.
    /// Resources.Load 래퍼 역할을 하며, 캐싱 기능을 제공합니다.
    /// </summary>
    public class GameResourceManager : MonoBehaviour
    {
        private static GameResourceManager _instance;
        public static GameResourceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameResourceManager");
                    _instance = go.AddComponent<GameResourceManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private readonly Dictionary<int, GameObject> _prefabCache = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Sprite> _iconCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// ID에 해당하는 프리팹을 로드합니다.
        /// 경로: Resources/{GetResourceFolderPath(id)}/{id}
        /// 실패 시 null 반환.
        /// </summary>
        public GameObject GetPrefab(int id)
        {
            if (_prefabCache.TryGetValue(id, out var cached))
                return cached;

            string folder = EntityIdDefine.GetResourceFolderPath(id);
            string path = "";

            // 0. Try ItemDataManager for custom path (e.g. from JSON/CSV)
            if (Client.Content.Item.ItemDataManager.Instance != null)
            {
                var tmpl = Client.Content.Item.ItemDataManager.Instance.GetEquipment(id);
                if (tmpl != null && !string.IsNullOrEmpty(tmpl.model_path))
                {
                    path = tmpl.model_path;
                }
            }

            GameObject prefab = null;

            // 1. Try Custom Path if exists
            if (!string.IsNullOrEmpty(path))
            {
                foreach (var candidate in BuildResourcePathCandidates(path))
                {
                    prefab = Resources.Load<GameObject>(candidate);
                    if (prefab != null) break;
                }
            }

            // 2. Try Standard Conventions if custom path failed or empty
            if (prefab == null)
            {
                // Try "Entity_{id}"
                prefab = Resources.Load<GameObject>($"{folder}/Entity_{id}");
            }

            if (prefab == null)
            {
                // Try just "{id}"
                prefab = Resources.Load<GameObject>($"{folder}/{id}");
            }

            if (prefab != null)
            {
                _prefabCache[id] = prefab;
                return prefab;
            }
            
            Debug.LogWarning($"[GameResourceManager] Prefab not found for ID: {id} (model_path='{path}') in {folder}");
            return null;
        }

        private static IEnumerable<string> BuildResourcePathCandidates(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                yield break;

            // Resources.Load path must be project-relative under a Resources folder,
            // without extension and using '/' separators.
            string normalized = rawPath.Replace('\\', '/').Trim();

            if (normalized.StartsWith("Assets/Resources/"))
                normalized = normalized.Substring("Assets/Resources/".Length);
            else if (normalized.StartsWith("Resources/"))
                normalized = normalized.Substring("Resources/".Length);

            normalized = normalized.TrimStart('/');
            normalized = Path.ChangeExtension(normalized, null).Replace('\\', '/');

            if (!string.IsNullOrEmpty(normalized))
                yield return normalized;

            // Backward-compat typo fallback: Armor <-> Armon
            if (normalized.Contains("/Armor/"))
                yield return normalized.Replace("/Armor/", "/Armon/");
            if (normalized.Contains("/Armon/"))
                yield return normalized.Replace("/Armon/", "/Armor/");
        }

        /// <summary>
        /// ID에 해당하는 아이콘(Sprite)을 로드합니다.
        /// 경로: Resources/{GetResourceFolderPath(id)}/Icons/{id}
        /// 실패 시 null 반환.
        /// </summary>
        public Sprite GetIcon(int id)
        {
            if (_iconCache.TryGetValue(id, out var cached))
                return cached;

            if (Client.Content.Item.ItemDataManager.Instance != null)
            {
                var tmpl = Client.Content.Item.ItemDataManager.Instance.GetEquipment(id);
                if (tmpl != null && !string.IsNullOrEmpty(tmpl.icon_path))
                {
                    foreach (var candidate in BuildResourcePathCandidates(tmpl.icon_path))
                    {
                        var customSprite = Resources.Load<Sprite>(candidate);
                        if (customSprite != null)
                        {
                            _iconCache[id] = customSprite;
                            return customSprite;
                        }
                    }
                }
            }
            
            string folder = EntityIdDefine.GetResourceFolderPath(id);
            // 아이콘은 보통 별도 폴더나 같은 폴더 내에 있을 수 있음. 
            // 여기서는 "Icons" 하위 폴더를 우선 가정하거나, 접미사 "_Icon"을 가정.
            
            // Try 1: {folder}/Icons/{id}
            string path1 = $"{folder}/Icons/{id}";
            var sprite = Resources.Load<Sprite>(path1);

            if (sprite == null)
            {
                // Try 2: {folder}/{id}_Icon
                string path2 = $"{folder}/{id}_Icon";
                sprite = Resources.Load<Sprite>(path2);
            }

            if (sprite != null)
            {
                _iconCache[id] = sprite;
                return sprite;
            }

            return null; // 아이콘 없음
        }

        public void ClearCache()
        {
            _prefabCache.Clear();
            _iconCache.Clear();
            Resources.UnloadUnusedAssets();
        }
    }
}
