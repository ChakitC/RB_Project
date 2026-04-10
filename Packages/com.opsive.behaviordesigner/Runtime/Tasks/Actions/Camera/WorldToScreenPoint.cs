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
    [Opsive.Shared.Utility.Description("Converts a world position to a screen position using the camera.")]
    public class WorldToScreenPoint : TargetGameObjectAction
    {
        [Tooltip("The world position to convert.")]
        [SerializeField] protected SharedVariable<Vector3> m_WorldPosition;
        [Tooltip("The resulting screen position (x, y).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_ScreenPosition;

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
                    Debug.LogError("WorldToScreenPoint: Unable to find a camera.");
                }
            }
        }

        /// <summary>
        /// Converts the world position to screen position.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCamera == null) {
                return TaskStatus.Success;
            }

            var screenPos = m_ResolvedCamera.WorldToScreenPoint(m_WorldPosition.Value);
            m_ScreenPosition.Value = new Vector2(screenPos.x, screenPos.y);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_WorldPosition = null;
            m_ScreenPosition = null;
        }
    }
}
#endif