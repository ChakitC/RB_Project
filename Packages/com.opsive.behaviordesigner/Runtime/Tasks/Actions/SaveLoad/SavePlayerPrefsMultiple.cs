#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SaveLoad
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class PlayerPrefsEntry
    {
        public string key;
        public SharedVariable<int> intValue;
        public SharedVariable<float> floatValue;
        public SharedVariable<string> stringValue;
        public enum ValueType { Int, Float, String }
        public ValueType valueType;
    }

    [Opsive.Shared.Utility.Category("Save/Load")]
    [Opsive.Shared.Utility.Description("Saves multiple PlayerPrefs values (int, float, string) in one action.")]
    public class SavePlayerPrefsMultiple : Action
    {
        [Tooltip("The list of PlayerPrefs entries to save.")]
        [SerializeField] protected List<PlayerPrefsEntry> m_Entries = new List<PlayerPrefsEntry>();

        /// <summary>
        /// Saves all PlayerPrefs entries.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Entries != null) {
                for (int i = 0; i < m_Entries.Count; ++i) {
                    var entry = m_Entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key)) {
                        continue;
                    }

                    switch (entry.valueType) {
                        case PlayerPrefsEntry.ValueType.Int:
                            if (entry.intValue != null) {
                                PlayerPrefs.SetInt(entry.key, entry.intValue.Value);
                            }
                            break;
                        case PlayerPrefsEntry.ValueType.Float:
                            if (entry.floatValue != null) {
                                PlayerPrefs.SetFloat(entry.key, entry.floatValue.Value);
                            }
                            break;
                        case PlayerPrefsEntry.ValueType.String:
                            if (entry.stringValue != null) {
                                PlayerPrefs.SetString(entry.key, entry.stringValue.Value);
                            }
                            break;
                    }
                }
                PlayerPrefs.Save();
            }

            return TaskStatus.Success;
        }
    }
}
#endif