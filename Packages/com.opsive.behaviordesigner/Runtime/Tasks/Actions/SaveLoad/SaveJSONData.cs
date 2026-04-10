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
    [Opsive.Shared.Utility.Description("Saves data to JSON file with path validation and error handling.")]
    public class SaveJSONData : Action
    {
        [Tooltip("The file path to save to (relative to Application.persistentDataPath).")]
        [SerializeField] protected SharedVariable<string> m_FilePath;
        [Tooltip("The JSON string data to save.")]
        [SerializeField] protected SharedVariable<string> m_JSONData;
        [Tooltip("Whether the save was successful.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_SaveSuccessful;

        /// <summary>
        /// Saves the JSON data to file.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_FilePath.Value)) {
                m_SaveSuccessful.Value = false;
                return TaskStatus.Success;
            }

            try {
                var fullPath = Path.Combine(Application.persistentDataPath, m_FilePath.Value);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, m_JSONData.Value);
                m_SaveSuccessful.Value = true;
            } catch (System.Exception e) {
                Debug.LogError($"SaveJSONData: Failed to save JSON data. Error: {e.Message}");
                m_SaveSuccessful.Value = false;
            }

            return TaskStatus.Success;
        }
    }
}
#endif