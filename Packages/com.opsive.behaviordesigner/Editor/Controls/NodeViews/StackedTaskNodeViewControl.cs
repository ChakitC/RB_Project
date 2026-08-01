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

            private int m_Index;
            private Image m_ActiveImage;
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
                m_ActiveImage = new Image();
                m_ActiveImage.image = Shared.Editor.Utility.EditorUtility.LoadAsset<Texture>(EditorGUIUtility.isProSkin ? c_DarkActiveIconGUID : c_LightActiveIconGUID);
                m_ActiveImage.style.width = 16;
                m_ActiveImage.style.height = 16;
                m_ActiveImage.style.display = DisplayStyle.None;
                horizontalLayout.Add(m_ActiveImage);

                Add(horizontalLayout);
            }

            /// <summary>
            /// Updates the status of the task.
            /// </summary>
            /// <param name="activeIndex">The index that is active.</param>
            public void UpdateStatus(int activeIndex)
            {
                m_ActiveImage.style.display = (m_Index == activeIndex ? DisplayStyle.Flex : DisplayStyle.None);
                if (m_Index == activeIndex) {
                    if (Application.isPlaying) {
                        m_CurrentRotation += c_ActiveIconRotationSpeed;
                        m_ActiveImage.style.rotate = new Rotate(Angle.Degrees(m_CurrentRotation));
                    }
                } else {
                    m_CurrentRotation = 0f;
                    m_ActiveImage.style.rotate = new Rotate(Angle.Degrees(0f));
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
            TaskStatus status;
            if ((status = base.UpdateNodeInternal()) == TaskStatus.Running && m_StackedTask.Tasks.Length > 1) {
                activeIndex = m_StackedTask.ActiveIndex;
                for (int i = 0; i < m_TaskViews.Length; ++i) {
                    m_TaskViews[i].UpdateStatus(activeIndex);
                }
            } else {
                for (int i = 0; i < m_TaskViews.Length; ++i) {
                    m_TaskViews[i].UpdateStatus(activeIndex);
                }
            }
            return status;
        }
    }
}
#endif
