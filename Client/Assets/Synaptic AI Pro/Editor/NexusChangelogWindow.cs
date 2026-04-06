using UnityEditor;
using UnityEngine;

namespace SynapticPro
{
    /// <summary>
    /// Changelog dialog shown on first import or version update
    /// </summary>
    [InitializeOnLoad]
    public class NexusChangelogWindow : EditorWindow
    {
        private const string CURRENT_VERSION = "1.2.3";
        private const string PREF_KEY_LAST_VERSION = "SynapticPro_LastShownVersion";
        private const string PREF_KEY_DONT_SHOW = "SynapticPro_DontShowChangelog";
        private const string PREF_KEY_LANGUAGE = "SynapticPro_ChangelogLanguage";

        private enum Language { English, Japanese }
        private static Language currentLanguage = Language.English;

        private static bool dontShowAgain = false;
        private Vector2 scrollPosition;

        static NexusChangelogWindow()
        {
            EditorApplication.delayCall += ShowOnStartupIfNeeded;
        }

        private static void ShowOnStartupIfNeeded()
        {
            // Don't show during play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Check if user disabled
            if (EditorPrefs.GetBool(PREF_KEY_DONT_SHOW, false))
                return;

            // Check if already shown for this version
            string lastVersion = EditorPrefs.GetString(PREF_KEY_LAST_VERSION, "");
            if (lastVersion == CURRENT_VERSION)
                return;

            // Show dialog
            ShowWindow();

            // Mark as shown
            EditorPrefs.SetString(PREF_KEY_LAST_VERSION, CURRENT_VERSION);
        }

