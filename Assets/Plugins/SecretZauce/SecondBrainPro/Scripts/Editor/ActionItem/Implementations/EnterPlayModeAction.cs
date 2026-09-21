using UnityEditor;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Action Item that enters Unity Play Mode when executed.
    /// </summary>
    public class EnterPlayModeAction : ActionItem
    {
        public override string ActionPath => "Examples";

        public override string GetDetailDisplay()
        {
            // No configurable option for this action; return an empty detail so nothing is shown.
            return string.Empty;
        }

        public override void Execute()
        {
            EditorApplication.EnterPlaymode();
        }
    }
}
