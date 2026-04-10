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

    /// <summary>
    /// Gets a PlayerPrefs integer value.
    /// </summary>
    [Opsive.Shared.Utility.Category("Save/Load")]
    [Opsive.Shared.Utility.Description("Gets a PlayerPrefs integer value.")]
    public class GetPlayerPrefsInt : Action
    {
        [Tooltip("The PlayerPrefs key.")]
        [SerializeField] protected SharedVariable<string> m_Key;
        [Tooltip("The default value to use if the key doesn't exist.")]
        [SerializeField] protected SharedVariable<int> m_DefaultValue;
        [Tooltip("The variable to store the retrieved value.")]
        [SerializeField] [RequireShared] protected SharedVariable<int> m_Value;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_Key.Value)) {
                return TaskStatus.Success;
            }

            m_Value.Value = PlayerPrefs.GetInt(m_Key.Value, m_DefaultValue.Value);

            return TaskStatus.Success;
        }
    }
}
#endif