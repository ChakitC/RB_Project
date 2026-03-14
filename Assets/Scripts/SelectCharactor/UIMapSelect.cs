using System;
using UnityEngine;

public class UIMapSelect : MonoBehaviour
{
   public GameObject backbutton;
   public BasementContext bct;

   private void Awake()
   {
       if (bct!) bct = GetComponent<BasementContext>();
   }

   public void MobliizClick()
   {
       backbutton.SetActive(true);
   }

   public void OnBackButtonClick()
   {
       backbutton.SetActive(false);
   }
}
