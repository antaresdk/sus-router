using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// Shared PanelSettings factory for playmode harnesses.
    /// Assigns UnityDefaultRuntimeTheme when available (Unity 6 warns/errors otherwise).
    /// </summary>
    internal static class SusTestPanelFactory
    {
        const string DefaultThemePath =
            "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";

        public static PanelSettings Create(string name)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = name;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
#if UNITY_EDITOR
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(DefaultThemePath);
            if (theme != null)
                settings.themeStyleSheet = theme;
#endif
            return settings;
        }
    }
}
