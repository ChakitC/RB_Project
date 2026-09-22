using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Specifies the built-in Unity Editor layout to switch to.
    /// </summary>
    public enum UnityLayout
    {
        Default,
        TwoByThree,
        FourSplit,
        Wide,
        Tall,
    }

    /// <summary>
    /// Action Item that switches the Unity Editor layout when executed.
    /// Set <see cref="layout"/> in the Inspector to choose which layout to apply.
    /// </summary>
    public class ChangeUnityLayoutAction : ActionItem
    {
        [Tooltip("The Unity Editor layout to switch to when this action is executed.")]
        public UnityLayout layout = UnityLayout.Default;

        public override string ActionPath => "Examples";

        public override string GetDetailDisplay()
        {
            // Return the friendly layout name for display in the TreeView subtitle
            return GetLayoutName(layout);
        }

        public override void Execute()
        {
            string layoutName = GetLayoutName(layout);
            EditorUtility.LoadWindowLayout(layoutName);
        }

        static string GetLayoutName(UnityLayout unityLayout)
        {
            return unityLayout switch
            {
                UnityLayout.TwoByThree => "2 by 3",
                UnityLayout.FourSplit  => "4 Split",
                UnityLayout.Wide       => "Wide",
                UnityLayout.Tall       => "Tall",
                _                      => "Default",
            };
        }
    }
}
