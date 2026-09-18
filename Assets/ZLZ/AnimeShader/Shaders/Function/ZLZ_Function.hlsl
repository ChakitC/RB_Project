#ifndef ZLZ_FUNCTION_INCLUDED
#define ZLZ_FUNCTION_INCLUDED

// void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
// {
//     Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
// }

float Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

float2 Unity_Remap_float2(float2 In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

// Mask channel picker: idx 0..7 -> m1.r/g/b/a, m2.r/g/b/a, idx >=8 -> None (1.0, uniform effect)
half ZLZ_PickMaskChannel(half4 m1, half4 m2, half idx)
{
    if (idx > 7.5h) return 1.0h;           // None: feature applies uniformly
    half4 src = (idx < 3.5h) ? m1 : m2;
    int c = ((int)idx) & 3;
    return src[c];
}

#endif // ZLZ_FUNCTION_INCLUDED
