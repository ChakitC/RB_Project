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
    [Opsive.Shared.Utility.Description("Performs box/sphere/capsule cast with direction, distance, and hit detection.")]
    public class CastShape : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the cast shape type.
        /// </summary>
        public enum CastType
        {
            Box,
            Sphere,
            Capsule
        }

        [Tooltip("The cast type.")]
        [SerializeField] protected CastType m_CastType = CastType.Sphere;
        [Tooltip("The origin of the cast.")]
        [SerializeField] protected SharedVariable<Vector3> m_Origin;
        [Tooltip("The direction of the cast.")]
        [SerializeField] protected SharedVariable<Vector3> m_Direction;
        [Tooltip("The cast distance.")]
        [SerializeField] protected SharedVariable<float> m_Distance = 10.0f;
        [Tooltip("The size of the cast shape (radius for sphere, halfExtents for box, radius/height for capsule).")]
        [SerializeField] protected SharedVariable<Vector3> m_Size = Vector3.one;
        [Tooltip("The layer mask to filter hits.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;
        [Tooltip("Whether a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_HitDetected;
        [Tooltip("The hit point if a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_HitPoint;
        [Tooltip("The hit normal if a hit was detected.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_HitNormal;
        [Tooltip("The GameObject that was hit.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_HitGameObject;

        /// <summary>
        /// Performs the shape cast.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var origin = m_Origin.Value != Vector3.zero ? m_Origin.Value : (m_ResolvedTransform != null ? m_ResolvedTransform.position : Vector3.zero);
            var direction = m_Direction.Value != Vector3.zero ? m_Direction.Value : (m_ResolvedTransform != null ? m_ResolvedTransform.forward : Vector3.forward);

            RaycastHit hit = default(RaycastHit);
            bool hitDetected = false;

            switch (m_CastType) {
                case CastType.Box:
                    hitDetected = Physics.BoxCast(origin, m_Size.Value * 0.5f, direction.normalized, out hit, Quaternion.identity, m_Distance.Value, m_LayerMask);
                    break;
                case CastType.Sphere:
                    hitDetected = Physics.SphereCast(origin, m_Size.Value.x, direction.normalized, out hit, m_Distance.Value, m_LayerMask);
                    break;
                case CastType.Capsule:
                    var point1 = origin + Vector3.up * m_Size.Value.y;
                    var point2 = origin - Vector3.up * m_Size.Value.y;
                    hitDetected = Physics.CapsuleCast(point1, point2, m_Size.Value.x, direction.normalized, out hit, m_Distance.Value, m_LayerMask);
                    break;
                default:
                    hitDetected = false;
                    break;
            }

            if (hitDetected) {
                m_HitDetected.Value = true;
                m_HitPoint.Value = hit.point;
                m_HitNormal.Value = hit.normal;
                m_HitGameObject.Value = hit.collider.gameObject;
            } else {
                m_HitDetected.Value = false;
                m_HitPoint.Value = Vector3.zero;
                m_HitNormal.Value = Vector3.zero;
                m_HitGameObject.Value = null;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_CastType = CastType.Sphere;
            m_Origin = null;
            m_Direction = null;
            m_Distance = 10.0f;
            m_Size = Vector3.one;
            m_LayerMask = -1;
            m_HitDetected = null;
            m_HitPoint = null;
            m_HitNormal = null;
            m_HitGameObject = null;
        }
    }
}
#endif