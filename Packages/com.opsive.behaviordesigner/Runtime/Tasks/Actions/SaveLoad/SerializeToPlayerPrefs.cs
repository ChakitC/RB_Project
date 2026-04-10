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
    [Opsive.Shared.Utility.Description("Serializes object to PlayerPrefs with key prefix.")]
    public class SerializeToPlayerPrefs : Action
    {
        [Tooltip("The key prefix for PlayerPrefs entries.")]
        [SerializeField] protected SharedVariable<string> m_KeyPrefix = "SaveData_";
        [Tooltip("The data to serialize (as JSON string).")]
        [SerializeField] protected SharedVariable<string> m_DataToSerialize;
        [Tooltip("The main key to store the serialized data under.")]
        [SerializeField] protected SharedVariable<string> m_MainKey = "SerializedData";
        [Tooltip("Whether the serialization was successful.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_SerializeSuccessful;

        /// <summary>
        /// Serializes the data to PlayerPrefs.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_MainKey.Value)) {
                m_SerializeSuccessful.Value = false;
                return TaskStatus.Success;
            }

            try {
                var keyPrefix = string.IsNullOrEmpty(m_KeyPrefix.Value) ? "SaveData_" : m_KeyPrefix.Value;
                var fullKey = keyPrefix + m_MainKey.Value;
                PlayerPrefs.SetString(fullKey, m_DataToSerialize.Value);
                PlayerPrefs.Save();
                m_SerializeSuccessful.Value = true;
            } catch (System.Exception e) {
                Debug.LogError($"SerializeToPlayerPrefs: Failed to serialize data. Error: {e.Message}");
                m_SerializeSuccessful.Value = false;
            }

            return TaskStatus.Success;
        }
    }
}
#endif