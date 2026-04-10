#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CameraTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Camera")]
    [Opsive.Shared.Utility.Description("Converts a screen position to a world position using the camera.")]
    public class ScreenToWorldPoint : TargetGameObjectAction
    {
        [Tooltip("The screen position to convert (x, y).")]
        [SerializeField] protected SharedVariable<Vector2> m_ScreenPosition;
        [Tooltip("The distance from the camera for the world position.")]
        [SerializeField] protected SharedVariable<float> m_Distance = 10.0f;
        [Tooltip("The resulting world position.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_WorldPosition;

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
                    Debug.LogError("ScreenToWorldPoint: Unable to find a camera.");
                }
            }
        }

        /// <summary>
        /// Converts the screen position to world position.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCamera == null) {
                return TaskStatus.Success;
            }

            m_WorldPosition.Value = m_ResolvedCamera.ScreenToWorldPoint(new Vector3(m_ScreenPosition.Value.x, m_ScreenPosition.Value.y, m_Distance.Value));
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_ScreenPosition = null;
            m_Distance = 10.0f;
            m_WorldPosition = null;
        }
    }
}
#endif