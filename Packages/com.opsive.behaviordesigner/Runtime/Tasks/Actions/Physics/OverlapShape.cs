#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.PhysicsTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics")]
    [Opsive.Shared.Utility.Description("Performs box/sphere/capsule overlap check with layer mask, tag filter, and result collection.")]
    public class OverlapShape : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the overlap shape type.
        /// </summary>
        public enum OverlapType
        {
            Box,
            Sphere,
            Capsule
        }

        [Tooltip("The overlap type.")]
        [SerializeField] protected OverlapType m_OverlapType = OverlapType.Sphere;
        [Tooltip("The center of the overlap shape.")]
        [SerializeField] protected SharedVariable<Vector3> m_Center;
        [Tooltip("The size of the overlap shape (radius for sphere, halfExtents for box, radius/height for capsule).")]
        [SerializeField] protected SharedVariable<Vector3> m_Size = Vector3.one;
        [Tooltip("The rotation of the overlap shape. Only used for Box.")]
        [SerializeField] protected SharedVariable<Quaternion> m_Rotation;
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

        private Collider[] m_HitColliders = new Collider[50];
        private List<GameObject> m_OverlappedList = new List<GameObject>();

        /// <summary>
        /// Performs the shape overlap with filtering.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var center = m_Center.Value != Vector3.zero ? m_Center.Value : (m_ResolvedTransform != null ? m_ResolvedTransform.position : Vector3.zero);
            var rotation = m_Rotation.Value != Quaternion.identity ? m_Rotation.Value : (m_ResolvedTransform != null ? m_ResolvedTransform.rotation : Quaternion.identity);
            var hitCount = 0;
            switch (m_OverlapType) {
                case OverlapType.Box:
                    hitCount = Physics.OverlapBoxNonAlloc(center, m_Size.Value * 0.5f, m_HitColliders, rotation, m_LayerMask);
                    break;
                case OverlapType.Sphere:
                    hitCount = Physics.OverlapSphereNonAlloc(center, m_Size.Value.x, m_HitColliders, m_LayerMask);
                    break;
                case OverlapType.Capsule:
                    var point1 = center + Vector3.up * m_Size.Value.y;
                    var point2 = center - Vector3.up * m_Size.Value.y;
                    hitCount = Physics.OverlapCapsuleNonAlloc(point1, point2, m_Size.Value.x, m_HitColliders, m_LayerMask);
                    break;
            }

            m_OverlappedList.Clear();

            for (int i = 0; i < hitCount; ++i) {
                if (m_HitColliders[i] == null) {
                    continue;
                }

                var tagMatches = string.IsNullOrEmpty(m_RequiredTag.Value) || m_HitColliders[i].CompareTag(m_RequiredTag.Value);

                if (tagMatches) {
                    m_OverlappedList.Add(m_HitColliders[i].gameObject);
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
            m_OverlapType = OverlapType.Sphere;
            m_Center = null;
            m_Size = Vector3.one;
            m_Rotation = null;
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