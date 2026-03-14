using System;
using Animancer;
using Animancer.Units;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;




public class CharacterAnimatorController_2 : MonoBehaviour
{
    [Header("Refs")]
    public AnimancerComponent Animancer;
    public PlayerContext ctx;
    
    [Header("Clips")]
    public DirectionalAnimationSet8 DirectionalAnimation; // 8 ทิศ
    public AnimationClip Idle;
    public AnimationClip Reload;
    public AnimationClip Shoot;

    [Header("Tuning")]
    [Range(0f, 0.5f)] public float fade = 0.12f;    
    public float minMoveToRun = 0.1f;               
    public float animSpeedAtMaxInput = 1.0f;       

    private AnimationClip _currentClip;
    private AnimationClip _currentActionClip;
    
    [Header("Mask")]
    
    [SerializeField] private AvatarMask _ActionMask;
    private AnimancerLayer _BaseLayer;
    private AnimancerLayer _ActionLayer;
    
    [Button]
    public void Play(AnimationClip clip) => Animancer.Play(clip, fade);
    
    private AnimancerState _lastState;
    
    private bool IsReloading() => ctx.WeaponSystem.IsReloading;
    private enum PlayerState 
    {
      NotActing,
      Acting,
    }
    private PlayerState _currentState;

    void Start()
    {
      
    }

    private void Awake()
    {
        {
            if (!Animancer)
                Animancer = GetComponent<AnimancerComponent>();

            if (!Animancer)
            {
                Debug.LogError("[Anim] Missing AnimancerComponent", this);
                enabled = false;
                return;
            }

            _BaseLayer   = Animancer.Layers[0];
            _ActionLayer = Animancer.Layers[1];
            _ActionLayer.Mask = _ActionMask;
        }
    }

    void Update()
    {
        if (Animancer == null)
        {
          
            Debug.LogError("[Anim] Animancer == null ใน CharacterAnimatorController_2", this);
            enabled = false;
            return;
        }

        if (Animancer.Animator == null)
        {
            return;
        }
        
        UpdateMovement();
        UpdateAction();
        
    }
   
    private void UpdateMovement()
    {
            Vector2 input = ctx.moveInput;
            Vector3 world = new Vector3(input.x, 0f, input.y);
            Vector3 local = transform.InverseTransformDirection(world);
            Vector2 dir2  = new Vector2(local.x, local.z);
        
            if (dir2.sqrMagnitude < minMoveToRun * minMoveToRun)
            {
                PlayIfChanged(Idle);
                return;
            }
            
            dir2 = dir2.normalized;
            var moveClip = DirectionalAnimation.Get(dir2);
            var state = PlayIfChanged(moveClip);
        
            if (state != null)
            {
                float inputMag = Mathf.Clamp01(input.magnitude);
                state.Speed = Mathf.Lerp(0.8f, animSpeedAtMaxInput, inputMag);
            } 
            
    }
   
    private AnimancerState PlayIfChanged(AnimationClip clip)
    {
        if (clip == null) return null;
       
        if (_currentClip == clip)
            return _lastState;
        
        _currentClip = clip;
        _lastState = Animancer.Play(clip, fade);
        return _lastState;
    }

    
    private void UpdateAction()
    {
        if (Reload != null && IsReloading())
        {
            if (_currentActionClip  != Reload) PlayForce(Reload);
            return;
        }
        
        if (Shoot != null && ctx.WeaponSystem.isFiring)
        {
            if (_currentActionClip  != Shoot) PlayForce(Shoot);
            return;
        }
        
        if (_ActionLayer.Weight > 0f)
            _ActionLayer.StartFade(0f, 0.1f);
        _currentActionClip = null;
        
        
       
        
    }
    
    private AnimancerState PlayForce(AnimationClip clip)
    {
        if (clip == null) return null;
        _currentActionClip  = clip;
        _lastState = _ActionLayer.Play(clip, fade);
        _lastState.NormalizedTime = 0f; 
        _lastState.Speed = 1f;
        return _lastState;
    }
    
}
