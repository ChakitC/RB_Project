using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class VfxRigSpawnPlayer : MonoBehaviour
{
    [SerializeField] Animator _animator;
    [SerializeField] AnimationClip _clip;
    [SerializeField] string _stateName = "Spawn";
    [SerializeField] int _layer = 0;

#if UNITY_EDITOR
    double _lastEditorTime;
    float _editorPlaybackTime;
#endif

    void OnValidate()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);
    }

    void OnEnable()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            _editorPlaybackTime = 0f;
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            if (_clip == null)
                PlayAnimatorState();
            return;
        }
#endif
        Play();
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    void Play()
    {
        if (_clip != null)
            OverrideAnimatorClip();

        PlayAnimatorState();
    }

    void PlayAnimatorState()
    {
        if (_animator == null || string.IsNullOrWhiteSpace(_stateName))
            return;

        _animator.Play(_stateName, _layer, 0f);
    }

    void OverrideAnimatorClip()
    {
        if (_animator == null || _clip == null || _animator.runtimeAnimatorController == null)
            return;

        var overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, _clip);
        overrideController.ApplyOverrides(overrides);
        _animator.runtimeAnimatorController = overrideController;
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (this == null)
        {
            EditorApplication.update -= EditorTick;
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - _lastEditorTime);
        _lastEditorTime = now;

        if (_clip != null)
        {
            _editorPlaybackTime += dt;
            float duration = _clip.length > 0f ? _clip.length : 1f;
            _editorPlaybackTime = _clip.isLooping
                ? _editorPlaybackTime % duration
                : Mathf.Min(_editorPlaybackTime, duration);
            _clip.SampleAnimation(gameObject, _editorPlaybackTime);
        }
        else if (_animator != null)
        {
            _animator.Update(dt);
        }

        SceneView.RepaintAll();
    }

    [ContextMenu("Preview Spawn Animation")]
    void Preview()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

        _editorPlaybackTime = 0f;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        if (_clip == null)
            PlayAnimatorState();
    }
#endif
}
