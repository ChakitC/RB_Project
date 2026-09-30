using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct SkillCastStartResult
{
    public readonly SkillCastStartKind Kind;
    public readonly int RequestId;

    public bool Started => Kind != SkillCastStartKind.Rejected;

    public SkillCastStartResult(SkillCastStartKind kind, int requestId)
    {
        Kind = kind;
        RequestId = requestId;
    }
}

