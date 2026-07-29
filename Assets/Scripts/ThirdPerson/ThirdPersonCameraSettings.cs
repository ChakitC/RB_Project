using UnityEngine;

public static class ThirdPersonCameraSettings
{
    const string SensitivityXKey = "TPS.Camera.SensitivityX";
    const string SensitivityYKey = "TPS.Camera.SensitivityY";
    const string InvertYKey = "TPS.Camera.InvertY";
    const string FovKey = "TPS.Camera.FOV";

    const float DefaultSensitivityX = 0.12f;
    const float DefaultSensitivityY = 0.1f;
    const float DefaultFov = 60f;

    public static float SensitivityX
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityXKey, DefaultSensitivityX), 0.01f, 1f);
        set
        {
            PlayerPrefs.SetFloat(SensitivityXKey, Mathf.Clamp(value, 0.01f, 1f));
            PlayerPrefs.Save();
        }
    }

    public static float SensitivityY
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityYKey, DefaultSensitivityY), 0.01f, 1f);
        set
        {
            PlayerPrefs.SetFloat(SensitivityYKey, Mathf.Clamp(value, 0.01f, 1f));
            PlayerPrefs.Save();
        }
    }

    public static bool InvertY
    {
        get => PlayerPrefs.GetInt(InvertYKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(InvertYKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static float FieldOfView
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(FovKey, DefaultFov), 35f, 90f);
        set
        {
            PlayerPrefs.SetFloat(FovKey, Mathf.Clamp(value, 35f, 90f));
            PlayerPrefs.Save();
        }
    }
}
