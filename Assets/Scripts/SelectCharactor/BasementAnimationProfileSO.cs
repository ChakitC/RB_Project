using Animancer;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Basement/Animation Profile", fileName = "BasementAnimationProfile")]
public sealed class BasementAnimationProfileSO : ScriptableObject
{
    public ClipTransition idle = new ClipTransition { NormalizedStartTime = 0f };
    public ClipTransition pickup = new ClipTransition { NormalizedStartTime = 0f };
    // Serialized compatibility for status hosts without the menu presentation controller.
    // Current screens author both animation and camera in menus.
    [HideInInspector]
    public ClipTransition inMenuIdle = new ClipTransition { NormalizedStartTime = 0f };
    public MenuEntry[] menus = System.Array.Empty<MenuEntry>();

    [System.Serializable]
    public sealed class MenuEntry
    {
        public BasementMenuDefinitionSO menu;
        public ClipTransition animation = new ClipTransition { NormalizedStartTime = 0f };
        public BasementMenuCameraPresetSO camera;
    }

    public MenuEntry FindMenu(BasementMenuDefinitionSO menu)
    {
        if (menu == null || menus == null) return null;
        MenuEntry result = null;
        foreach (var entry in menus)
        {
            if (entry == null || entry.menu != menu) continue;
            if (result != null)
            {
                Debug.LogWarning($"[Basement Menu] Duplicate entry for {menu.name} in {name}.", this);
                return null;
            }
            result = entry;
        }
        return result;
    }
}
