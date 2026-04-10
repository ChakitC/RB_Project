#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics2D")]
    [Opsive.Shared.Utility.Description("Performs 2D raycast with layer mask, tag filter, and hit result processing.")]
    public class Raycast2D : TargetGameObjectAction
    {
        [Tooltip("The origin of the raycast.")]
        [SerializeField] protected SharedVariable<Vector2> m_Origin;
        [Tooltip("The direction of the raycast.")]
        [SerializeField] protected SharedVariable<Vector2> m_Direction;
        [Tooltip("The maximum distance of the raycast.")]
        [SerializeField] protected SharedVariable<float> m_MaxDistance = Mathf.Infinity;
        [Tooltip("The layer mask to filter hits.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;
        [Tooltip("The tag to filter hits. Leave empty to ignore tag filtering.")]
        [SerializeField] protected SharedVariable<string> m_RequiredTag = "";
        [Tooltip("Whether a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_HitDetected;
        [Tooltip("The hit point if a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_HitPoint;
        [Tooltip("The hit normal if a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_HitNormal;
        [Tooltip("The GameObject that was hit.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_HitGameObject;
        [Tooltip("The distance to the hit point.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_HitDistance;

        /// <summary>
        /// Performs the 2D raycast with filtering.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var origin = m_Origin.Value == Vector2.zero ? new Vector2(m_ResolvedTransform.position.x, m_ResolvedTransform.position.y) : m_Origin.Value;
            var direction = m_Direction.Value == Vector2.zero ? new Vector2(m_ResolvedTransform.right.x, m_ResolvedTransform.right.y) : m_Direction.Value;
            var hit = Physics2D.Raycast(origin, direction.normalized, m_MaxDistance.Value, m_LayerMask);
            if (hit.collider != null) {
                var tagMatches = string.IsNullOrEmpty(m_RequiredTag.Value) || hit.collider.CompareTag(m_RequiredTag.Value);
                if (tagMatches) {
                    m_HitDetected.Value = true;
                    m_HitPoint.Value = hit.point;
                    m_HitNormal.Value = hit.normal;
                    m_HitGameObject.Value = hit.collider.gameObject;
                    m_HitDistance.Value = hit.distance;
                } else {
                    m_HitDetected.Value = false;
                    m_HitPoint.Value = Vector2.zero;
                    m_HitNormal.Value = Vector2.zero;
                    m_HitGameObject.Value = null;
                    m_HitDistance.Value = 0.0f;
                }
            } else {
                m_HitDetected.Value = false;
                m_HitPoint.Value = Vector2.zero;
                m_HitNormal.Value = Vector2.zero;
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