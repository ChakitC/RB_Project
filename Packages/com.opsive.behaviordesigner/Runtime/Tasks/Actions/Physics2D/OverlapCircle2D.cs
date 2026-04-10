#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics2D")]
    [Opsive.Shared.Utility.Description("Performs 2D overlap circle detection with layer mask, tag filter, and result processing.")]
    public class OverlapCircle2D : TargetGameObjectAction
    {
        [Tooltip("The center of the overlap circle.")]
        [SerializeField] protected SharedVariable<Vector2> m_Center;
        [Tooltip("The radius of the overlap circle.")]
        [SerializeField] protected SharedVariable<float> m_Radius = 1.0f;
        [Tooltip("The layer mask to filter hits.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;
        [Tooltip("The tag to filter hits. Leave empty to ignore tag filtering.")]
        [SerializeField] protected SharedVariable<string> m_RequiredTag = "";
        [Tooltip("The minimum number of overlaps required.")]
        [SerializeField] protected SharedVariable<int> m_MinOverlaps = 0;
        [Tooltip("Whether overlaps were detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_OverlapsDetected;
        [Tooltip("The number of overlaps detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<int> m_OverlapCount;
        [Tooltip("The GameObjects that were overlapped.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject[]> m_OverlappedGameObjects;

        private List<GameObject> m_OverlappedList = new List<GameObject>();

        /// <summary>
        /// Performs the 2D overlap circle detection with filtering.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var center = m_Center.Value == Vector2.zero && m_ResolvedTransform != null ? new Vector2(m_ResolvedTransform.position.x, m_ResolvedTransform.position.y) : m_Center.Value;
            var colliders = Physics2D.OverlapCircleAll(center, m_Radius.Value, m_LayerMask);
            m_OverlappedList.Clear();

            foreach (var collider in colliders) {
                if (collider == null) {
                    continue;
                }

                var tagMatches = string.IsNullOrEmpty(m_RequiredTag.Value) || collider.CompareTag(m_RequiredTag.Value);

                if (tagMatches) {
                    m_OverlappedList.Add(collider.gameObject);
                }
            }

            m_OverlapCount.Value = m_OverlappedList.Count;
            m_OverlapsDetected.Value = m_OverlapCount.Value >= m_MinOverlaps.Value;
            m_OverlappedGameObjects.Value = m_OverlappedList.ToArray();

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Center = null;
            m_Radius = 1.0f;
            m_LayerMask = -1;
            m_RequiredTag = "";
            m_MinOverlaps = 0;
            m_OverlapsDetected = null;
            m_OverlapCount = null;
            m_OverlappedGameObjects = null;
        }
    }
}
#endif