        [MenuItem("Tools/Synaptic Pro/What's New", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<NexusChangelogWindow>(true, "Synaptic AI Pro - What's New", true);
            window.minSize = new Vector2(500, 450);
            window.maxSize = new Vector2(600, 650);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            dontShowAgain = EditorPrefs.GetBool(PREF_KEY_DONT_SHOW, false);
            currentLanguage = (Language)EditorPrefs.GetInt(PREF_KEY_LANGUAGE, 0);
        }

        private void OnGUI()
        {
            // Language selector (top right)
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(currentLanguage == Language.English ? "Language:" : "言語:", GUILayout.Width(60));
            var newLang = (Language)EditorGUILayout.Popup((int)currentLanguage, new string[] { "English", "日本語" }, GUILayout.Width(80));
            if (newLang != currentLanguage)
            {
                currentLanguage = newLang;
                EditorPrefs.SetInt(PREF_KEY_LANGUAGE, (int)currentLanguage);
            }
            EditorGUILayout.EndHorizontal();

            // Header
            GUILayout.Space(5);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label($"Synaptic AI Pro v{CURRENT_VERSION}", headerStyle);
            GUILayout.Space(5);

            var subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
            GUILayout.Label(L("What's New", "更新内容"), subtitleStyle);

            GUILayout.Space(15);

            // Changelog content
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawChangelogContent();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            // Don't show again toggle
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool newDontShow = EditorGUILayout.ToggleLeft(
                L("Don't show on startup", "起動時に表示しない"),
                dontShowAgain, GUILayout.Width(180));
            if (newDontShow != dontShowAgain)
            {
                dontShowAgain = newDontShow;
                EditorPrefs.SetBool(PREF_KEY_DONT_SHOW, dontShowAgain);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(L("Open Setup", "セットアップを開く"), GUILayout.Width(120), GUILayout.Height(30)))
            {
                NexusMCPSetupWindow.ShowWindow();
                Close();
            }

            GUILayout.Space(10);

            if (GUILayout.Button(L("Close", "閉じる"), GUILayout.Width(80), GUILayout.Height(30)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        // Localization helper
        private string L(string en, string ja)
        {
            return currentLanguage == Language.Japanese ? ja : en;
        }

        private void DrawChangelogContent()
        {
            var sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

            var itemStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true
            };

            // v1.2.3
            GUILayout.Label(L("v1.2.3 - HTTP API Fix", "v1.2.3 - HTTP API修正"), sectionStyle);
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(L("<b>Fixed</b>", "<b>修正</b>"), itemStyle);
            GUILayout.Label(L("• HTTP API: All tools now work via /execute, /batch", "• HTTP API: 全ツールが/execute, /batchで動作"), itemStyle);
            GUILayout.Label(L("• Fixed 'Unknown operation' error for unmapped tools", "• マッピングなしツールの'Unknown operation'エラー修正"), itemStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // v1.2.2
            GUILayout.Label(L("v1.2.2 - SuperSave Fixes", "v1.2.2 - SuperSave修正"), sectionStyle);
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(L("<b>Fixed</b>", "<b>修正</b>"), itemStyle);
            GUILayout.Label(L("• SuperSave execute tool now works correctly", "• SuperSaveのexecuteツールが正常に動作"), itemStyle);
            GUILayout.Label(L("• All MCP clients use selected server mode", "• 全MCPクライアントで選択モードを使用"), itemStyle);
            GUILayout.Label(L("• Component details for all types (not null)", "• 全コンポーネントの詳細情報を取得可能"), itemStyle);
            GUILayout.Label(L("• Filter aliases: tag, layer, name accepted", "• フィルタ別名: tag, layer, name対応"), itemStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // v1.2.1
            GUILayout.Label(L("v1.2.1 - Hotfix", "v1.2.1 - 緊急修正"), sectionStyle);
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(L("<b>Fixed</b>", "<b>修正</b>"), itemStyle);
            GUILayout.Label(L("• SuperSave Mode: Added shutdown handlers", "• SuperSave: シャットダウン処理追加"), itemStyle);
            GUILayout.Label(L("• Proper MCP client cleanup on exit", "• MCP終了時の適切なクリーンアップ"), itemStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // v1.2.0
            GUILayout.Label(L("v1.2.0 - Token SuperSave Mode", "v1.2.0 - トークン SuperSave モード"), sectionStyle);
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label(L("<b>★ New: Token SuperSave Mode [Recommended]</b>", "<b>★ 新機能: Token SuperSave モード [推奨]</b>"), itemStyle);
            GUILayout.Label(L("• 99% context reduction with only 3 meta-tools", "• 3つのメタツールで99%のコンテキスト削減"), itemStyle);
            GUILayout.Label(L("• list_categories() - Discover tool categories", "• list_categories() - カテゴリ一覧"), itemStyle);
            GUILayout.Label(L("• list_tools(category) - See tools & parameters", "• list_tools(category) - ツール詳細"), itemStyle);
            GUILayout.Label(L("• execute(tool, params) - Run any of 350+ tools", "• execute(tool, params) - 350+ツール実行"), itemStyle);
            GUILayout.Label(L("• Works with all MCP clients", "• 全MCPクライアント対応"), itemStyle);
            GUILayout.Label(L("• Best for long AI sessions", "• 長いAIセッションに最適"), itemStyle);

            GUILayout.Space(10);

            GUILayout.Label(L("<b>Improvements</b>", "<b>改善</b>"), itemStyle);
            GUILayout.Label(L("• Setup window redesigned with mode selection", "• セットアップ画面をモード選択式に刷新"), itemStyle);
            GUILayout.Label(L("• SuperSave Mode set as default", "• SuperSaveモードをデフォルトに"), itemStyle);
            GUILayout.Label(L("• Better error messages with suggestions", "• エラーメッセージに提案を追加"), itemStyle);
            GUILayout.Label(L("• Tool registry loaded from JSON dynamically", "• ツール定義をJSONから動的読み込み"), itemStyle);

            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // v1.1.9
            GUILayout.Label(L("v1.1.9 - Stability Fixes", "v1.1.9 - 安定性修正"), sectionStyle);
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(L("• Batch tool format conversion fix", "• バッチツールのフォーマット変換修正"), itemStyle);
            GUILayout.Label(L("• MCP stdio protocol stability (JSON-RPC)", "• MCP stdio プロトコル安定化"), itemStyle);
            GUILayout.Label(L("• HTTP server localhost binding fix", "• HTTPサーバーのlocalhost接続修正"), itemStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // v1.1.8
            GUILayout.Label(L("v1.1.8 - Sphere Skybox", "v1.1.8 - 球体スカイボックス"), sectionStyle);
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(L("• Sphere Skybox: Create skybox from any photo", "• 球体スカイボックス: 写真からスカイボックス生成"), itemStyle);
            GUILayout.Label(L("• Multi-pipeline shader support", "• マルチパイプラインシェーダー対応"), itemStyle);
            GUILayout.Label(L("• MCP server renamed to unity-synaptic", "• MCPサーバー名をunity-synapticに変更"), itemStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Links
            GUILayout.Label(L("Links", "リンク"), sectionStyle);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("Documentation", "ドキュメント"), GUILayout.Height(25)))
            {
                Application.OpenURL("https://synaptic-ai.net/docs");
            }
            if (GUILayout.Button("Discord", GUILayout.Height(25)))
            {
                Application.OpenURL("https://discord.gg/MXwHCVWmPe");
            }
            if (GUILayout.Button(L("Website", "ウェブサイト"), GUILayout.Height(25)))
            {
                Application.OpenURL("https://synaptic-ai.net");
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Reset the "don't show" preference (for testing)
        /// </summary>
        [MenuItem("Tools/Synaptic Pro/Reset Changelog Preference", false, 101)]
        public static void ResetPreference()
        {
            EditorPrefs.DeleteKey(PREF_KEY_LAST_VERSION);
            EditorPrefs.DeleteKey(PREF_KEY_DONT_SHOW);
            Debug.Log("[Synaptic] Changelog preference reset. Will show on next startup.");
        }
    }
}
