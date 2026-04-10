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
    /// Sets a PlayerPrefs integer value.
    /// </summary>
    [Opsive.Shared.Utility.Category("Save/Load")]
    [Opsive.Shared.Utility.Description("Sets a PlayerPrefs integer value.")]
    public class SetPlayerPrefsInt : Action
    {
        [Tooltip("The PlayerPrefs key.")]
        [SerializeField] protected SharedVariable<string> m_Key;
        [Tooltip("The integer value to set.")]
        [SerializeField] protected SharedVariable<int> m_Value;
        [Tooltip("Should PlayerPrefs.Save() be called after setting the value?")]
        [SerializeField] protected bool m_Save = true;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_Key.Value)) {
                return TaskStatus.Success;
            }

            PlayerPrefs.SetInt(m_Key.Value, m_Value.Value);
            if (m_Save) {
                PlayerPrefs.Save();
            }

            return TaskStatus.Success;
        }
    }
}
#endif