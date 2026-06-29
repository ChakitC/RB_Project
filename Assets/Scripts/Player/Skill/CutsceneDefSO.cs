using UnityEngine;

[CreateAssetMenu(fileName = "CutsceneDef", menuName = "Game/Cutscene/Cutscene Def")]
public sealed class CutsceneDefSO : ScriptableObject
{
    public CutsceneDef cutscene = new CutsceneDef();
}
