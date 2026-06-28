using UnityEngine;

internal enum MeleeSessionAction { Continue, Advance, Complete }

internal sealed class MeleeComboSession
{
    MeleeComboSO _combo;
    int _step;
    int _bufferedPresses;
    bool _windowExpired;
    bool _chainOpen;
    bool _pressedInWindow;
    bool _hasChain;
    MeleeComboSO.Step _cfg;

    public int CurrentStepIndex => _step;
    public MeleeComboSO.Step CurrentStep => _cfg;
    public MeleeComboSO Combo => _combo;
    public bool IsActive => _combo != null;

    public void Start(MeleeComboSO combo)
    {
        _combo = combo;
        _step = 0;
        _bufferedPresses = 0;
        _windowExpired = false;
        _chainOpen = false;
        _pressedInWindow = false;

        if (combo.Steps.Count > 0)
            _cfg = combo.Steps[0];

        UpdateChainInfo();
    }

    public void Clear()
    {
        _combo = null;
        _step = 0;
        _bufferedPresses = 0;
        _windowExpired = false;
        _chainOpen = false;
        _pressedInWindow = false;
        _hasChain = false;
        _cfg = default;
    }

    public MeleeSessionAction QueuePress()
    {
        if (_combo == null)
            return MeleeSessionAction.Continue;

        int last = _combo.Steps.Count - 1;
        bool canRepeat = CanRepeatLastStep(last);
        int maxRemaining = last - _step;
        if (maxRemaining <= 0 && !canRepeat)
            return MeleeSessionAction.Continue;

        if (_windowExpired && _cfg.dropBufferOnWindowExpire)
            return MeleeSessionAction.Continue;

        _bufferedPresses = canRepeat
            ? Mathf.Min(1, _bufferedPresses + 1)
            : Mathf.Min(maxRemaining, _bufferedPresses + 1);

        if (_hasChain && _chainOpen)
        {
            _pressedInWindow = true;
            return TryAdvance();
        }

        return MeleeSessionAction.Continue;
    }

    public MeleeSessionAction NotifyChainWindowOpened()
    {
        _chainOpen = true;
        _pressedInWindow = false;

        if (_bufferedPresses > 0)
        {
            _pressedInWindow = true;
            return TryAdvance();
        }

        return MeleeSessionAction.Continue;
    }

    public void NotifyChainWindowClosed()
    {
        _chainOpen = false;
        _windowExpired = true;

        if (_cfg.dropBufferOnWindowExpire && !_pressedInWindow)
            _bufferedPresses = 0;
    }

    public MeleeSessionAction NotifyStepCompleted()
    {
        if (_combo == null)
            return MeleeSessionAction.Complete;

        int last = _combo.Steps.Count - 1;
        bool canRepeat = CanRepeatLastStep(last);

        if (_bufferedPresses > 0 && (_step < last || canRepeat))
            return TryAdvance();

        return MeleeSessionAction.Complete;
    }

    MeleeSessionAction TryAdvance()
    {
        if (_combo == null)
            return MeleeSessionAction.Complete;

        int last = _combo.Steps.Count - 1;
        bool canRepeat = CanRepeatLastStep(last);

        if (_step >= last)
        {
            if (!canRepeat)
                return MeleeSessionAction.Continue;

            _bufferedPresses = Mathf.Max(0, _bufferedPresses - 1);
            SetStep(_step);
            return MeleeSessionAction.Advance;
        }

        _bufferedPresses = Mathf.Max(0, _bufferedPresses - 1);
        SetStep(_step + 1);
        return MeleeSessionAction.Advance;
    }

    void SetStep(int newStep)
    {
        _step = newStep;
        _windowExpired = false;
        _chainOpen = false;
        _pressedInWindow = false;

        if (_combo != null && newStep >= 0 && newStep < _combo.Steps.Count)
            _cfg = _combo.Steps[newStep];

        UpdateChainInfo();
    }

    void UpdateChainInfo()
    {
        if (_combo == null)
        {
            _hasChain = false;
            return;
        }

        int last = _combo.Steps.Count - 1;
        float chainEnd = Mathf.Clamp01(_cfg.chainWindowN.y);
        bool repeatLast = _step == last && chainEnd > 0.0001f;
        _hasChain = (_step < last || repeatLast) && chainEnd > 0.0001f;
    }

    bool CanRepeatLastStep(int lastIndex)
    {
        return _step == lastIndex && _cfg.chainWindowN.y > 0.0001f;
    }
}
