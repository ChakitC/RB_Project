using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillCastStartKind
{
    Rejected,
    ImmediateSuccess,
    WaitingForAnimation,
    // PartyCommandSkillExecution has accepted a field-ally warp. No cast request
    // exists until arrival; RequestId is zero while the visibility transition runs.
    WaitingForPlacement,
}

