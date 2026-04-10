#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SaveLoad
{
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Save/Load")]
    [Opsive.Shared.Utility.Description("Loads multiple PlayerPrefs values with default value fallback.")]
    public class LoadPlayerPrefsMultiple : Action
    {
        [Tooltip("The list of PlayerPrefs entries to load.")]
        [SerializeField] protected List<PlayerPrefsEntry> m_Entries = new List<PlayerPrefsEntry>();

        /// <summary>
        /// Loads all PlayerPrefs entries.
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
                                var defaultValue = entry.intValue.Value;
                                entry.intValue.Value = PlayerPrefs.GetInt(entry.key, defaultValue);
                            }
                            break;
                        case PlayerPrefsEntry.ValueType.Float:
                            if (entry.floatValue != null) {
                                var defaultValue = entry.floatValue.Value;
                                entry.floatValue.Value = PlayerPrefs.GetFloat(entry.key, defaultValue);
                            }
                            break;
                        case PlayerPrefsEntry.ValueType.String:
                            if (entry.stringValue != null) {
                                var defaultValue = entry.stringValue.Value;
                                entry.stringValue.Value = PlayerPrefs.GetString(entry.key, defaultValue);
                            }
                            break;
                    }
                }
            }

            return TaskStatus.Success;
        }
    }
}
#endif