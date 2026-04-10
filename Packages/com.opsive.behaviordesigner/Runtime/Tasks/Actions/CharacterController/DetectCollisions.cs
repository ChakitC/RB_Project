#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CharacterControllerTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Character Controller")]
    [Opsive.Shared.Utility.Description("Detects CharacterController collisions with filtering, tag/layer checks, and collision reporting.")]
    public class DetectCollisions : TargetGameObjectAction
    {
        [Tooltip("The detection radius (extends beyond controller radius).")]
        [SerializeField] protected SharedVariable<float> m_DetectionRadius = 0.1f;
        [Tooltip("The layer mask to filter collisions.")]
        [SerializeField] protected LayerMask m_CollisionLayerMask = -1;
        [Tooltip("The required tag. Leave empty to ignore tag filtering.")]
        [SerializeField] protected SharedVariable<string> m_RequiredTag = "";
        [Tooltip("The number of collisions detected (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<int> m_CollisionCount;
        [Tooltip("The first colliding GameObject (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_FirstCollision;
        [Tooltip("The list of all colliding GameObjects (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<List<GameObject>> m_AllCollisions;

        private CharacterController m_ResolvedCharacterController;
        private Collider[] m_Colliders = new Collider[20];
        private List<GameObject> m_CollisionList;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            if (m_AllCollisions.Value == null) {
                m_AllCollisions.Value = new List<GameObject>();
            }
            m_CollisionList = m_AllCollisions.Value;
        }

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCharacterController = m_ResolvedGameObject.GetComponent<CharacterController>();
        }

        /// <summary>
        /// Updates the collision detection.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCharacterController == null) {
                m_CollisionCount.Value = 0;
                m_FirstCollision.Value = null;
                m_CollisionList.Clear();
                return TaskStatus.Success;
            }

            m_CollisionList.Clear();

            var totalRadius = m_ResolvedCharacterController.radius + m_DetectionRadius.Value;
            var center = m_ResolvedTransform.position + m_ResolvedCharacterController.center;
            var hitCount = Physics.OverlapSphereNonAlloc(center, totalRadius, m_Colliders, m_CollisionLayerMask);
            GameObject firstCollision = null;

            for (int i = 0; i < hitCount; ++i) {
                if (m_Colliders[i] == null || m_Colliders[i] == m_ResolvedCharacterController) {
                    continue;
                }

                var go = m_Colliders[i].gameObject;
                var tagMatches = string.IsNullOrEmpty(m_RequiredTag.Value) || go.CompareTag(m_RequiredTag.Value);

                if (tagMatches) {
                    m_CollisionList.Add(go);
                    if (firstCollision == null) {
                        firstCollision = go;
                    }
                }
            }

            m_CollisionCount.Value = m_CollisionList.Count;
            m_FirstCollision.Value = firstCollision;
            m_AllCollisions.Value = m_CollisionList;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_DetectionRadius = 0.1f;
            m_CollisionLayerMask = -1;
            m_RequiredTag = "";
            m_CollisionCount = null;
            m_FirstCollision = null;
            m_AllCollisions = null;
        }
    }
}
#endif