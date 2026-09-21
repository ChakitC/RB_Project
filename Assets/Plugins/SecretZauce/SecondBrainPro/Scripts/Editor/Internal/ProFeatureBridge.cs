using SecretZauce.SecondBrain.Editor;
using UnityEditor;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    [InitializeOnLoad]
    public static class ProFeatureBridge
    {
        static ProFeatureBridge()
        {
            ProFeature.RegisterResolver(new ProFeatureProvider());
        }
    }
}
