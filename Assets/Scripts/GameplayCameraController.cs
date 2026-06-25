using System;
using System.Collections.Generic;
using UnityEngine;

public class GameplayCameraController : MonoBehaviour
{
    public Transform taget;
    public float smooth;
    public Vector3 offset;

    [Header("Aim Ahead")]
    [SerializeField, Range(0f, 1f)] float lookAheadWeight = 0.4f;
    [SerializeField] float lookAheadSmooth = 0.15f;
    [SerializeField] float defaultLookAheadDistance = 3f;
    [SerializeField] WeaponLookAheadEntry[] lookAheadTable = new WeaponLookAheadEntry[]
    {
        new WeaponLookAheadEntry { weaponType = WeaponType.Sniper,  distance = 7f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Hmg,     distance = 5f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Rifle,   distance = 4f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Smg,     distance = 3f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Shotgun, distance = 2.5f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Pistol,  distance = 2f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Melee,   distance = 0.5f },
    };

    [Header("Camera Shake")]
    [SerializeField] float shakeTraumaPerMarker = 0.6f;
    [SerializeField] float shakeMaxPositionOffset = 0.3f;
    [SerializeField] float shakeMaxRotationDeg = 2f;
    [SerializeField] float shakeTraumaDecayPerSecond = 1.5f;
    [SerializeField] float shakeNoiseSpeed = 8f;

    Vector3 _followVelocity = Vector3.zero;
    Vector3 _aimAheadVelocity = Vector3.zero;
    Vector3 _currentAimAheadOffset = Vector3.zero;
    bool _aimAheadEnabled;
    bool _prevIsAiming;

    Transform _shakeTarget;
    Vector3 _shakeBaseLocalPos;
    Vector3 _shakeBaseLocalRot;
    float _trauma;
    float _noiseOffset;

    readonly List<CharacterAnimBrain> _subscribedBrains = new();
    FieldAllyManager _fieldAllyManager;
    AllyHelperManager _allyHelperManager;

    void Awake()
    {
        _noiseOffset = UnityEngine.Random.Range(0f, 100f);

        if (transform.childCount > 0)
        {
            _shakeTarget = transform.GetChild(0);
            _shakeBaseLocalPos = _shakeTarget.localPosition;
            _shakeBaseLocalRot = _shakeTarget.localEulerAngles;
        }
    }

    void OnEnable()
    {
        SubscribeAll();
    }

    void OnDisable()
    {
        UnsubscribeAll();
        ResetShakeTarget();
    }

    void LateUpdate()
    {
        if (taget == null)
            return;

        TickAimAheadToggle();
        Vector3 targetAimAhead = ComputeAimAheadOffset();
        _currentAimAheadOffset = Vector3.SmoothDamp(
            _currentAimAheadOffset, targetAimAhead, ref _aimAheadVelocity, lookAheadSmooth);

        Vector3 targetPosition = taget.position + offset + _currentAimAheadOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position, targetPosition, ref _followVelocity, smooth);

