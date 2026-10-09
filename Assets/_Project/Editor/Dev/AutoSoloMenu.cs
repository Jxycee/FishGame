// FishGame > Dev > Auto Solo (checkable, EditorPrefs). The main-editor-only gate lives in T13 DevStartup (MPPM clones share EditorPrefs). Owned by T15.
using FishGame.Runtime;
using UnityEditor;

namespace FishGame.EditorTools
{
    public static class AutoSoloMenu
    {
        const string MenuPath = "FishGame/Dev/Auto Solo";

        [MenuItem(MenuPath)]
        public static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(DevPrefKeys.AutoSolo, false);
            EditorPrefs.SetBool(DevPrefKeys.AutoSolo, next);
            Menu.SetChecked(MenuPath, next);
        }

        [MenuItem(MenuPath, true)]
        public static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(DevPrefKeys.AutoSolo, false));
            return true;
        }
    }
}
