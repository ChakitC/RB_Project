#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Compares GameObject tag and layer with multiple conditions.")]
    public class CompareTagAndLayer : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the comparison mode.
        /// </summary>
        public enum ComparisonMode
        {
            BothMatch,
            EitherMatches,
            TagOnly,
            LayerOnly
        }

        [Tooltip("The tag to compare against.")]
        [SerializeField] protected SharedVariable<string> m_Tag;
        [Tooltip("The layer mask to compare against.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;
        [Tooltip("The comparison mode.")]
        [SerializeField] protected ComparisonMode m_ComparisonMode = ComparisonMode.BothMatch;
        [Tooltip("Whether the comparison condition is met.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_ConditionMet;

        /// <summary>
        /// Compares tag and layer.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var tagMatches = string.IsNullOrEmpty(m_Tag.Value) || m_ResolvedGameObject.CompareTag(m_Tag.Value);
            var layerMatches = ((1 << m_ResolvedGameObject.layer) & m_LayerMask) != 0;

            var conditionMet = false;
            switch (m_ComparisonMode) {
                case ComparisonMode.BothMatch:
                    conditionMet = tagMatches && layerMatches;
                    break;
                case ComparisonMode.EitherMatches:
                    conditionMet = tagMatches || layerMatches;
                    break;
                case ComparisonMode.TagOnly:
                    conditionMet = tagMatches;
                    break;
                case ComparisonMode.LayerOnly:
                    conditionMet = layerMatches;
                    break;
            }

            m_ConditionMet.Value = conditionMet;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Tag = null;
            m_LayerMask = -1;
            m_ComparisonMode = ComparisonMode.BothMatch;
            m_ConditionMet = null;
        }
    }
}
#endif