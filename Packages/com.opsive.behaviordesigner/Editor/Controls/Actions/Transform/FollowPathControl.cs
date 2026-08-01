/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Transform
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using FollowPathAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.FollowPath;

    /// <summary>
    /// Implements TypeControlBase for the FollowPath task.
    /// </summary>
    [ControlType(typeof(FollowPathAction))]
    public class FollowPathControl : ConditionalTypeControlBase<FollowPathAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, FollowPathAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var waypointPositionsContainer = ConditionalFieldControlUtility.CreateContainer();
            var reverseContainer = ConditionalFieldControlUtility.CreateContainer();
            var lookAtWaypointContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(waypointPositionsContainer, !ConditionalFieldControlUtility.HasListItems(target, "m_Waypoints"));
                ConditionalFieldControlUtility.SetDisplay(reverseContainer, !ConditionalFieldControlUtility.GetValue(target, "m_LoopPath", false));
                ConditionalFieldControlUtility.SetDisplay(lookAtWaypointContainer, ConditionalFieldControlUtility.GetValue(target, "m_LookAtWaypoint", false));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject");
            var refreshWaypoints = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_Waypoints", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Waypoints", verticalLayout, (object obj) => {
                refreshWaypoints?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_WaypointPositions", waypointPositionsContainer);
            verticalLayout.Add(waypointPositionsContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_MovementSpeed", "m_ArrivedDistance");
            var refreshLoopPath = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_LoopPath", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_LoopPath", verticalLayout, (object obj) => {
                refreshLoopPath?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_ReverseOnComplete", reverseContainer);
            verticalLayout.Add(reverseContainer);
            var refreshLookAtWaypoint = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_LookAtWaypoint", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_LookAtWaypoint", verticalLayout, (object obj) => {
                refreshLookAtWaypoint?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_RotationSpeed", lookAtWaypointContainer);
            verticalLayout.Add(lookAtWaypointContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}