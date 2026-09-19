using UnityEngine;

namespace NuclearOptionCommander;

internal static class CommanderUiTheme
{
    private static bool initialized;
    private static Texture2D? panelTexture;
    private static Texture2D? raisedTexture;
    private static Texture2D? cardTexture;
    private static Texture2D? buttonTexture;
    private static Texture2D? buttonHoverTexture;
    private static Texture2D? buttonActiveTexture;
    private static Texture2D? accentTexture;
    private static Texture2D? accentBrightTexture;
    private static Texture2D? warningTexture;
    private static Texture2D? warningHoverTexture;
    private static Texture2D? borderTexture;
    private static Texture2D? borderMutedTexture;
    private static Texture2D? headerStripeTexture;

    internal static GUIStyle Window { get; private set; } = null!;
    internal static GUIStyle Panel { get; private set; } = null!;
    internal static GUIStyle Card { get; private set; } = null!;
    internal static GUIStyle Button { get; private set; } = null!;
    internal static GUIStyle PrimaryButton { get; private set; } = null!;
    internal static GUIStyle DangerButton { get; private set; } = null!;
    internal static GUIStyle SelectedButton { get; private set; } = null!;
    internal static GUIStyle SuccessButton { get; private set; } = null!;
    internal static GUIStyle Label { get; private set; } = null!;
    internal static GUIStyle MutedLabel { get; private set; } = null!;
    internal static GUIStyle Header { get; private set; } = null!;
    internal static GUIStyle SubHeader { get; private set; } = null!;
    internal static GUIStyle Money { get; private set; } = null!;
    internal static GUIStyle HelpButton { get; private set; } = null!;
    internal static GUIStyle CloseButton { get; private set; } = null!;
    internal static GUIStyle Toggle { get; private set; } = null!;
    internal static GUIStyle Badge { get; private set; } = null!;

    // Military Tactical Color Palette
    private static readonly Color DefaultAccent = new(0.25f, 0.88f, 0.82f, 1f);
    private static readonly Color AccentGlow = new(0.35f, 0.95f, 0.90f, 1f);
    private static readonly Color MutedTeal = new(0.55f, 0.72f, 0.72f, 1f);
    private static readonly Color TextBright = new(0.95f, 0.98f, 0.98f, 1f);
    private static readonly Color GoldColor = new(0.98f, 0.82f, 0.32f, 1f);
    private static readonly Color GreenColor = new(0.32f, 0.92f, 0.52f, 1f);

    internal static Color Accent => DefaultAccent;
    internal static Color Friendly => GameAssets.i != null ? GameAssets.i.HUDFriendly : DefaultAccent;
    internal static Color Hostile => new(1f, 0.32f, 0.28f, 1f);
    internal static Color Gold => GoldColor;
    internal static Color Green => GreenColor;

    internal static Texture2D BorderTexture
    {
        get
        {
            Ensure();
            return borderTexture!;
        }
    }

    internal static void Ensure()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        // High-end textures with subtle gradients & glassmorphism
        panelTexture = MakeTexture(new Color(0.04f, 0.065f, 0.08f, 0.96f));
        raisedTexture = MakeTexture(new Color(0.07f, 0.11f, 0.13f, 0.98f));
        cardTexture = MakeTexture(new Color(0.09f, 0.14f, 0.17f, 0.95f));

        buttonTexture = MakeTexture(new Color(0.12f, 0.18f, 0.21f, 1f));
        buttonHoverTexture = MakeTexture(new Color(0.18f, 0.28f, 0.32f, 1f));
        buttonActiveTexture = MakeTexture(new Color(0.15f, 0.52f, 0.50f, 1f));

        accentTexture = MakeTexture(new Color(0.12f, 0.42f, 0.40f, 1f));
        accentBrightTexture = MakeTexture(new Color(0.20f, 0.65f, 0.60f, 1f));
        warningTexture = MakeTexture(new Color(0.55f, 0.18f, 0.15f, 1f));
        warningHoverTexture = MakeTexture(new Color(0.70f, 0.22f, 0.18f, 1f));

        borderTexture = MakeTexture(new Color(0.25f, 0.88f, 0.82f, 0.9f));
        borderMutedTexture = MakeTexture(new Color(0.18f, 0.32f, 0.35f, 0.7f));
        headerStripeTexture = MakeTexture(new Color(0.25f, 0.88f, 0.82f, 0.85f));

        // Font discovery: attempt to use crisp game/monospaced fonts
        Font? appFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Consolas", "Arial" }, 13);

        Label = new GUIStyle(GUI.skin.label)
        {
            font = appFont,
            fontSize = 13,
            normal = { textColor = TextBright },
            wordWrap = true,
            alignment = TextAnchor.MiddleLeft
        };

        MutedLabel = new GUIStyle(Label)
        {
            fontSize = 11,
            normal = { textColor = MutedTeal }
        };

