using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ASP.Scripts.Editor
{
    partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {
        partial void DrawSimpleShadowSetupEditor(VisualElement root, ASPCharacterPanel characterPanel)
        {
            var aspSimpleShadowSetupGroup = new Foldout();
            aspSimpleShadowSetupGroup.Q<Foldout>().text = "(NEW!) Simple AABB Cube to Remove Self-Shadowing (Without using extra shadow map)";
            aspSimpleShadowSetupGroup.Q<Foldout>().style.fontSize = 15;
            aspSimpleShadowSetupGroup.Q<Foldout>().value = true;
            
            var useSimpleAABBCutOffForCharacterShadowProp = serializedObject.FindProperty("UseSimpleAABBCutOffForCharacterShadow");
        
            var imguiContainer = new IMGUIContainer(() =>
            {
                EditorGUILayout.HelpBox("Turn on gizmo in Unity Editor to visualize the aabb cube", MessageType.Info);
                if (GetFeature<ASPShadowMapFeature>() != null && useSimpleAABBCutOffForCharacterShadowProp.boolValue)
                {
                    EditorGUILayout.HelpBox("You dont need to enable asp character shadow map if all character using aabb cutoff, consider disable it", MessageType.Warning);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Open URP Data", GUILayout.Width(130)))
                            OpenURPDataAsset();
                        GUILayout.Space(8);
                    }
                    GUILayout.Space(11);
                }
            });
            aspSimpleShadowSetupGroup.Add(imguiContainer);

            var simpleAABBCutOffToggle = new PropertyField(useSimpleAABBCutOffForCharacterShadowProp, "Enable");
            simpleAABBCutOffToggle.style.width = new StyleLength(new Length( 100, LengthUnit.Percent));
            simpleAABBCutOffToggle.style.marginTop = 5;
            aspSimpleShadowSetupGroup.Add(simpleAABBCutOffToggle);
            
            var characterCenterInfo = new Label();
            characterCenterInfo.style.width = new StyleLength(new Length( 100, LengthUnit.Percent));
            characterCenterInfo.text = "Character World Position Center : "+ (characterPanel.transform.position + characterPanel.CenterPositionOffset);
            
            var characterCenterOffset = serializedObject.FindProperty("CenterPositionOffset");
            var characterCenterOffsetField = new Vector3Field("Character Center Position Offset");
            characterCenterOffsetField.SetValueWithoutNotify(characterPanel.CenterPositionOffset);
            characterCenterOffsetField.style.width = new StyleLength(new Length( 100, LengthUnit.Percent));
            characterCenterOffsetField.style.marginLeft = -0.5f;
            characterCenterOffsetField.RegisterCallback<ChangeEvent<Vector3>>(e =>
            {
                characterCenterOffset.vector3Value =
                    e.newValue;
                serializedObject.ApplyModifiedProperties();
                characterPanel.UpdateLightDirectionOverrideParam();
                characterCenterInfo.text = "Character World Position Center : "+ (characterPanel.transform.position + characterPanel.CenterPositionOffset);
            });
            
            aspSimpleShadowSetupGroup.Add(characterCenterOffsetField);
            aspSimpleShadowSetupGroup.Add(characterCenterInfo);
            
            var characterCenterCubeSize = serializedObject.FindProperty("_CharacterCenterCubeSize");
            var characterCenterCubeSizeField = new FloatField("Self Shadow Cutoff AABB Size");
            characterCenterCubeSizeField.SetValueWithoutNotify(characterPanel._CharacterCenterCubeSize);
            characterCenterCubeSizeField.style.width = new StyleLength(new Length( 100, LengthUnit.Percent));
            characterCenterCubeSizeField.style.marginLeft = -0.5f;
            characterCenterCubeSizeField.RegisterCallback<ChangeEvent<float>>(e =>
            {
                characterCenterCubeSize.floatValue = e.newValue;
                serializedObject.ApplyModifiedProperties();
                characterPanel.UpdateLightDirectionOverrideParam();
            });
            aspSimpleShadowSetupGroup.Add(characterCenterCubeSizeField);
            
            simpleAABBCutOffToggle.RegisterCallback<ChangeEvent<bool>>(e =>
            {
                useSimpleAABBCutOffForCharacterShadowProp.boolValue = e.newValue;
                serializedObject.ApplyModifiedProperties();
                characterPanel.UpdateLightDirectionOverrideParam();
            });
            
            root.Add(aspSimpleShadowSetupGroup);
        }
    }
}
