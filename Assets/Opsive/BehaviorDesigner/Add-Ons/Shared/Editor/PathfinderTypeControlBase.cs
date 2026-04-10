/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Editor
{
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Pathfinding;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls.Types;
    using Opsive.Shared.Utility;
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Base class for any type control that uses a Pathfinder.
    /// </summary>
    public abstract class PathfinderTypeControlBase : TypeControlBase
    {
        /// <summary>
        /// The PathfinderView displays the contents of a task that uses a Pathfinder.
        /// </summary>
        public abstract class PathfinderView : VisualElement
        {
            protected string m_PathfinderTypeKey;
            protected UnityEngine.Object m_UnityObject;
            protected Func<object, bool> m_OnChangeEvent;

            private List<Type> m_PathfinderTypes = new List<Type>();
            private List<string> m_PathfinderTypeNames = new List<string>();

            private int m_PathfinderIndex = 0;
            private VisualElement m_PathfinderContainer;

            /// <summary>
            /// PathfinderView constructor.
            /// </summary>
            /// <param name="unityObject">A reference to the owning Unity Object.</param>
            /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
            /// <param name="pathfinderTypeKey">The key used to store the pathfinder type in EditorPrefs.</param>
            public PathfinderView(UnityEngine.Object unityObject, Func<object, bool> onChangeEvent, string pathfinderTypeKey)
            {
                m_UnityObject = unityObject;
                m_OnChangeEvent = onChangeEvent;
                m_PathfinderTypeKey = pathfinderTypeKey;

                PopulatePathfinderTypes();
            }

            /// <summary>
            /// Initializes the pathfinder for the task.
            /// </summary>
            /// <param name="getPathfinder">Function to get the current pathfinder.</param>
            /// <param name="setPathfinder">Action to set the new pathfinder.</param>
            protected void InitializePathfinder(Func<Pathfinder> getPathfinder, Action<Pathfinder> setPathfinder)
            {
                var pathfinder = getPathfinder();
                if (pathfinder == null) {
                    pathfinder = Activator.CreateInstance(m_PathfinderTypes[m_PathfinderIndex]) as Pathfinder;
                    setPathfinder(pathfinder);
                    m_OnChangeEvent?.Invoke(pathfinder);
                }

                // Ensure the pathfinder index matches.
                if (pathfinder.GetType() != m_PathfinderTypes[m_PathfinderIndex]) {
                    for (int i = 0; i < m_PathfinderTypeNames.Count; ++i) {
                        if (pathfinder.GetType() == m_PathfinderTypes[i]) {
                            m_PathfinderIndex = i;
                            break;
                        }
                    }
                }

                // Add a popup with the different pathfinding choices.
                AddTitleLabel("Pathfinder", string.Empty);
                var pathfinderTypePopup = new PopupField<string>();
                pathfinderTypePopup.label = "Pathfinder";
                pathfinderTypePopup.tooltip = "Specifies the base pathfinding implementation that should be used.";
                pathfinderTypePopup.choices = m_PathfinderTypeNames;
                pathfinderTypePopup.value = m_PathfinderTypeNames[m_PathfinderIndex];
                pathfinderTypePopup.RegisterValueChangedCallback(c =>
                {
                    m_PathfinderIndex = pathfinderTypePopup.index;
                    EditorPrefs.SetString(m_PathfinderTypeKey, m_PathfinderTypes[m_PathfinderIndex].FullName);
                    var newPathfinder = Activator.CreateInstance(m_PathfinderTypes[m_PathfinderIndex]) as Pathfinder;
                    setPathfinder(newPathfinder);

                    m_PathfinderContainer.Clear();
                    FieldInspectorView.AddFields(m_UnityObject, newPathfinder, MemberVisibility.Public, m_PathfinderContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });

                    m_OnChangeEvent?.Invoke(newPathfinder);
                });
                Add(pathfinderTypePopup);

                // Show any pathfinder fields.
                m_PathfinderContainer = new VisualElement();
                m_PathfinderContainer.style.marginBottom = 4;
                FieldInspectorView.AddFields(m_UnityObject, pathfinder, MemberVisibility.Public, m_PathfinderContainer, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
                Add(m_PathfinderContainer);
            }

            /// <summary>
            /// Gets all of the pathfinding implementations available.
            /// </summary>
            private void PopulatePathfinderTypes()
            {
                m_PathfinderTypes.Clear();
                m_PathfinderTypeNames.Clear();
                var defaultPathfinder = EditorPrefs.GetString(m_PathfinderTypeKey);
                var types = UnitOptions.GetAllTypes();
                for (int i = 0; i < types.Length; ++i) {
                    if (typeof(Pathfinder).IsAssignableFrom(types[i]) && !types[i].IsAbstract) {
                        m_PathfinderTypes.Add(types[i]);
                        m_PathfinderTypeNames.Add(types[i].Name.Replace("Pathfinder", ""));
                        if (string.Equals(defaultPathfinder, types[i].FullName)) {
                            m_PathfinderIndex = m_PathfinderTypeNames.Count - 1;
                        }
                    }
                }
            }

            /// <summary>
            /// Adds a title label with the specified text.
            /// </summary>
            /// <param name="text">The label text.</param>
            /// <param name="tooltip">The label tooltip.</param>
            protected void AddTitleLabel(string text, string tooltip)
            {
                var label = new Label();
                label.enableRichText = true;
                label.style.marginTop = 5;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.text = $"<font-weight=700><size=+1>{text}</size></font-weight>";
                if (!string.IsNullOrEmpty(tooltip)) {
                    label.tooltip = tooltip;
                }
                Add(label);
            }
        }

        /// <summary>
        /// Does the control use a label?
        /// </summary>
        public override bool UseLabel { get { return false; } }
    }
}