#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SaveLoad
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Save/Load")]
    [Opsive.Shared.Utility.Description("Deserializes object from PlayerPrefs with type checking.")]
    public class DeserializeFromPlayerPrefs : Action
    {
        [Tooltip("The key prefix for PlayerPrefs entries.")]
        [SerializeField] protected SharedVariable<string> m_KeyPrefix = "SaveData_";
        [Tooltip("The main key to load the serialized data from.")]
        [SerializeField] protected SharedVariable<string> m_MainKey = "SerializedData";
        [Tooltip("The default value if key doesn't exist.")]
        [SerializeField] protected SharedVariable<string> m_DefaultValue = "";
        [Tooltip("The deserialized data (as JSON string).")]
        [SerializeField] [RequireShared] protected SharedVariable<string> m_DeserializedData;
        [Tooltip("Whether the deserialization was successful.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_DeserializeSuccessful;

        /// <summary>
        /// Deserializes the data from PlayerPrefs.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_MainKey.Value)) {
                m_DeserializeSuccessful.Value = false;
                m_DeserializedData.Value = "";
                return TaskStatus.Success;
            }

            try {
                var keyPrefix = string.IsNullOrEmpty(m_KeyPrefix.Value) ? "SaveData_" : m_KeyPrefix.Value;
                var fullKey = keyPrefix + m_MainKey.Value;
                m_DeserializedData.Value = PlayerPrefs.GetString(fullKey, m_DefaultValue.Value);
                m_DeserializeSuccessful.Value = !string.IsNullOrEmpty(m_DeserializedData.Value);
            } catch (System.Exception e) {
                Debug.LogError($"DeserializeFromPlayerPrefs: Failed to deserialize data. Error: {e.Message}");
                m_DeserializeSuccessful.Value = false;
                m_DeserializedData.Value = "";
            }

            return TaskStatus.Success;
        }
    }
}
#endif