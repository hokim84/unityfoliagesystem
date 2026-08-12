#ifndef PLAYWITH_Lighting_INCLUDED
#define PLAYWITH_Lighting_INCLUDED

#include "../Common/Playwith_BRDF.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

#if defined(LIGHTMAP_ON)
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) float2 lmName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT) OUT.xy = lightmapUV.xy * lightmapScaleOffset.xy + lightmapScaleOffset.zw;
    #define OUTPUT_SH(normalWS, OUT)
#else
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) half3 shName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT)
    #define OUTPUT_SH(normalWS, OUT) OUT.xyz = SampleSHVertex(normalWS)
#endif

struct LightingData
{
    half3 giColor;
    half3 mainLightColor;
    half3 additionalLightsColor;
    half3 vertexLightingColor;
    half3 emissionColor;
};

LightingData InitLightingData()
{
    LightingData lightingData;

    lightingData.giColor = 0;
    lightingData.vertexLightingColor = 0;
    lightingData.mainLightColor = 0;
    lightingData.additionalLightsColor = 0;
    lightingData.emissionColor = 0;

    return lightingData;
}

half3 VertexLighting(float3 positionWS, half3 normalWS)
{
    half3 vertexLightColor = half3(0.0, 0.0, 0.0);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
    uint lightsCount = GetAdditionalLightsCount();
    
        #if USE_FORWARD_PLUS
            for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
            {
                FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                Light light = GetAdditionalLight(lightIndex, positionWS);
                #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                    #endif
                {
                    half3 lightColor = light.color * light.distanceAttenuation;
                    vertexLightColor += CustomLambertModel(normalWS, light.direction, 0.7, 1) * lightColor;
                }
            }
        #endif
    
    LIGHT_LOOP_BEGIN(lightsCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);
    
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
    {
        half3 lightColor = light.color * light.distanceAttenuation;
        vertexLightColor += CustomLambertModel(normalWS, light.direction, 0.7, 1) * lightColor;
    }
    LIGHT_LOOP_END
    
    #endif
    
    return vertexLightColor;
}

half3 GlobalIllumination(BRDFData brdfData, InputData inputData, half occlusion)
{
    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV);
    half3 indirectDiffuse = inputData.bakedGI;

    //occlusion은 내부에서 사용되지 않고, color에 직접 곱해짐
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS,
                                        brdfData.perceptualRoughness, 1.0h, inputData.normalizedScreenSpaceUV);
    half3 color = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);
    if (IsOnlyAOLightingFeatureEnabled()) color = half3(1,1,1); // Rendering Debugger 지원
    return color * occlusion;
}
half3 GlobalIllumination_Character(BRDFData brdfData, InputData inputData, float occlusion)
{
    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV);
    
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS,
                                        brdfData.perceptualRoughness, 1.0h, inputData.normalizedScreenSpaceUV);
    
    half3 indirectDiffuse = inputData.bakedGI;
    half3 color = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);
    if (IsOnlyAOLightingFeatureEnabled()) color = half3(1,1,1); // Rendering Debugger 지원
    
    return color * occlusion;
}

half4 CalculateLightingColor(LightingData lightingData, half alpha)
{
    half3 lightingColor = 0;

    if (IsOnlyAOLightingFeatureEnabled())
        return half4(lightingData.giColor, 1);

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_GLOBAL_ILLUMINATION))
        lightingColor += lightingData.giColor;

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_MAIN_LIGHT))
        lightingColor += lightingData.mainLightColor;
    
    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_ADDITIONAL_LIGHTS))
        lightingColor += lightingData.additionalLightsColor;

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_VERTEX_LIGHTING))
        lightingColor += lightingData.vertexLightingColor;
    //일반 표면 라이팅 데이터 종료

    //이후 추가되는 연산들은 모두 emissionColor로 연산
    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_EMISSION))
    {
        lightingColor += lightingData.emissionColor;
    }
    
    return half4 (lightingColor, alpha);
}
#endif