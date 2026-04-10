#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AnimatorTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class AnimatorLayerSync
    {
        public int layerIndex;
        public SharedVariable<float> weight = 1.0f;
        public SharedVariable<float> normalizedTime = 0.0f;
    }

    [Opsive.Shared.Utility.Category("Animator")]
    [Opsive.Shared.Utility.Description("Synchronizes multiple animator layers with weight and time control.")]
    public class SyncAnimatorLayers : TargetGameObjectAction
    {
        [Tooltip("The list of layers to synchronize.")]
        [SerializeField] protected List<AnimatorLayerSync> m_Layers = new List<AnimatorLayerSync>();

        private Animator m_ResolvedAnimator;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAnimator = m_ResolvedGameObject.GetComponent<Animator>();
        }

        /// <summary>
        /// Synchronizes the animator layers.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAnimator == null) {
                return TaskStatus.Success;
            }

            if (m_Layers != null) {
                for (int i = 0; i < m_Layers.Count; ++i) {
                    var layer = m_Layers[i];
                    if (layer == null) {
                        continue;
                    }

                    if (layer.layerIndex >= 0 && layer.layerIndex < m_ResolvedAnimator.layerCount) {
                        m_ResolvedAnimator.SetLayerWeight(layer.layerIndex, Mathf.Clamp01(layer.weight.Value));

                        var stateInfo = m_ResolvedAnimator.GetCurrentAnimatorStateInfo(layer.layerIndex);
                        if (stateInfo.length > 0) {
                            m_ResolvedAnimator.Play(stateInfo.fullPathHash, layer.layerIndex, layer.normalizedTime.Value);
                        }
                    }
                }
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Layers = new List<AnimatorLayerSync>();
        }
    }
}
#endif