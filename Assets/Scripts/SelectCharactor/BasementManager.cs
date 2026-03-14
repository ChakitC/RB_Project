using System;
using UnityEngine;

public class BasementContext : MonoBehaviour
{
   public CameraManager camManager;
   public CharacterSelectManager characterSelectManager;
   public UiBaseMentManager uiBaseMentManager;
   public UIButtonHoverOutline  uiButtonHoverOutline;
   public UItoolbarManager uiToolbarManager;
   public SceneLoaderSystem sceneLoader;
   public PlayerInventory playerInventory;


   public UIInventory Inventory;

   private void Awake()
   {
      if (!sceneLoader) sceneLoader = FindObjectOfType<SceneLoaderSystem>();
      if (!uiButtonHoverOutline) uiButtonHoverOutline = GetComponent<UIButtonHoverOutline>();
      if (!camManager) camManager = GetComponent<CameraManager>();
      if (!characterSelectManager) characterSelectManager = GetComponent<CharacterSelectManager>();
      if (!uiBaseMentManager) uiBaseMentManager = GetComponent<UiBaseMentManager>();
   }
}
