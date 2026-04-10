#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Clones a GameObject with optional position offset, rotation offset, scale multiplier, and component copying.")]
    public class CloneGameObject : TargetGameObjectAction
    {
        [Tooltip("The position offset for the clone.")]
        [SerializeField] protected SharedVariable<Vector3> m_PositionOffset = Vector3.zero;
        [Tooltip("The rotation offset for the clone (Euler angles in degrees).")]
        [SerializeField] protected SharedVariable<Vector3> m_RotationOffset = Vector3.zero;
        [Tooltip("The scale multiplier for the clone.")]
        [SerializeField] protected SharedVariable<Vector3> m_ScaleMultiplier = Vector3.one;
        [Tooltip("The new parent for the clone. If null, no parent is assigned.")]
        [SerializeField] protected SharedVariable<Transform> m_NewParent;
        [Tooltip("Should all components be copied?")]
        [SerializeField] protected SharedVariable<bool> m_CopyComponents = false;
        [Tooltip("The cloned GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_ClonedObject;

        /// <summary>
        /// Clones the GameObject.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            // Clone the GameObject.
            var cloned = UnityEngine.Object.Instantiate(m_ResolvedGameObject);
            cloned.transform.position = m_ResolvedTransform.TransformPoint(m_PositionOffset.Value);
            cloned.transform.rotation = m_ResolvedTransform.rotation * Quaternion.Euler(m_RotationOffset.Value);
            cloned.transform.localScale = Vector3.Scale(m_ResolvedTransform.localScale, m_ScaleMultiplier.Value);

            if (m_NewParent.Value != null) {
                cloned.transform.SetParent(m_NewParent.Value, true);
            }

            // Copy all components if requested.
            if (m_CopyComponents.Value) {
                var sourceComponents = m_ResolvedGameObject.GetComponents<Component>();
                foreach (var sourceComponent in sourceComponents) {
                    // Skip Transform as it's already handled.
                    if (sourceComponent is Transform) {
                        continue;
                    }

                    var componentType = sourceComponent.GetType();
                    var clonedComponent = cloned.GetComponent(componentType);
                    if (clonedComponent == null) {
                        clonedComponent = cloned.AddComponent(componentType);
                    }
                    // Copy component values using Unity's CopySerialized.
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.CopySerialized(sourceComponent, clonedComponent);
#else
                    CopyComponentValues(sourceComponent, clonedComponent);
#endif
                }
            }

            m_ClonedObject.Value = cloned;
            return TaskStatus.Success;
        }

        /// <summary>
        /// Copies component values from source to destination at runtime.
        /// </summary>
        /// <param name="source">The source component.</param>
        /// <param name="destination">The destination component.</param>
        private void CopyComponentValues(Component source, Component destination)
        {
            if (source == null || destination == null) {
                return;
            }

            var sourceType = source.GetType();
            var destinationType = destination.GetType();
            if (sourceType != destinationType) {
                return;
            }

            // Copy all fields (public and private with SerializeField).
            var fields = sourceType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields) {
                // Skip constants and readonly fields.
                if (field.IsLiteral || field.IsInitOnly) {
                    continue;
                }

                if (field.IsPublic || field.GetCustomAttributes(typeof(SerializeField), false).Length > 0) {
                    field.SetValue(destination, field.GetValue(source));
                }
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_PositionOffset = Vector3.zero;
            m_RotationOffset = Vector3.zero;
            m_ScaleMultiplier = Vector3.one;
            m_NewParent = null;
            m_CopyComponents = false;
            m_ClonedObject = null;
        }
    }
}
#endif