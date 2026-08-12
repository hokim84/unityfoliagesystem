#ifndef PLAYWITH_BRDF_INCLUDED
#define PLAYWITH_BRDF_INCLUDED

#include "../Common/Playwith_Common.hlsl"
#include "../Common/Playwith_ToonLightingData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

//Unity의 BRDF Data 초기화와 완전히 동일한 상태
inline void InitBRDFData(inout SurfaceData surfaceData, out BRDFData outBRDFData)
{
    half oneMinusReflectivity = kDielectricSpec.a - surfaceData.metallic * kDielectricSpec.a;
    half reflectivity = half(1.0) - oneMinusReflectivity;
    half3 brdfDiffuse = surfaceData.albedo * oneMinusReflectivity;
    half3 brdfSpecular = lerp(kDielectricSpec.rgb, surfaceData.albedo, surfaceData.metallic);

    outBRDFData = (BRDFData)0;
    outBRDFData.albedo = surfaceData.albedo;
    outBRDFData.diffuse = brdfDiffuse;
    outBRDFData.specular = brdfSpecular;
    outBRDFData.reflectivity = reflectivity;
    //smoothness로 가져왔지만 Roughness로 전환하여 계산
    //real은 모바일이거나 스위치인 경우 half, 그렇지 않은 경우 float - Common.hlsl
    outBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surfaceData.smoothness);
    outBRDFData.roughness = max(outBRDFData.perceptualRoughness * outBRDFData.perceptualRoughness, HALF_MIN_SQRT);
    outBRDFData.roughness2 = max(outBRDFData.roughness * outBRDFData.roughness, HALF_MIN);
    outBRDFData.grazingTerm = saturate(surfaceData.smoothness + reflectivity);
    outBRDFData.normalizationTerm = outBRDFData.roughness * half(4.0) + half(2.0);
    outBRDFData.roughness2MinusOne = outBRDFData.roughness2 - half(1.0);
}

half LambertModel(float3 normalWS, float3 lightDirection)
{
    return dot(normalWS, lightDirection);
}

half HalfLambertModel(float3 normalWS, float3 lightDirection)
{
    return dot(normalWS, lightDirection) * 0.5 + 0.5;
}

half CustomLambertModel(float3 normalWS, float3 lightDirection, half Threshold, half Smooth)
{
    //HalfLambert
    float dotNL = dot(normalWS, lightDirection) * 0.5 + 0.5;
    float CalculateSmooth = saturate(0.59 + (Threshold - 0.1) * ((0.057 - 0.59) / (0.9 - 0.1)));
    return saturate(Smoother(dotNL, Threshold, min(CalculateSmooth, Smooth)));
}

half URPSpecularTerm(BRDFData brdfData, InputData inputData, Light light)
{
    //Light 구조체의 direction이 half3이기 때문에 발생하는 정밀도 문제 해결
    float3 floatLightDirection = float3(light.direction);
    float3 halfDir = SafeNormalize(floatLightDirection + inputData.viewDirectionWS);
    float dotNH = saturate(dot(float3(inputData.normalWS), halfDir));
    float dotLH = saturate(dot(light.direction, halfDir));
    
    float d = dotNH * dotNH * brdfData.roughness2MinusOne + 1.00001f;

    half dotLH2 = dotLH * dotLH;
    half specular = brdfData.roughness2 / ((d * d) * max(0.1h, dotLH2) * brdfData.normalizationTerm);
    #if REAL_IS_HALF
        specular = specular - HALF_MIN;
        specular = clamp(specular, 0.0, 1000.0);
    #endif
    return specular;
}

// John Hable Opti GGX3
float LightingGGX_D(float dotNH, float roughness)
{
    float alpha = roughness * roughness;
    float alphaSqr = alpha * alpha;
    float denom = dotNH * dotNH * (alphaSqr-1.0) + 1.000001f;
    return alphaSqr / (PI * denom * denom);
}

