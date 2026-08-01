/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Events
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UIElements;
    using InvokeUnityEventAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.InvokeUnityEvent;

    /// <summary>
    /// Implements TypeControlBase for the InvokeUnityEvent task.
    /// </summary>
    [ControlType(typeof(InvokeUnityEventAction))]
    public class InvokeUnityEventControl : ConditionalTypeControlBase<InvokeUnityEventAction>
    {
        private const string c_UnityEventFieldName = "m_UnityEvent";
        private static readonly BindingFlags s_FieldBindings = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, InvokeUnityEventAction target)
        {
            var field = typeof(InvokeUnityEventAction).GetField(c_UnityEventFieldName, s_FieldBindings);
            if (field == null) {
                return new Label($"Error: Unable to find the {c_UnityEventFieldName} field.");
            }

            var unityEvent = field.GetValue(target) as UnityEvent;
            if (unityEvent == null) {
                unityEvent = new UnityEvent();
                field.SetValue(target, unityEvent);
            }

            return UnityEventControlUtility.Create(unityEvent, "Unity Event", (UnityEvent value) =>
            {
                field.SetValue(target, value);
                return input.OnChangeEvent == null || input.OnChangeEvent(value);
            });
        }
    }

    /// <summary>
    /// Utility methods for drawing UnityEvent fields.
    /// </summary>
    internal static class UnityEventControlUtility
    {
        private const string c_UnityEventFieldName = "m_UnityEvent";

        /// <summary>
        /// Creates a UnityEvent control.
        /// </summary>
        /// <param name="unityEvent">The UnityEvent being edited.</param>
        /// <param name="label">The label that should be drawn.</param>
        /// <param name="onChangeEvent">Callback invoked when the UnityEvent changes.</param>
        /// <returns>The created control.</returns>
        public static VisualElement Create(UnityEvent unityEvent, string label, System.Func<UnityEvent, bool> onChangeEvent)
        {
            var root = ConditionalFieldControlUtility.CreateVerticalLayout();
            var proxy = ScriptableObject.CreateInstance<UnityEventProxy>();
            // HideAndDontSave includes NotEditable, which disables the UnityEvent drawer.
            proxy.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            proxy.Event = unityEvent;

            var serializedObject = new SerializedObject(proxy);
            serializedObject.Update();
            var property = serializedObject.FindProperty(c_UnityEventFieldName);
            var eventField = new PropertyField(property, string.IsNullOrEmpty(label) ? "Unity Event" : label);
            eventField.RegisterValueChangeCallback(c =>
            {
                if (proxy == null) {
                    return;
                }

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                onChangeEvent?.Invoke(proxy.Event);
            });
            eventField.Bind(serializedObject);
            root.Add(eventField);
            root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (proxy != null) {
                    Object.DestroyImmediate(proxy);
                }
            });

            return root;
        }
    }

    /// <summary>
    /// Unity object used to draw UnityEvent with the native Unity inspector UI.
    /// </summary>
    internal class UnityEventProxy : ScriptableObject
    {
        [Tooltip("The UnityEvent that should be invoked.")]
        [SerializeField] private UnityEvent m_UnityEvent = new UnityEvent();

        /// <summary>
        /// The UnityEvent being edited.
        /// </summary>
        public UnityEvent Event
        {
            get => m_UnityEvent;
            set => m_UnityEvent = value;
        }
    }
}