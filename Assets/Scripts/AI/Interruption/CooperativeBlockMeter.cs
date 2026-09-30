using UnityEngine;

// Driven by one pause-aware real-time clock. Input first advances to its event time.
public sealed class CooperativeBlockMeter
{
    readonly CooperativeBlockSettings settings;
    public float Value { get; private set; }
    public double Elapsed { get; private set; }
    public float Remaining => Mathf.Max(0f, settings.duration - (float)Elapsed);
    public CooperativeBlockState State { get; private set; } = CooperativeBlockState.Struggling;
    public CooperativeBlockMeter(CooperativeBlockSettings settings)
    {
        if (settings == null || !settings.IsValid) throw new System.ArgumentException("Invalid cooperative meter settings.");
        this.settings = settings.Copy(); Value = settings.initialMeter;
    }
    public void Advance(double seconds)
    {
        if (State != CooperativeBlockState.Struggling || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
        Elapsed += seconds;
        Value = Mathf.Max(0f, settings.initialMeter + totalGain - (float)(Elapsed * settings.drainPerSecond));
        if (Value <= 0f || Elapsed >= settings.duration) State = CooperativeBlockState.Failure;
    }
    float totalGain;
    public bool Press()
    {
        if (State != CooperativeBlockState.Struggling) return false;
        totalGain += settings.gainPerPress;
        Value = Mathf.Min(1f, settings.initialMeter + totalGain - (float)(Elapsed * settings.drainPerSecond));
        if (Value >= 1f - .000001f) State = CooperativeBlockState.Success;
        return true;
    }
}
