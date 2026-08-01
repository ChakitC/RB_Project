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
    [Opsive.Shared.Utility.Description("Sets physics material properties (friction, bounciness, combine mode).")]
    public class MaterialControl : TargetGameObjectAction
    {
        [Tooltip("The physics material to apply.")]
#if UNITY_6000_0_OR_NEWER
        [SerializeField] protected PhysicsMaterial m_PhysicMaterial;
#else
        [SerializeField] protected PhysicMaterial m_PhysicMaterial;
#endif
        [Tooltip("The dynamic friction (0-1).")]
        [SerializeField] protected SharedVariable<float> m_DynamicFriction = 0.6f;
        [Tooltip("The static friction (0-1).")]
        [SerializeField] protected SharedVariable<float> m_StaticFriction = 0.6f;
        [Tooltip("The bounciness (0-1).")]
        [SerializeField] protected SharedVariable<float> m_Bounciness = 0.0f;
#if UNITY_6000_0_OR_NEWER
        [Tooltip("The friction combine mode.")]
        [SerializeField] protected PhysicsMaterialCombine m_FrictionCombine = PhysicsMaterialCombine.Average;
        [Tooltip("The bounce combine mode.")]
        [SerializeField] protected PhysicsMaterialCombine m_BounceCombine = PhysicsMaterialCombine.Average;

        private PhysicsMaterial m_CreatedMaterial;
#else
        [Tooltip("The friction combine mode.")]
        [SerializeField] protected PhysicMaterialCombine m_FrictionCombine = PhysicMaterialCombine.Average;
        [Tooltip("The bounce combine mode.")]
        [SerializeField] protected PhysicMaterialCombine m_BounceCombine = PhysicMaterialCombine.Average;

        private PhysicMaterial m_CreatedMaterial;
#endif
        private Collider m_ResolvedCollider;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCollider = m_ResolvedGameObject.GetComponent<Collider>();
        }

        /// <summary>
        /// Updates the physics material.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCollider == null) {
                return TaskStatus.Success;
            }

            if (m_PhysicMaterial != null) {
                m_ResolvedCollider.material = m_PhysicMaterial;
            } else {
                if (m_CreatedMaterial == null) {
#if UNITY_6000_0_OR_NEWER
                    m_CreatedMaterial = new PhysicsMaterial("DynamicMaterial");
#else
                    m_CreatedMaterial = new PhysicMaterial("DynamicMaterial");
#endif
                }

                m_CreatedMaterial.dynamicFriction = Mathf.Clamp01(m_DynamicFriction.Value);
                m_CreatedMaterial.staticFriction = Mathf.Clamp01(m_StaticFriction.Value);
                m_CreatedMaterial.bounciness = Mathf.Clamp01(m_Bounciness.Value);
                m_CreatedMaterial.frictionCombine = m_FrictionCombine;
                m_CreatedMaterial.bounceCombine = m_BounceCombine;

                m_ResolvedCollider.material = m_CreatedMaterial;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_PhysicMaterial = null;
            m_DynamicFriction = 0.6f;
            m_StaticFriction = 0.6f;
            m_Bounciness = 0.0f;
#if UNITY_6000_0_OR_NEWER
            m_FrictionCombine = PhysicsMaterialCombine.Average;
            m_BounceCombine = PhysicsMaterialCombine.Average;
#else
            m_FrictionCombine = PhysicMaterialCombine.Average;
            m_BounceCombine = PhysicMaterialCombine.Average;
#endif
        }
    }
}
#endif