        Header = new GUIStyle(Label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = AccentGlow },
            alignment = TextAnchor.MiddleLeft
        };

        SubHeader = new GUIStyle(Label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = GoldColor },
            alignment = TextAnchor.MiddleLeft
        };

        Window = new GUIStyle(GUI.skin.window)
        {
            font = appFont,
            normal = { background = panelTexture, textColor = TextBright },
            onNormal = { background = panelTexture, textColor = TextBright },
            border = new RectOffset(2, 2, 2, 2),
            padding = new RectOffset(12, 12, 30, 12),
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };

        Panel = new GUIStyle(GUI.skin.box)
        {
            font = appFont,
            normal = { background = raisedTexture, textColor = TextBright },
            border = new RectOffset(1, 1, 1, 1),
            padding = new RectOffset(8, 8, 8, 8)
        };

        Card = new GUIStyle(GUI.skin.box)
        {
            font = appFont,
            normal = { background = cardTexture, textColor = TextBright },
            border = new RectOffset(1, 1, 1, 1),
            padding = new RectOffset(10, 10, 6, 6)
        };

        Button = new GUIStyle(GUI.skin.button)
        {
            font = appFont,
            normal = { background = buttonTexture, textColor = TextBright },
            hover = { background = buttonHoverTexture, textColor = Color.white },
            active = { background = buttonActiveTexture, textColor = Color.white },
            focused = { background = buttonHoverTexture, textColor = Color.white },
            border = new RectOffset(1, 1, 1, 1),
            padding = new RectOffset(8, 8, 5, 5),
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        PrimaryButton = new GUIStyle(Button)
        {
            normal = { background = accentTexture, textColor = Color.white },
            hover = { background = accentBrightTexture, textColor = Color.white },
            active = { background = buttonActiveTexture, textColor = Color.white },
            fontStyle = FontStyle.Bold
        };

        DangerButton = new GUIStyle(Button)
        {
            normal = { background = warningTexture, textColor = Color.white },
            hover = { background = warningHoverTexture, textColor = Color.white },
            fontStyle = FontStyle.Bold
        };

        SelectedButton = new GUIStyle(Button)
        {
            normal = { background = buttonActiveTexture, textColor = Color.white },
            hover = { background = accentBrightTexture, textColor = Color.white },
            fontStyle = FontStyle.Bold
        };

        SuccessButton = new GUIStyle(Button)
        {
            normal = { background = MakeTexture(new Color(0.12f, 0.45f, 0.22f, 1f)), textColor = Color.white },
            hover = { background = MakeTexture(new Color(0.16f, 0.60f, 0.28f, 1f)), textColor = Color.white },
            fontStyle = FontStyle.Bold
        };

        HelpButton = new GUIStyle(Button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(0, 0, 0, 0),
            normal = { background = buttonTexture, textColor = MutedTeal },
            hover = { background = buttonHoverTexture, textColor = AccentGlow }
        };

        CloseButton = new GUIStyle(Button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(0, 0, 0, 0),
            normal = { background = warningTexture, textColor = Color.white },
            hover = { background = warningHoverTexture, textColor = Color.white }
        };

        Toggle = new GUIStyle(GUI.skin.toggle)
        {
            font = appFont,
            normal = { textColor = TextBright },
            hover = { textColor = Color.white },
            onNormal = { textColor = AccentGlow },
            onHover = { textColor = Color.white },
            fontSize = 12,
            wordWrap = true
        };

        Money = new GUIStyle(Panel)
        {
            font = appFont,
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = raisedTexture, textColor = GoldColor }
        };

        Badge = new GUIStyle(Panel)
        {
            font = appFont,
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = cardTexture, textColor = MutedTeal },
            padding = new RectOffset(4, 4, 2, 2)
        };
    }

    internal static Rect ClampWindow(Rect rect, float margin = 12f)
    {
        rect.width = Mathf.Min(rect.width, Mathf.Max(160f, CommanderUiScale.Width - margin * 2f));
        rect.height = Mathf.Min(rect.height, Mathf.Max(120f, CommanderUiScale.Height - margin * 2f));
        rect.x = Mathf.Clamp(rect.x, margin, Mathf.Max(margin, CommanderUiScale.Width - rect.width - margin));
        rect.y = Mathf.Clamp(rect.y, margin, Mathf.Max(margin, CommanderUiScale.Height - rect.height - margin));
        return rect;
    }

    internal static void DrawFrame(Rect rect, float thickness = 1.5f)
    {
        Ensure();
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), borderTexture!);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), borderTexture!);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), borderTexture!);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), borderTexture!);
    }

    internal static void DrawMutedFrame(Rect rect, float thickness = 1f)
    {
        Ensure();
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), borderMutedTexture!);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), borderMutedTexture!);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), borderMutedTexture!);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), borderMutedTexture!);
    }

    internal static void DrawHeaderStripe(Rect rect, float height = 2f)
    {
        Ensure();
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 26f, rect.width - 4f, height), headerStripeTexture!);
    }

    internal static bool DrawHelpButton(float windowWidth, ref bool visible)
    {
        Ensure();
        if (GUI.Button(new Rect(windowWidth - 62f, 4f, 24f, 22f), "?", HelpButton))
        {
            visible = !visible;
        }
        return visible;
    }

    internal static bool DrawCloseButton(float windowWidth)
    {
        Ensure();
        return GUI.Button(new Rect(windowWidth - 34f, 4f, 26f, 22f), "✕", CloseButton);
    }

    internal static void DrawHelpOverlay(Rect rect, string text)
    {
        Ensure();
        GUI.Box(rect, string.Empty, Panel);
        DrawFrame(rect, 1f);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f), text, Label);
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return texture;
    }
}
