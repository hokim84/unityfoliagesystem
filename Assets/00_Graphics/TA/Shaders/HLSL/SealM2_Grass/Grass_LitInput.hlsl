#ifndef PLAYWITH_LIT_INPUT_INCLUDED
#define PLAYWITH_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

#include "Assets/00_Graphics/TA/Shaders/HLSL/Common/Playwith_Common.hlsl"
#include "Assets/00_Graphics/TA/Shaders/HLSL/Common/Playwith_CustomStructs.hlsl"

CBUFFER_START(UnityPerMaterial)

float _Baking;
float _ConvertUnits;

//Surface Options
float _Cull;
float _Cutoff;
float _MipScale;

//Shadow Options
// float _EnableGlobalShadow;
// float4 _MidShadowColor;
// float4 _ShadowColor;
// float4 _ShadowTerm;

//Surface Inputs
float4 _BaseColor;
float4 _BottomColor;
float _BottomColorLevel;
float _BottomColorFade;
//float4 _Random_Color;
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _BaseMap_MipInfo;

// Surface Options
float _BumpScale;
// float _Smoothness;
// float _Metallic;
float _AmbientOcclusion;

half _EmissionIntensity;
float4 _EmissionColor;

float _Grass_Tension;
float2 _Wind_Direction;
float _Wind_Speed;
float2 _Wind_Wave_Scale;
float _Wind_Pattern_Scale;
float _Wind_Intensity;
//float _Wave_Light_Intensity;

float _Interaction_Distance;
float _Interaction_Strength;

CBUFFER_END

float3 _GrassTargetPosition;
float _EffectController;
float _EffectRange;

//Texture
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

float3 RotateAboutAxis_Radians(float3 In, float3 Axis, float Rotation)
{
    float s = sin(Rotation);
    float c = cos(Rotation);
    float one_minus_c = 1.0 - c;

    Axis = normalize(Axis);
    float3x3 rot_mat =
    {   one_minus_c * Axis.x * Axis.x + c, one_minus_c * Axis.x * Axis.y - Axis.z * s, one_minus_c * Axis.z * Axis.x + Axis.y * s,
        one_minus_c * Axis.x * Axis.y + Axis.z * s, one_minus_c * Axis.y * Axis.y + c, one_minus_c * Axis.y * Axis.z - Axis.x * s,
        one_minus_c * Axis.z * Axis.x - Axis.y * s, one_minus_c * Axis.y * Axis.z + Axis.x * s, one_minus_c * Axis.z * Axis.z + c
    };
    return mul(rot_mat,  In);
}

//Noise
float2 unity_gradientNoise_dir(float2 p)
{
    p = p % 289;
    float x = (34 * p.x + 1) * p.x % 289 + p.y;
    x = (34 * x + 1) * x % 289;
    x = frac(x / 41) * 2 - 1;
    return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
}

float unity_gradientNoise(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    float d00 = dot(unity_gradientNoise_dir(ip), fp);
    float d01 = dot(unity_gradientNoise_dir(ip + float2(0, 1)), fp - float2(0, 1));
    float d10 = dot(unity_gradientNoise_dir(ip + float2(1, 0)), fp - float2(1, 0));
    float d11 = dot(unity_gradientNoise_dir(ip + float2(1, 1)), fp - float2(1, 1));
    fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
    return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x);
}

float GradientNoise(float2 UV, float2 Scale)
{
    return unity_gradientNoise(UV * Scale) + 0.5;
}

float RandomRange(float2 Seed, float Min, float Max)
{
    float randomno =  frac(sin(dot(Seed, float2(12.9898, 78.233)))*43758.5453);
    return  lerp(Min, Max, randomno);
}

float3 loaclPosition(float2 uv, float _ConvertUnits)
{   
    float4x4 transformationMatrix = GetObjectToWorldMatrix();
    float3 moveOS = GetAbsolutePositionWS(transformationMatrix._m03_m13_m23);
    float3 convertUnit = float3(uv.x * _ConvertUnits, 0 ,uv.y * _ConvertUnits);
    float4 mulMatrix = mul(transformationMatrix,float4(convertUnit,1));
    return float3(mulMatrix.xyz) - moveOS;
}