        ApplyShake();
    }

    void TickAimAheadToggle()
    {
        PlayerContext ctx = PlayerContext.Instance;
        bool isAiming = ctx != null && ctx.WeaponSystem != null && ctx.WeaponSystem.IsAiming;
        if (isAiming && !_prevIsAiming)
            _aimAheadEnabled = !_aimAheadEnabled;
        _prevIsAiming = isAiming;
    }

    Vector3 ComputeAimAheadOffset()
    {
        if (!_aimAheadEnabled)
            return Vector3.zero;

        PlayerContext ctx = PlayerContext.Instance;
        if (ctx == null)
            return Vector3.zero;

        Transform aimTransform = ctx.aimTarget;
        if (aimTransform == null)
            return Vector3.zero;

        Vector3 delta = aimTransform.position - taget.position;
        delta.y = 0f;

        float dist = delta.magnitude;
        if (dist < 0.001f)
            return Vector3.zero;

        float maxDistance = GetCurrentLookAheadDistance();
        float lookAmount = Mathf.Min(dist * lookAheadWeight, maxDistance);
        return (delta / dist) * lookAmount;
    }

    float GetCurrentLookAheadDistance()
    {
        PlayerContext ctx = PlayerContext.Instance;
        if (ctx == null || ctx.WeaponSystem == null)
            return defaultLookAheadDistance;

        WeaponType current = ctx.WeaponSystem.gunType;
        for (int i = 0; i < lookAheadTable.Length; i++)
        {
            if (lookAheadTable[i].weaponType == current)
                return lookAheadTable[i].distance;
        }

        return defaultLookAheadDistance;
    }

    void ApplyShake()
    {
        if (_shakeTarget == null)
            return;

        float shake = _trauma * _trauma;

        if (shake > 0.0001f)
        {
            float t = Time.unscaledTime * shakeNoiseSpeed + _noiseOffset;
            float px = Mathf.PerlinNoise(t, 0f) * 2f - 1f;
            float py = Mathf.PerlinNoise(0f, t) * 2f - 1f;
            float rz = Mathf.PerlinNoise(t + 53f, t + 53f) * 2f - 1f;

            _shakeTarget.localPosition = _shakeBaseLocalPos +
                new Vector3(px, py, 0f) * (shakeMaxPositionOffset * shake);
            _shakeTarget.localEulerAngles = _shakeBaseLocalRot +
                new Vector3(0f, 0f, rz * shakeMaxRotationDeg * shake);
        }
        else
        {
            _shakeTarget.localPosition = _shakeBaseLocalPos;
            _shakeTarget.localEulerAngles = _shakeBaseLocalRot;
        }

        _trauma = Mathf.MoveTowards(_trauma, 0f, shakeTraumaDecayPerSecond * Time.unscaledDeltaTime);
    }

    void ResetShakeTarget()
    {
        _trauma = 0f;
        if (_shakeTarget != null)
        {
            _shakeTarget.localPosition = _shakeBaseLocalPos;
            _shakeTarget.localEulerAngles = _shakeBaseLocalRot;
        }
    }

    void SubscribeAll()
    {
        UnsubscribeAll();

        PlayerContext ctx = PlayerContext.Instance;
        if (ctx == null)
            return;

        if (ctx.AnimBrain != null)
            SubscribeBrain(ctx.AnimBrain);

        _fieldAllyManager = ctx.fieldAllyManager;
        if (_fieldAllyManager != null)
        {
            _fieldAllyManager.MemberRegistered += OnAllyRegistered;
            _fieldAllyManager.MemberUnregistered += OnAllyUnregistered;

            foreach (FieldAllyMember member in _fieldAllyManager.RegisteredMembers)
            {
                CharacterAnimBrain brain = member != null ? member.AnimBrainRef : null;
                if (brain != null)
                    SubscribeBrain(brain);
            }
        }

        _allyHelperManager = ctx.allyHelper;
        if (_allyHelperManager != null)
        {
            _allyHelperManager.HelperAnimBrainChanged += OnHelperAnimBrainChanged;
            CharacterAnimBrain helperBrain = _allyHelperManager.HelperAnimBrain;
            if (helperBrain != null)
                SubscribeBrain(helperBrain);
        }
    }

    void UnsubscribeAll()
    {
        for (int i = _subscribedBrains.Count - 1; i >= 0; i--)
        {
            CharacterAnimBrain brain = _subscribedBrains[i];
            if (brain != null)
                brain.SkillTimelineEventRaised -= OnSkillTimelineEvent;
        }
        _subscribedBrains.Clear();

        if (_fieldAllyManager != null)
        {
            _fieldAllyManager.MemberRegistered -= OnAllyRegistered;
            _fieldAllyManager.MemberUnregistered -= OnAllyUnregistered;
            _fieldAllyManager = null;
        }

        if (_allyHelperManager != null)
        {
            _allyHelperManager.HelperAnimBrainChanged -= OnHelperAnimBrainChanged;
            _allyHelperManager = null;
        }
    }

    void SubscribeBrain(CharacterAnimBrain brain)
    {
        if (brain == null || _subscribedBrains.Contains(brain))
            return;

        brain.SkillTimelineEventRaised += OnSkillTimelineEvent;
        _subscribedBrains.Add(brain);
    }

    void UnsubscribeBrain(CharacterAnimBrain brain)
    {
        if (brain == null)
            return;

        brain.SkillTimelineEventRaised -= OnSkillTimelineEvent;
        _subscribedBrains.Remove(brain);
    }

    void OnSkillTimelineEvent(int requestId, CombatTimelineEventName eventName)
    {
        if (eventName != CombatTimelineEventName.ShakeCamera)
            return;

        _trauma = Mathf.Clamp01(_trauma + shakeTraumaPerMarker);
    }

    void OnAllyRegistered(ChainActorRole role, FieldAllyMember member)
    {
        if (member == null)
            return;

        CharacterAnimBrain brain = member.AnimBrainRef;
        if (brain != null)
            SubscribeBrain(brain);
    }

    void OnAllyUnregistered(ChainActorRole role)
    {
        for (int i = _subscribedBrains.Count - 1; i >= 0; i--)
        {
            CharacterAnimBrain brain = _subscribedBrains[i];
            if (brain == null)
            {
                _subscribedBrains.RemoveAt(i);
                continue;
            }
        }
    }

    void OnHelperAnimBrainChanged(CharacterAnimBrain prev, CharacterAnimBrain next)
    {
        if (prev != null)
            UnsubscribeBrain(prev);
        if (next != null)
            SubscribeBrain(next);
    }

    [Serializable]
    public struct WeaponLookAheadEntry
    {
        public WeaponType weaponType;
        public float distance;
    }
}
