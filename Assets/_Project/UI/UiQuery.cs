using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // T14-private helpers: required element lookup (fails loudly), show/hide, dev-build flag.
    internal static class UiQuery
    {
        // Runtime check (true in the editor and in Development Builds); replaces the deprecated DEVELOPMENT_BUILD define.
        public static bool IsDevBuild => Debug.isDebugBuild;

        public static T Req<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null) throw new InvalidOperationException("UI element missing in AppRoot.uxml: " + name + " (" + typeof(T).Name + ")");
            return element;
        }

        public static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
