#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Resources
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Resources")]
    [Opsive.Shared.Utility.Description("Unloads unused assets with progress callback.")]
    public class UnloadUnusedAssets : Action
    {
        [Tooltip("Whether to unload unused assets.")]
        [SerializeField] protected SharedVariable<bool> m_Unload = true;
        [Tooltip("The unloading progress (0-1).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_UnloadProgress;
        [Tooltip("Whether the unload operation is complete.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_UnloadComplete;

        private AsyncOperation m_UnloadOperation;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_UnloadProgress.Value = 0.0f;
            m_UnloadComplete.Value = false;
            m_UnloadOperation = null;

            if (m_Unload.Value) {
                m_UnloadOperation = UnityEngine.Resources.UnloadUnusedAssets();
            }
        }

        /// <summary>
        /// Updates the unload operation.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_UnloadOperation != null) {
                m_UnloadProgress.Value = m_UnloadOperation.progress;
                m_UnloadComplete.Value = m_UnloadOperation.isDone;

                if (m_UnloadOperation.isDone) {
                    return TaskStatus.Success;
                }
            } else {
                m_UnloadProgress.Value = 0.0f;
                m_UnloadComplete.Value = false;
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }
    }
}
#endif