float3 isBaking(float _Baking, float3 True, float3 False)
{
    return _Baking ? True : False;
}



VertexPositionInputs GetVertexPositionInputs_Matrix(float4x4 instanceMatrix, float3 positionOS)
{
    VertexPositionInputs input;
    input.positionWS = mul(instanceMatrix, float4(positionOS, 1.0)).xyz;
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

VertexNormalInputs GetVertexNormalInputs_Matrix(float4x4 instanceMatrix, float3 normalOS)
{
    VertexNormalInputs tbn;
    tbn.tangentWS = real3(1.0, 0.0, 0.0);
    tbn.bitangentWS = real3(0.0, 1.0, 0.0);
    tbn.normalWS = normalize(mul((float3x3)instanceMatrix, normalOS));
    return tbn;
}

//Vertex 연산
void VertexStage(inout float4 positionOS, inout float3 normalOS, inout float2 uv, float2 uv1, float2 uv2)
{
    VertexPositionInputs vertexInputs = GetVertexPositionInputs(positionOS.xyz);

    float3 positionWS = vertexInputs.positionWS;

    //objectToWorld 메트릭스 
    float4x4 transformationMatrix = GetObjectToWorldMatrix();

    //오브젝트 위치 가져오기
    float3 moveOS = GetAbsolutePositionWS(transformationMatrix._m03_m13_m23);

    //맥스 스크립트로 위치값 베이킹 된 오브젝트일 떄 사용
    float3 bakingOS = loaclPosition(uv2, _ConvertUnits);

    //베이킹 유무 
    float3 eachOS = positionWS - moveOS;
    float3 PosOSBaking = isBaking(_Baking,bakingOS + eachOS,eachOS);    
    float3 targetBaking = isBaking(_Baking,moveOS - bakingOS,moveOS);
    
    //Wind Axis 바람이 불어오는 위치 지정
    float3 WindAxis = cross(float3(0,1,0), normalize(float3(_Wind_Direction.x, 0, _Wind_Direction.y)));

    //노이즈로 바람 제어
    //float windSpeed = lerp(_Wind_Speed,3,_EffectController * skillMask);
    float2 windSpeed = _Time.y * _Wind_Speed * normalize(-1 * _Wind_Direction);
    float2 worldPositionUV = windSpeed + targetBaking.xz;
    float windWave = GradientNoise(worldPositionUV,_Wind_Wave_Scale) * 1.2;
    float windpattern = GradientNoise(worldPositionUV,_Wind_Pattern_Scale);
    
    float windRocationFin = saturate(windpattern * windWave);
        
    //풀이 심어진 곳에서 바닥 부분이 떨어지지 않도록 함, 바람 세기 조절
    float grassTension = pow(abs(uv1.y),_Grass_Tension);
    float windFin = grassTension * windRocationFin * _Wind_Intensity;
    float3 positionRotate = RotateAboutAxis_Radians(PosOSBaking, WindAxis, windFin);
    
    float distancePlayer = distance(_GrassTargetPosition.xz, targetBaking.xz);
    
    //캐릭터 스킬 인터렉티브
    float2 skillNoiseUV = targetBaking.xz + (_Time.y * 3 * normalize(-1 * _Wind_Direction));
    float skillMaskNoise = GradientNoise(skillNoiseUV,1.5);
    skillMaskNoise = Remap(skillMaskNoise,float2(0,1),float2(0.2,1));
    float skillMask = smoothstep(0,_EffectRange,distancePlayer);
    skillMask *= skillMask;
    skillMask = (1 - skillMask) * skillMaskNoise;

    //캐릭터 잔디 인터렉티브
    float characterMask = smoothstep(0,_Interaction_Distance,distancePlayer);
    characterMask *= characterMask;
    characterMask = 1 - characterMask;
    
    float finMask = lerp(characterMask, skillMask , _EffectController);

    float interactiveStrength = lerp(_Interaction_Strength, 1.5, _EffectController);
    float interactionTension = finMask * grassTension * interactiveStrength;

    //풀이 어느 방향으로 돌아갈 건지 플레이어 위치 - 풀 위치로 계산 
    float2 distanceNormalize = normalize(_GrassTargetPosition.xz - targetBaking.xz);
    float3 distancePlayerAxis = cross(float3(distanceNormalize.x, 0, distanceNormalize.y),float3(0, 1, 0));
    
    float3 interactionFin = RotateAboutAxis_Radians( positionRotate, distancePlayerAxis, interactionTension);

    //float3 fin = lerp(positionRotate, interactionFin, characterMask);
    
    float3 MoveWStoOS = interactionFin + moveOS;
    float3 bakingWStoOS = MoveWStoOS - bakingOS;
    float3 WStoOS = isBaking(_Baking,bakingWStoOS,MoveWStoOS);
    
    positionOS = float4(TransformWorldToObject(WStoOS), positionOS.w);
    
    uv = uv;
    normalOS = normalOS;
}

//Fragment 연산
void FragmentStage(AdditionalFragmentData additionalFragmentData, out SurfaceData outSurfaceData)
{
    //objectToWorld 메트릭스 
    //float4x4 transformationMatrix = GetObjectToWorldMatrix();

    // float3 moveOS = GetAbsolutePositionWS(transformationMatrix._m03_m13_m23);
    // float3 bakingOS = loaclPosition(uv2, _ConvertUnits);
    // float3 targetBaking = isBaking(_Baking,moveOS - bakingOS,moveOS);    
    //
    // //노이즈로 흐르는 느낌을 줌
    // float2 windSpeed = _Time.y * _Wind_Speed * normalize(-1 * _Wind_Direction);
    // float2 worldPositionUV = windSpeed + targetBaking.xz;
    // float windWave = GradientNoise(worldPositionUV,_Wind_Wave_Scale) * 1.2;
    // float windpattern = GradientNoise(worldPositionUV,_Wind_Pattern_Scale);
    // float windRocationFin = saturate(windpattern * windWave);;
    
    //float3 RandomColor = lerp(_Random_Color,float3(1,1,1),RandomRange(moveOS.xz, 0, 1)) * _BaseColor;

    //Ground Color
    float ground = smoothstep(_BottomColorLevel - _BottomColorFade,_BottomColorLevel + _BottomColorFade,additionalFragmentData.positionOS.y);
    ground = saturate(ground);
    float3 baseColor = lerp(_BottomColor, _BaseColor.rgb, ground);
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, additionalFragmentData.UV);
    
    //float grassTension = pow(abs(uv1.y),_Grass_Tension) * _Wind_Direction * windRocationFin * _Wind_Intensity;
    //float3 waveColor = baseMap.rgb * RandomColor + _Wave_Light_Intensity * grassTension * RandomColor;
    
    float3 normalMap = UnpackNormalScale(float4(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, additionalFragmentData.UV)), _BumpScale);
    float4 maskMap = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, additionalFragmentData.UV);
    
    outSurfaceData.albedo = baseMap.rgb * baseColor.rgb;
    outSurfaceData.metallic = 0;
    outSurfaceData.smoothness = 0;
    outSurfaceData.occlusion = saturate(maskMap.b / _AmbientOcclusion);

    outSurfaceData.normalTS = normalMap;
    outSurfaceData.emission = _EmissionColor.rgb * _EmissionColor.a * maskMap.a * _EmissionIntensity;

    float alpha = 1;
    #ifdef _ALPHATEST_ON
    alpha =  baseMap.a * _BaseColor.a;
    alpha *= 1 + max(0, CalcMipLevel(additionalFragmentData.UV * _BaseMap_TexelSize.zw)) * _MipScale;
    alpha = (alpha - _Cutoff) / max(fwidth(alpha), HALF_MIN) + 0.5;
    #endif
    
    outSurfaceData.alpha = alpha;

    //각종 고정 값들
    outSurfaceData.specular = 0;	
    outSurfaceData.clearCoatMask = 1.0;
    outSurfaceData.clearCoatSmoothness = 0;
}

#endif
