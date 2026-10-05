/// <summary>Storage owned by a grant source. Only explicitly persistent variables use it.</summary>
public interface IPassiveStateStore
{
    bool TryRead(string key, out float value);
    void Write(string key, float value);
}
