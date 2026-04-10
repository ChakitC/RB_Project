#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SaveLoad
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.IO;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Save/Load")]
    [Opsive.Shared.Utility.Description("Loads data from JSON file with type validation and error handling.")]
    public class LoadJSONData : Action
    {
        [Tooltip("The file path to load from (relative to Application.persistentDataPath).")]
        [SerializeField] protected SharedVariable<string> m_FilePath;
        [Tooltip("The loaded JSON string data.")]
        [SerializeField] [RequireShared] protected SharedVariable<string> m_JSONData;
        [Tooltip("Whether the load was successful.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_LoadSuccessful;

        /// <summary>
        /// Loads the JSON data from file.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_FilePath.Value)) {
                m_LoadSuccessful.Value = false;
                return TaskStatus.Success;
            }

            try {
                var fullPath = Path.Combine(Application.persistentDataPath, m_FilePath.Value);

                if (!File.Exists(fullPath)) {
                    m_LoadSuccessful.Value = false;
                    m_JSONData.Value = "";
                    return TaskStatus.Success;
                }

                m_JSONData.Value = File.ReadAllText(fullPath);
                m_LoadSuccessful.Value = true;
            } catch (System.Exception e) {
                Debug.LogError($"LoadJSONData: Failed to load JSON data. Error: {e.Message}");
                m_LoadSuccessful.Value = false;
                m_JSONData.Value = "";
            }

            return TaskStatus.Success;
        }
    }
}
#endif