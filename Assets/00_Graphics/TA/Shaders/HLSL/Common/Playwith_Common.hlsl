#ifndef PLAYWITH_COMMON_INCLUDED
#define PLAYWITH_COMMON_INCLUDED

half LinearStep(half In, half Min, half Max)
{
    half subtractInMin = In - Min;
    half subtractMaxMin = Max - Min;
    return subtractInMin/subtractMaxMin;
}

half Smoother(half In, half Threshold, half Smoother){
    half Out = 0;
    half addFactor = Threshold + Smoother + Smoother;
    half subtractFactor = Threshold - Smoother;

    half values = LinearStep(In, addFactor, subtractFactor);
    return Out = 1 - values;
}

float3 NormalBlend(float3 A, float3 B)
{
    return normalize(float3(A.rg + B.rg, A.b * B.b));
}

float3 RotateAboutAxis_Degrees(float3 In, float3 Axis, float Rotation)
{
    Rotation = radians(Rotation);
    float s = sin(Rotation);
    float c = cos(Rotation);
    float one_minus_c = 1.0 - c;

    Axis = normalize(Axis);

    float3x3 rot_mat =
        {
        one_minus_c * Axis.x * Axis.x + c, one_minus_c * Axis.x * Axis.y - Axis.z * s, one_minus_c * Axis.z * Axis.x + Axis.y * s,
        one_minus_c * Axis.x * Axis.y + Axis.z * s, one_minus_c * Axis.y * Axis.y + c, one_minus_c * Axis.y * Axis.z - Axis.x * s,
        one_minus_c * Axis.z * Axis.x - Axis.y * s, one_minus_c * Axis.y * Axis.z + Axis.x * s, one_minus_c * Axis.z * Axis.z + c
        };
    
    return mul(rot_mat, In);
}

