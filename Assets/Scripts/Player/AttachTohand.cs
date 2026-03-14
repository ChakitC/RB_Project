using System;
using UnityEngine;

public class AttachTohand : MonoBehaviour
{
   public Animator animator;
   
   [Header("Left Hand Item")]
    public  Transform LeftItem;
    public Vector3 LeftlocalPos, LeftlocalEuler;
    public Vector3 LeftlocalScale =Vector3.one;
    
    [Header("Right Hand Item")]
    public  Transform RightItem;
    public Vector3 RightlocalPos,  RightlocalEuler;
    public Vector3 RightlocalScale =Vector3.one;
    private void Awake()
    {
        if(!animator) animator = GetComponent<Animator>();
    }

    void Start()
    {
        if (!animator || !animator.isHuman)
        {
            Debug.LogError("AttachTohand needs to be a HumanAnimator");
        }
        
        var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        if (RightItem && rightHand)
        {
            RightItem.SetParent(rightHand, false);
            rightHand.localPosition = RightlocalPos;
            rightHand.localEulerAngles = RightlocalEuler;
            rightHand.localScale = RightlocalScale;
            
        }

        if (LeftItem && leftHand)
        {
            LeftItem.SetParent(leftHand, false);
            leftHand.localPosition = LeftlocalPos;
            leftHand.localEulerAngles = LeftlocalEuler;
            leftHand.localScale = LeftlocalScale;
        }
        
        
       
    }

    
    void Update()
    {
        
    }
}
