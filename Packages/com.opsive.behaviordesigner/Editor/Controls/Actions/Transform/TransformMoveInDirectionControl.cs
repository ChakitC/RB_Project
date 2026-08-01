/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.Transform
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using TransformMoveInDirection = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks.MoveInDirection;

    /// <summary>
    /// Implements TypeControlBase for the Transform MoveInDirection task.
    /// </summary>
    [ControlType(typeof(TransformMoveInDirection))]
    public class TransformMoveInDirectionControl : ConditionalTypeControlBase<TransformMoveInDirection>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, TransformMoveInDirection target)
        {
            target.MigrateLegacyFields();

            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var directionContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var directionMode = ConditionalFieldControlUtility.GetEnumValue(target, "m_DirectionMode", TransformMoveInDirection.DirectionMode.Forward);
                ConditionalFieldControlUtility.SetDisplay(directionContainer, directionMode != TransformMoveInDirection.DirectionMode.Forward);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_TargetGameObject", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_DirectionMode", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Direction", directionContainer);
            verticalLayout.Add(directionContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Speed", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_Acceleration", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_MaxSpeed", verticalLayout);

            UpdateVisibility();
            return verticalLayout;
        }
    }
}