float2 Rotate_Degrees(float2 UV, float2 Center, float Rotation)
{
    Rotation = Rotation * (3.1415926f/180.0f);
    UV -= Center;
    float s = sin(Rotation);
    float c = cos(Rotation);
    float2x2 rMatrix = float2x2(c, -s, s, c);
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix * 2 - 1;
    UV.xy = mul(UV.xy, rMatrix);
    UV += Center;
    return UV;
}
void Saturation(float3 In, float Saturation, out float3 Out)
{
    float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
    Out =  luma.xxx + Saturation.xxx * (In - luma.xxx);
}
float Remap(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

float2 Remap(float2 In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

float3 Remap(float3 In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

float4 Remap(float4 In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

float2 TilingAndOffset(float2 UV, float2 Tiling, float2 Offset)
{
    return UV * Tiling + Offset;   
}

float2 TilingAndOffset(float2 UV, float4 TilingOffset)
{
    return UV * TilingOffset.xy + TilingOffset.zw;   
}

float Hash_Tchou_2_1(float2 i)
{
    uint r;
    uint2 v = (uint2) (int2) round(i);
    v.y ^= 1103515245U;
    v.x += v.y;
    v.x *= v.y;
    v.x ^= v.x >> 5u;
    r = v.x *= 0x27d4eb2du;
    return r * (1.0 / float(0xffffffff));
}

float3 Hash23_Without_Sine(float2 UV)
{
    float3 p3 = frac(float3(UV.xyx)* float3(0.1031,0.1030,0.0973));
    p3 += dot(p3,p3.yzx+19.19);
    return frac((p3.xxy+p3.yyz)*p3.zyx);
}
        
float SimpleNoise_Value(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);
    uv = abs(frac(uv) - 0.5);
    float2 c0 = i + float2(0.0, 0.0);
    float2 c1 = i + float2(1.0, 0.0);
    float2 c2 = i + float2(0.0, 1.0);
    float2 c3 = i + float2(1.0, 1.0);
    float r0 = Hash_Tchou_2_1(c0);
    float r1 = Hash_Tchou_2_1(c1);
    float r2 = Hash_Tchou_2_1(c2);
    float r3 = Hash_Tchou_2_1(c3);
    float bottomOfGrid = lerp(r0, r1, f.x);
    float topOfGrid = lerp(r2, r3, f.x);
    float t = lerp(bottomOfGrid, topOfGrid, f.y);
    return t;
}

float SimpleNoise_Deterministic(float2 uv, float scale)
{
    float result = 0;
    float freq, amp;
    freq = pow(2.0, float(0));
    amp = pow(0.5, float(3-0));
    result += SimpleNoise_Value(float2(uv.xy*(scale/freq)))*amp;
    freq = pow(2.0, float(1));
    amp = pow(0.5, float(3-1));
    result += SimpleNoise_Value(float2(uv.xy*(scale/freq)))*amp;
    freq = pow(2.0, float(2));
    amp = pow(0.5, float(3-2));
    result += SimpleNoise_Value(float2(uv.xy*(scale/freq)))*amp;
    return result;
}

half3 GetViewDirectionTangentSpace(half4 tangentWS, half3 normalWS, half3 viewDirWS)
{
    const half3 unNormalized_normalWS = normalWS;
    const half reNormalFactor = 1.0 / length(unNormalized_normalWS);

    const half crossSign = (tangentWS.w > 0.0 ? 1.0 : -1.0);
    const half3 biTangent = crossSign * cross(normalWS.xyz, tangentWS.xyz);

    half3 WorldSpaceNormal = reNormalFactor * normalWS.xyz;

    half3 WorldSpaceTangent = reNormalFactor * tangentWS.xyz;
    half3 WorldSpaceBiTangent = reNormalFactor * biTangent;

    const half3x3 tangentSpaceTransform = half3x3(WorldSpaceTangent, WorldSpaceBiTangent, WorldSpaceNormal);
    half3 viewDirTS = mul(tangentSpaceTransform, viewDirWS);

    return viewDirTS;
}

float4 HeightToNormal(float2 uv, float height, float normalStrength, float2 pixelToTexelRatio)
{
    float2 normalxy = -float2(ddx(height), ddy(height)) * pixelToTexelRatio;
    normalxy *= normalStrength;
    normalxy += 0.5;
    
    return float4(normalxy, 1, 1); 
}

float3 NormalFromHeight_World(float In, float Strength, float3 Position, float3x3 TangentMatrix)
{
    float3 worldDerivativeX = ddx(Position);
    float3 worldDerivativeY = ddy(Position);

    float3 crossX = cross(TangentMatrix[2].xyz, worldDerivativeX);
    float3 crossY = cross(worldDerivativeY, TangentMatrix[2].xyz);
    float d = dot(worldDerivativeX, crossY);
    float sgn = d < 0.0 ? (-1.0f) : 1.0f;
    float surface = sgn / max(0.000000000000001192093f, abs(d));

    float dHdx = ddx(In);
    float dHdy = ddy(In);
    float3 surfGrad = surface * (dHdx*crossY + dHdy*crossX);
    return normalize(TangentMatrix[2].xyz - (Strength * surfGrad));
}

void UVRandomTransform(inout float2 UV, out float rotationDegress, float2 RandomSeed, float2 Scale, float2 Rotation)
{
    float3 randomHash = Hash23_Without_Sine(RandomSeed);
    float4 TilingOffset = float4(lerp(Scale.x,Scale.y, randomHash.z).xx, randomHash.xy);

    rotationDegress = lerp(Rotation.x, Rotation.y ,frac(randomHash.z * 16));
    float2 uv = Rotate_Degrees(UV, float2(0.5,0.5), rotationDegress);
    UV = TilingAndOffset(uv, TilingOffset);
}


float3 random3(float3 c) {
    float j = 4096.0*sin(dot(c,float3(17.0, 59.4, 15.0)));
    float3 r;
    r.z = frac(512.0*j);
    j *= .125;
    r.x = frac(512.0*j);
    j *= .125;
    r.y = frac(512.0*j);
    return r-0.5;
}

/* skew constants for 3d simplex functions */
const float F3 =  0.3333333;
const float G3 =  0.1666667;

/* 3d simplex noise */
float simplex3d(float3 p)
{
    /* 1. find current tetrahedron T and it's four vertices */
    /* s, s+i1, s+i2, s+1.0 - absolute skewed (integer) coordinates of T vertices */
    /* x, x1, x2, x3 - unskewed coordinates of p relative to each of T vertices*/
				 
    /* calculate s and x */
    float3 s = floor(p + dot(p, float3(F3,F3,F3)));
    float3 x = p - s + dot(s, float3(G3,G3,G3));
				 
    /* calculate i1 and i2 */
    float3 e = step(float3(0,0,0), x - x.yzx);
    float3 i1 = e*(1.0 - e.zxy);
    float3 i2 = 1.0 - e.zxy*(1.0 - e);
	 				
    /* x1, x2, x3 */
    float3 x1 = x - i1 + G3;
    float3 x2 = x - i2 + 2.0*G3;
    float3 x3 = x - 1.0 + 3.0*G3;
				 
    /* 2. find four surflets and store them in d */
    float4 w, d;
				 
    /* calculate surflet weights */
    w.x = dot(x, x);
    w.y = dot(x1, x1);
    w.z = dot(x2, x2);
    w.w = dot(x3, x3);
				 
    /* w fades from 0.6 at the center of the surflet to 0.0 at the margin */
    w = max(0.6 - w, 0.0);
				 
    /* calculate surflet components */
    d.x = dot(random3(s), x);
    d.y = dot(random3(s + i1), x1);
    d.z = dot(random3(s + i2), x2);
    d.w = dot(random3(s + 1.0), x3);
				 
    /* multiply d by w^4 */
    w *= w;
    w *= w;
    d *= w;
				 
    /* 3. return the sum of the four surflets */
    return dot(d, 52.0);
}
half Noise3D(float3 m)
{
    return   0.5333333*simplex3d(m)
    +0.2666667*simplex3d(2.0*m)
    +0.1333333*simplex3d(4.0*m)
    +0.0666667*simplex3d(8.0*m);
}

void SG_Noise3D_half(float3 m, out float Out)
{
    Out = 0.5333333*simplex3d(m)
    +0.2666667*simplex3d(2.0*m)
    +0.1333333*simplex3d(4.0*m)
    +0.0666667*simplex3d(8.0*m);
}
// Alpha To Coverage Extension
float CalcMipLevel(float2 UV)
{
    float2 dx = ddx(UV);
    float2 dy = ddy(UV);
    float delta_max_sqr = max(dot(dx,dx), dot(dy,dy));
    return max(0,0.5 * log2(delta_max_sqr));
}



#endif