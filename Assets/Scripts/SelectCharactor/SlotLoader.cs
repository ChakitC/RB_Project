using System;
using UnityEngine;

public class SlotLoader : MonoBehaviour
{
   [SerializeField] private CharacterSelectable CharacterSelectable;
   private void Awake()
   {
      if (CharacterSelectable == null) { CharacterSelectable = GetComponent<CharacterSelectable>(); }
      
      if (SaveManager.Instance == null)return;
      
      
      
      
      
      
      
      
      
   }
   
}
