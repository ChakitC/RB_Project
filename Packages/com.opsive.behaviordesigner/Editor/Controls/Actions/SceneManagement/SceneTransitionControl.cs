/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.Actions.SceneManagement
{
    using Opsive.BehaviorDesigner.Editor.Controls;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;
    using SceneTransitionTask = Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SceneManagementTasks.SceneTransition;

    /// <summary>
    /// Implements TypeControlBase for the SceneTransition task.
    /// </summary>
    [ControlType(typeof(SceneTransitionTask))]
    public class SceneTransitionControl : ConditionalTypeControlBase<SceneTransitionTask>
    {
        /// <summary>
        /// Returns the control for the typed target.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <param name="target">The typed target.</param>
        /// <returns>The created control.</returns>
        protected override VisualElement GetControl(TypeControlInput input, SceneTransitionTask target)
        {
            target.MigrateLegacyFields();

            var verticalLayout = ConditionalFieldControlUtility.CreateVerticalLayout();
            var sceneNameContainer = ConditionalFieldControlUtility.CreateContainer();
            var sceneBuildIndexContainer = ConditionalFieldControlUtility.CreateContainer();
            var fadeContainer = ConditionalFieldControlUtility.CreateContainer();

            void UpdateVisibility()
            {
                var sceneSource = ConditionalFieldControlUtility.GetEnumValue(target, "m_SceneSource", SceneTransitionTask.SceneSource.SceneName);
                var usesFade = ConditionalFieldControlUtility.GetValue(target, "m_UseFade", true);

                ConditionalFieldControlUtility.SetDisplay(sceneNameContainer, sceneSource == SceneTransitionTask.SceneSource.SceneName);
                ConditionalFieldControlUtility.SetDisplay(sceneBuildIndexContainer, sceneSource == SceneTransitionTask.SceneSource.BuildIndex);
                ConditionalFieldControlUtility.SetDisplay(fadeContainer, usesFade);
            }

            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SceneSource", verticalLayout, (object obj) => {
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SceneName", sceneNameContainer);
            verticalLayout.Add(sceneNameContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_SceneBuildIndex", sceneBuildIndexContainer);
            verticalLayout.Add(sceneBuildIndexContainer);
            var refreshUseFade = ConditionalFieldControlUtility.WatchSharedVariableField(verticalLayout, target, "m_UseFade", UpdateVisibility);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_UseFade", verticalLayout, (object obj) => {
                refreshUseFade?.Invoke();
                UpdateVisibility();
            });
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FadeDuration", fadeContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_FadeProgress", fadeContainer);
            verticalLayout.Add(fadeContainer);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_LoadingProgress", verticalLayout);
            ConditionalFieldControlUtility.AddWatchedField(input, target, "m_TransitionComplete", verticalLayout);

            UpdateVisibility();
            return verticalLayout;
        }
    }
}