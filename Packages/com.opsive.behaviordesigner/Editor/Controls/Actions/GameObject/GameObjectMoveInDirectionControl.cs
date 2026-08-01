/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.GameObject
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using GameObjectMoveInDirection = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks.MoveInDirection;

    /// <summary>
    /// Implements TypeControlBase for the GameObject MoveInDirection task.
    /// </summary>
    [ControlType(typeof(GameObjectMoveInDirection))]
    public class GameObjectMoveInDirectionControl : ConditionalTypeControlBase<GameObjectMoveInDirection>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, GameObjectMoveInDirection target)
        {
            target.MigrateLegacyFields();

            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var directionContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var directionMode = ConditionalFieldControlUtility.GetEnumValue(target, "m_DirectionMode", GameObjectMoveInDirection.DirectionMode.Forward);
                ConditionalFieldControlUtility.SetDisplay(directionContainer, directionMode != GameObjectMoveInDirection.DirectionMode.Forward);
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