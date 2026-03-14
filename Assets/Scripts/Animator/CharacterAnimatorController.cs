using System;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

public class CharacterAnimatorController : MonoBehaviour
{
    public AnimancerComponent Animancer;
    public DirectionalAnimationSet8 DirectionalAnimation;
    public PlayerControllerTest Playercontroller;
    public AnimationClip Idle;
    public AnimationClip Reload;

    [Button]
    public void Play(AnimationClip clip)
    {
        Animancer.Play(clip);
    }
    
    
    public void Update()
    {
        
        
       
            
     
        
        Animancer.Play(Playercontroller.move != Vector2.zero ? DirectionalAnimation.Get(Playercontroller.move) : Idle);
        
       
        
        
    
    }

    // private State _CurrentState;
    //
    // private enum State
    // {
    //     NotActing,
    //     Acting,
    // }




}