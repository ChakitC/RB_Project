#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.CameraTasks
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Camera")]
    [Opsive.Shared.Utility.Description("Checks if the mouse is over a specific GameObject using a raycast from the camera.")]
    public class IsMouseOverObject : TargetGameObjectConditional
    {
        [Tooltip("The GameObject to check if the mouse is over.")]
        [SerializeField] protected SharedVariable<GameObject> m_TargetObject;
        [Tooltip("The maximum distance of the raycast.")]
        [SerializeField] protected SharedVariable<float> m_MaxDistance = Mathf.Infinity;
        [Tooltip("The layer mask to filter hits.")]
        [SerializeField] protected LayerMask m_LayerMask = -1;
        [Tooltip("Should the condition check if mouse is over the object (true) or not over the object (false)?")]
        [SerializeField] protected SharedVariable<bool> m_IsOverObject = true;

        private Camera m_ResolvedCamera;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCamera = m_ResolvedGameObject.GetComponent<Camera>();
            if (m_ResolvedCamera == null) {
                m_ResolvedCamera = Camera.main;
                if (m_ResolvedCamera == null) {
                    Debug.LogError("IsMouseOverObject: Unable to find a camera.");
                }
            }
        }

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (m_TargetObject.Value == null || m_ResolvedCamera == null) {
                return (!m_IsOverObject.Value) ? TaskStatus.Success : TaskStatus.Failure;
            }

            var ray = m_ResolvedCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, m_MaxDistance.Value, m_LayerMask)) {
                var hitTransform = hit.collider.transform;
                var targetTransform = m_TargetObject.Value.transform;
                while (hitTransform != null) {
                    if (hitTransform == targetTransform) {
                        return m_IsOverObject.Value ? TaskStatus.Success : TaskStatus.Failure;
                    }
                    hitTransform = hitTransform.parent;
                }
            }

            return (!m_IsOverObject.Value) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif