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
    /// Deletes a PlayerPrefs key.
    /// </summary>
    [Opsive.Shared.Utility.Category("Save/Load")]
    [Opsive.Shared.Utility.Description("Deletes a PlayerPrefs key.")]
    public class DeletePlayerPrefsKey : Action
    {
        [Tooltip("The PlayerPrefs key to delete.")]
        [SerializeField] protected SharedVariable<string> m_Key;
        [Tooltip("Should PlayerPrefs.Save() be called after deleting the key?")]
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

            PlayerPrefs.DeleteKey(m_Key.Value);
            if (m_Save) {
                PlayerPrefs.Save();
            }

            return TaskStatus.Success;
        }
    }
}
#endif