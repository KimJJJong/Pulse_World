using TMPro;
using UnityEngine;

public static class WorldMapFontLibrary
{
    public const string TitleFontName = "Cinzel Decorative Bold";
    public const string HeaderFontName = "Cormorant Garamond SemiBold";
    public const string ButtonFontName = "Cinzel Bold";
    public const string KoreanBodyFontName = "Gowun Batang";

    private const string ResourceRoot = "Fonts & Materials/";

    private static TMP_FontAsset _titleFont;
    private static TMP_FontAsset _headerFont;
    private static TMP_FontAsset _buttonFont;
    private static TMP_FontAsset _koreanBodyFont;

    public static TMP_FontAsset TitleFont => LoadFont(ref _titleFont, TitleFontName);
    public static TMP_FontAsset HeaderFont => LoadFont(ref _headerFont, HeaderFontName);
    public static TMP_FontAsset ButtonFont => LoadFont(ref _buttonFont, ButtonFontName);
    public static TMP_FontAsset KoreanBodyFont => LoadFont(ref _koreanBodyFont, KoreanBodyFontName);

    public static void ApplyPreferredFont(TMP_Text text, TMP_FontAsset fallbackFont = null)
    {
        if (text == null)
            return;

        TMP_FontAsset font = ResolvePreferredFont(text);
        if (!IsUsableFont(font))
            font = fallbackFont;
        if (!IsUsableFont(font))
            font = null;

        if (font == null)
            return;

        text.font = font;
        text.fontSharedMaterial = font.material;
    }

    private static TMP_FontAsset ResolvePreferredFont(TMP_Text text)
    {
        string objectName = text.name ?? "";
        string value = text.text ?? "";

        if (ContainsKorean(value))
            return KoreanBodyFont;

        if (objectName == "MapTitleText" || value.Trim() == "WORLD MAP")
            return TitleFont;

        if (objectName == "RealmTitle" || objectName == "SelectedTownTitle" || objectName == "KeyResultTitle")
            return HeaderFont;

        if (objectName == "RealmTicket")
            return HeaderFont ?? KoreanBodyFont;

        if (objectName == "Label" && IsInsideSelectButton(text.transform))
            return ButtonFont;

        if (value.Trim() == "TRAVEL HERE")
            return ButtonFont;

        if (objectName == "RealmDescription" || objectName == "MapStatus")
            return KoreanBodyFont;

        return KoreanBodyFont ?? HeaderFont ?? ButtonFont ?? TitleFont;
    }

    private static bool IsInsideSelectButton(Transform transform)
    {
        while (transform != null)
        {
            if (transform.name == "Button_SelectRealm")
                return true;

            transform = transform.parent;
        }

        return false;
    }

    private static TMP_FontAsset LoadFont(ref TMP_FontAsset cache, string fontName)
    {
        if (cache != null)
            return cache;

        cache = Resources.Load<TMP_FontAsset>(ResourceRoot + fontName);
        if (cache == null)
            cache = Resources.Load<TMP_FontAsset>(fontName);
        return cache;
    }

    private static bool IsUsableFont(TMP_FontAsset font)
    {
        return font != null
               && font.material != null
               && font.atlasTextures != null
               && font.atlasTextures.Length > 0
               && font.atlasTextures[0] != null;
    }

    private static bool ContainsKorean(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c >= 0xAC00 && c <= 0xD7A3)
                return true;
        }

        return false;
    }
}
