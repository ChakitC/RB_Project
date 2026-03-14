using System;
using Unity.Cinemachine;
using UnityEngine;


public class CameraManager : MonoBehaviour
{
  public BasementContext bct;
  public CinemachineCamera StadeSelectCamera;
  public CinemachineCamera CharacterSelectCamera;

  private bool _isCharacterCamActive = false;
  public bool _inMapUI = false;
  public bool CamareInMapselect => _isCharacterCamActive;

  private void Awake()
  {
    CharacterSelectCamera.Priority = 20;
  }

  public void MobilizClick()
  {
    if (!_inMapUI)
    {
     
      CharacterSelectCamera.Priority = 10;
      StadeSelectCamera.Priority = 20;
      bct.uiBaseMentManager.FadeOut();
      Debug.Log("FadeOutToolbar");
      _inMapUI = true;
      
    }
    else
    {
      bct.sceneLoader.LoadGame();
      Debug.Log("Playgame");
    }
  }
  
  public void BackClick()
  {
    if (_inMapUI)
    {
      CharacterSelectCamera.Priority = 20;
      StadeSelectCamera.Priority = 10;
      bct.uiBaseMentManager.FadeIn();
      Debug.Log("FadeinToolbar");
      _inMapUI = false;
    }
  }
  
  
}