float2 LightingGGX_FV(float dotLH, float roughness)
{
    float alpha = roughness * roughness;
    // F
    float dotLH5 = pow(abs(1.0f - dotLH), 5);
    float F_a = 1.0f;
    float F_b = dotLH5;

    // V
    float k = alpha / 2.0f;
    float k2 = k * k;
    float invK2 = 1.0f - k2;
    float vis = rcp(dotLH * dotLH * invK2 + k2);

    return float2(F_a * vis, F_b * vis); 
}

half OptiGGXSpecularTerm(BRDFData brdfData, InputData inputData, Light light, float F0)
{
    float3 floatLightDirection = float3(light.direction);
    float3 halfDir = SafeNormalize(floatLightDirection + inputData.viewDirectionWS);

    F0 = lerp(0, 0.08, F0);

    float dotNL = saturate(dot(inputData.normalWS, light.direction));
    float dotLH = saturate(dot(light.direction, halfDir));
    float dotNH = saturate(dot(float3(inputData.normalWS), halfDir));

    float roughness = Remap(brdfData.roughness, float2(0, 1), float2(0.1, 0.9));

    float D = LightingGGX_D(dotNH, roughness);
    float2 FV_helper = LightingGGX_FV(dotLH, roughness);
    float FV = F0 * FV_helper.x + (1.0f - F0) * FV_helper.y;

    float specular = dotNL * D * FV;
    
    return specular;
}

half3 DefaultPBRLighting(BRDFData brdfData, InputData inputData, Light light, bool enableSpecularHighlight)
{
    float diffuseFunc = CustomLambertModel(inputData.normalWS, light.direction, 0.7, 1);
    half3 radiance = light.color * light.distanceAttenuation * light.shadowAttenuation * diffuseFunc;

    half3 brdf = brdfData.diffuse;
    #ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if(enableSpecularHighlight)
        brdf += brdfData.specular * URPSpecularTerm(brdfData, inputData ,light);
    #endif

    return brdf * radiance;
}
half3 OptiGGXPBRLighting(BRDFData brdfData, InputData inputData, Light light, bool enableSpecularHighlight)
{
    float diffuseFunc = CustomLambertModel(inputData.normalWS, light.direction, 0.7, 1) ;
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * diffuseFunc);

    half3 brdf = brdfData.diffuse ;
    #ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if(enableSpecularHighlight)
        brdf += brdfData.specular * OptiGGXSpecularTerm(brdfData, inputData ,light, 1);
    #endif

    return brdf * radiance;
}

half3 ToonPBRLighting(BRDFData brdfData, InputData inputData, Light light, ToonLightingData toonLightingData, bool enableSpecularHighlight)
{
    float diffuseFunc = LambertModel(inputData.normalWS, light.direction);
    half radiance = saturate(light.distanceAttenuation * light.shadowAttenuation * saturate(diffuseFunc));
    
    half3 brdf = brdfData.diffuse;
    #ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if(enableSpecularHighlight)
        brdf += brdfData.specular * OptiGGXSpecularTerm(brdfData, inputData ,light, 8) * radiance;
    #endif
    
    return brdf * radiance * light.color;
}
half3 ToonPBRLighting_Character(BRDFData brdfData, InputData inputData, Light light, ToonLightingData toonLightingData, bool enableSpecularHighlight)
{
    half3 brdf = brdfData.diffuse * 0.5;
    
    float lambert = toonLightingData.lambert;
    float radiance = saturate(lambert) * light.shadowAttenuation * light.distanceAttenuation;
    float shadow1 = toonLightingData.shadow1;
    float shadow2 = toonLightingData.shadow2;
    //return occlusion;
    
    [branch] if(enableSpecularHighlight)
    brdf += brdfData.specular * OptiGGXSpecularTerm(brdfData, inputData ,light, 8) * radiance * light.color;
    
    half3 shadowColor = lerp(lerp(toonLightingData.shadowColor,toonLightingData.midShadowColor,shadow2),1,shadow1) * light.color;
    
    return (brdf * shadowColor) * light.distanceAttenuation;
} 

#endif