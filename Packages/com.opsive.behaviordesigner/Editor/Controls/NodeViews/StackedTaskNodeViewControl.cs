#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.NodeViews
{
    using Opsive.GraphDesigner.Editor;
    using Opsive.GraphDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Implements TypeControlBase for the StackedTask type.
    /// </summary>
    [ControlType(typeof(StackedTask))]
    public class StackedTaskNodeViewControl : TaskNodeViewControl
    {
        private const float c_ActiveIconRotationSpeed = 5;

        /// <summary>
        /// Displays information about the nested task within the Stacked Task.
        /// </summary>
        private class TaskView : VisualElement
        {
            private const string c_DarkActiveIconGUID = "1230b934cbd748345b13125468a34720";
            private const string c_LightActiveIconGUID = "e57f179ee476f274dbe537179e67bf04";
            private const string c_DarkSuccessIconGUID = "240eed9b6e6dc004f94216f1e9fcc390";
            private const string c_LightSuccessIconGUID = "cf3f27e8ca1f20f4680890e078c7613a";
            private const string c_DarkFailureIconGUID = "8d159db7a8da43e41a50a77e43cfd6ba";
            private const string c_LightFailureIconGUID = "c3622912d9f7bcd41a54a95add672423";

            private int m_Index;
            private Image m_StatusImage;
            private Texture m_ActiveIcon;
            private Texture m_SuccessIcon;
            private Texture m_FailureIcon;
            private float m_CurrentRotation;

            /// <summary>
            /// TaskView constructor.
            /// </summary>
            /// <param name="index">The index of the task.</param>
            /// <param name="task">A reference to the task.</param>
            /// <param name="customName">The custom task name.</param>
            public TaskView(int index, Task task, string customName)
            {
                m_Index = index;

                var horizontalLayout = new VisualElement();
                horizontalLayout.AddToClassList("horizontal-layout");
                horizontalLayout.style.height = 18;
                var label = new Label(ContainedNodeNameUtility.GetDisplayName(customName, task.ToString()));
                label.style.flexGrow = 1;
                horizontalLayout.Add(label);
                m_ActiveIcon = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkActiveIconGUID : c_LightActiveIconGUID);
                m_SuccessIcon = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkSuccessIconGUID : c_LightSuccessIconGUID);
                m_FailureIcon = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkFailureIconGUID : c_LightFailureIconGUID);
                m_StatusImage = new Image();
                m_StatusImage.style.width = 16;
                m_StatusImage.style.height = 16;
                m_StatusImage.style.display = DisplayStyle.None;
                horizontalLayout.Add(m_StatusImage);

                Add(horizontalLayout);
            }

            /// <summary>
            /// Updates the status of the task.
            /// </summary>
            /// <param name="status">The latest execution status of the task.</param>
            /// <param name="activeIndex">The index of the active task.</param>
            public void UpdateStatus(TaskStatus status, int activeIndex)
            {
                Texture statusIcon = null;
                if (status == TaskStatus.Success) {
                    statusIcon = m_SuccessIcon;
                } else if (status == TaskStatus.Failure) {
                    statusIcon = m_FailureIcon;
                } else if (m_Index == activeIndex) {
                    statusIcon = m_ActiveIcon;
                }

                m_StatusImage.image = statusIcon;
                m_StatusImage.style.display = statusIcon != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (statusIcon == m_ActiveIcon) {
                    if (Application.isPlaying) {
                        m_CurrentRotation += c_ActiveIconRotationSpeed;
                        m_StatusImage.style.rotate = new Rotate(Angle.Degrees(m_CurrentRotation));
                    }
                } else {
                    m_CurrentRotation = 0f;
                    m_StatusImage.style.rotate = new Rotate(Angle.Degrees(0f));
                }
            }
        }

        private StackedTask m_StackedTask;
        private TaskView[] m_TaskViews;

        /// <summary>
        /// Addes the UIElements for the specified runtime node to the editor Node within the graph.
        /// </summary>
        /// <param name="graphWindow">A reference to the GraphWindow.</param>
        /// <param name="parent">The parent UIElement that should contain the node UIElements.</param>
        /// <param name="node">The node that the control represents.</param>
        public override void AddNodeView(GraphWindow graphWindow, VisualElement parent, object node)
        {
            base.AddNodeView(graphWindow, parent, node);

            m_StackedTask = node as StackedTask;
            if (m_StackedTask.Tasks == null) {
                return;
            }

            var tasks = m_StackedTask.Tasks;
            var containedNodeNames = GetContainedNodeNames(graphWindow, m_StackedTask);
            m_TaskViews = new TaskView[tasks.Length];
            for (int i = 0; i < tasks.Length; ++i) {
                var task = m_StackedTask.Tasks[i];
                // The task no longer exists. Replace it.
                if (task == null) {
                    tasks[i] = new UnknownTask(string.Empty);
                    m_StackedTask.Tasks = tasks;
                }
                m_TaskViews[i] = new TaskView(i, m_StackedTask.Tasks[i], ContainedNodeNameUtility.GetName(containedNodeNames, i));
                parent.Add(m_TaskViews[i]);
            }
        }

        /// <summary>
        /// Returns the names assigned to the contained tasks.
        /// </summary>
        /// <param name="graphWindow">A reference to the graph window.</param>
        /// <param name="stackedTask">The stacked task that owns the contained tasks.</param>
        /// <returns>The contained task names.</returns>
        private static string[] GetContainedNodeNames(GraphWindow graphWindow, StackedTask stackedTask)
        {
            if (graphWindow == null || graphWindow.Graph == null || graphWindow.Graph.LogicNodeProperties == null || stackedTask == null) {
                return null;
            }

            var nodeIndex = stackedTask.Index;
            if (nodeIndex >= graphWindow.Graph.LogicNodeProperties.Length || graphWindow.Graph.LogicNodeProperties[nodeIndex] == null) {
                return null;
            }

            return graphWindow.Graph.LogicNodeProperties[nodeIndex].Data.ContainedNodeNames;
        }

        /// <summary>
        /// Internal method which updates the node with the current execution status.
        /// </summary>
        /// <returns>The status of the task.</returns>
        protected override TaskStatus UpdateNodeInternal()
        {
            var activeIndex = -1;
            var status = base.UpdateNodeInternal();
            if (status == TaskStatus.Running) {
                activeIndex = m_StackedTask.ActiveIndex;
            }
            for (int i = 0; i < m_TaskViews.Length; ++i) {
                m_TaskViews[i].UpdateStatus(m_StackedTask.Tasks[i].Status, activeIndex);
            }
            return status;
        }
    }
}
#endif