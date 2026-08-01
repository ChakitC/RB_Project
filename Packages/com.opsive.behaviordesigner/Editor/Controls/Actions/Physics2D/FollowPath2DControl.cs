/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Physics2D
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.BehaviorDesigner.Editor.Controls.Actions;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using FollowPath2DAction = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks.FollowPath2D;

    /// <summary>
    /// Implements TypeControlBase for the FollowPath2D task.
    /// </summary>
    [ControlType(typeof(FollowPath2DAction))]
    public class FollowPath2DControl : ConditionalTypeControlBase<FollowPath2DAction>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, FollowPath2DAction target)
        {
            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var waypointPositionsContainer = ConditionalFieldControlUtility.CreateContainer();
            var reverseContainer = ConditionalFieldControlUtility.CreateContainer();
            var lookAtWaypointContainer = ConditionalFieldControlUtility.CreateContainer();
            var dampenDistanceContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                ConditionalFieldControlUtility.SetDisplay(waypointPositionsContainer, !ConditionalFieldControlUtility.HasListItems(target, "m_Waypoints"));
                ConditionalFieldControlUtility.SetDisplay(reverseContainer, !ConditionalFieldControlUtility.GetValue(target, "m_LoopPath", false));
                ConditionalFieldControlUtility.SetDisplay(lookAtWaypointContainer, ConditionalFieldControlUtility.GetValue(target, "m_LookAtWaypoint", false));
                ConditionalFieldControlUtility.SetDisplay(dampenDistanceContainer, ConditionalFieldControlUtility.GetValue(target, "m_DampenOnApproach", true));
            }

            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_TargetGameObject");
            var refreshWaypoints = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_Waypoints", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Waypoints", verticalLayout, (object obj) => {
                refreshWaypoints?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_WaypointPositions", waypointPositionsContainer);
            verticalLayout.Add(waypointPositionsContainer);
            ConditionalActionControlBuilder.AddFields(input, target, verticalLayout, "m_MovementForce", "m_ArrivedDistance");
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
            ConditionalActionControlBuilder.AddFields(input, target, lookAtWaypointContainer, "m_RotationSpeed", "m_MaxRotationTorque");
            verticalLayout.Add(lookAtWaypointContainer);
            var refreshDampenOnApproach = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_DampenOnApproach", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_DampenOnApproach", verticalLayout, (object obj) => {
                refreshDampenOnApproach?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_DampenDistance", dampenDistanceContainer);
            verticalLayout.Add(dampenDistanceContainer);
            UpdateVisibility();
            return verticalLayout;
        }
    }
}