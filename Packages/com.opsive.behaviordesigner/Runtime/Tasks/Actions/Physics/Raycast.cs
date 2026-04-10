#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.PhysicsTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics")]
    [Opsive.Shared.Utility.Description("Performs a raycast with layer mask, tag filter, and hit result processing.")]
    public class Raycast : TargetGameObjectAction
    {
        [Tooltip("The origin of the raycast.")]
        [SerializeField] protected SharedVariable<Vector3> m_Origin;
        [Tooltip("The direction of the raycast.")]
        [SerializeField] protected SharedVariable<Vector3> m_Direction;
        [Tooltip("The maximum distance of the raycast.")]
        [SerializeField] protected SharedVariable<float> m_MaxDistance = Mathf.Infinity;
        [Tooltip("The layer mask to filter hits.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;
        [Tooltip("The tag to filter hits. Leave empty to ignore tag filtering.")]
        [SerializeField] protected SharedVariable<string> m_RequiredTag = "";
        [Tooltip("Whether a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_HitDetected;
        [Tooltip("The hit point if a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_HitPoint;
        [Tooltip("The hit normal if a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_HitNormal;
        [Tooltip("The GameObject that was hit.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_HitGameObject;
        [Tooltip("The distance to the hit point.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_HitDistance;

        /// <summary>
        /// Performs the raycast with filtering.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var origin = m_Origin.Value != Vector3.zero ? m_Origin.Value : (m_ResolvedTransform != null ? m_ResolvedTransform.position : Vector3.zero);
            var direction = m_Direction.Value != Vector3.zero ? m_Direction.Value : (m_ResolvedTransform != null ? m_ResolvedTransform.forward : Vector3.forward);

            RaycastHit hit;
            var hitDetected = Physics.Raycast(origin, direction.normalized, out hit, m_MaxDistance.Value, m_LayerMask);
            if (hitDetected) {
                var tagMatches = string.IsNullOrEmpty(m_RequiredTag.Value) || hit.collider.CompareTag(m_RequiredTag.Value);
                if (tagMatches) {
                    m_HitDetected.Value = true;
                    m_HitPoint.Value = hit.point;
                    m_HitNormal.Value = hit.normal;
                    m_HitGameObject.Value = hit.collider.gameObject;
                    m_HitDistance.Value = hit.distance;
                } else {
                    m_HitDetected.Value = false;
                    m_HitPoint.Value = Vector3.zero;
                    m_HitNormal.Value = Vector3.zero;
                    m_HitGameObject.Value = null;
                    m_HitDistance.Value = 0.0f;
                }
            } else {
                m_HitDetected.Value = false;
                m_HitPoint.Value = Vector3.zero;
                m_HitNormal.Value = Vector3.zero;
                m_HitGameObject.Value = null;
                m_HitDistance.Value = 0.0f;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Origin = null;
            m_Direction = null;
            m_MaxDistance = Mathf.Infinity;
            m_LayerMask = -1;
            m_RequiredTag = "";
            m_HitDetected = null;
            m_HitPoint = null;
            m_HitNormal = null;
            m_HitGameObject = null;
            m_HitDistance = null;
        }
    }
}
#endif