using UnityEngine;
using Animancer;
using System;

public sealed partial class CharacterAnimBrain
{
    
    private sealed class  Locomotion_StatusEffect : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        public Locomotion_StatusEffect(CharacterAnimBrain owner) => this.owner = owner;
        
    }
  
}
