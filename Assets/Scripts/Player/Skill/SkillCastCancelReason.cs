using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillCastCancelReason
{
    InvalidState,
    AnimationInterrupted,
    Disabled,
    Blocked,
    Stunned,
    Staggered,
    CharacterDown,
    CharacterDead,
}

