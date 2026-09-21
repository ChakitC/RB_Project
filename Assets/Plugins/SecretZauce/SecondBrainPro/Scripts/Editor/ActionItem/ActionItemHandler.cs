using System;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public class ActionItemHandler : ActionItemHandlerBase
    {
        public override string TryCreateFromActionItem(Type childType, string defaultName)
        {
            // Compute a sensible default name when creating certain typed children.
            // For ActionItem subclasses allow the class to provide an override via
            // the virtual DefaultName property. We create a temporary ScriptableObject
            // instance to read the overridden value (same pattern as ActionItemSelector uses
            // for ActionPath) and then destroy it immediately.
            if (childType != null)
            {
                try
                {
                    var actionItemBase = typeof(ActionItem);
                    if (actionItemBase.IsAssignableFrom(childType))
                    {
                        var tempInstance = ScriptableObject.CreateInstance(childType) as ActionItem;
                        if (tempInstance != null)
                        {
                            defaultName = tempInstance.DefaultName ?? childType.Name;
                            UnityEngine.Object.DestroyImmediate(tempInstance);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[TreeView] Failed to compute ActionItem default name for type '{childType?.Name}': {ex.Message}");
                    defaultName = string.Empty;
                }
            }

            return defaultName;
        }
        
        /// <summary>
        /// Adds all discovered ActionItem types to an existing GenericMenu under a parent label.
        /// This allows embedding the action-type entries as a submenu of a larger context menu
        /// (preferred to showing a separate popup menu when invoked from another GenericMenu).
        /// </summary>
        public override void AddToMenu(GenericMenu menu, Action<Type> onTypeSelected, string parentLabel = "Add Action")
        {
            if (menu == null || onTypeSelected == null)
                return;

            var types = TypeCache.GetTypesDerivedFrom<ActionItem>();
            bool hasAny = false;
            foreach (var type in types)
            {
                if (type.IsAbstract)
                    continue;

                hasAny = true;
                var capturedType = type;
                string actionPath = GetActionPath(type);
                string menuPath = string.IsNullOrEmpty(actionPath)
                    ? type.Name
                    : actionPath + "/" + type.Name;

                // Add under the parent label so entries appear as a submenu
                menu.AddItem(new GUIContent(parentLabel + "/" + menuPath), false, () => onTypeSelected(capturedType));
            }

            if (!hasAny)
            {
                menu.AddDisabledItem(new GUIContent(parentLabel + "/No ActionItem types found"));
            }
        }

        /// <summary>
        /// Returns the <see cref="ActionItem.ActionPath"/> for <paramref name="type"/> by
        /// creating a temporary (non-persisted) ScriptableObject instance. Falls back to an
        /// empty string if instantiation fails.
        /// </summary>
        static string GetActionPath(Type type)
        {
            try
            {
                var tempInstance = ScriptableObject.CreateInstance(type) as ActionItem;
                if (tempInstance == null)
                    return string.Empty;

                string path = tempInstance.ActionPath ?? string.Empty;
                UnityEngine.Object.DestroyImmediate(tempInstance);
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ActionItemSelector] Failed to read ActionPath for type '{type?.Name}': {ex.Message}");
                return string.Empty;
            }
        }
    }
}
