#ifndef PLAYWITH_SHADINGMODEL_INCLUDED
#define PLAYWITH_SHADINGMODEL_INCLUDED

#include "../Common/Playwith_Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

half4 PlaywithDefaultPBR(InputData inputData, SurfaceData surfaceData)
{
    BRDFData brdfData = (BRDFData)0;
    InitBRDFData(surfaceData, brdfData);
    bool enableSpecularHighlight = true;

    //Support DeubgDisplay
    #if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
        return debugColor;
    #endif

    //AmbientOcclusion 선택 SSAO가 없을 때에는 surfaceData.occlusion 반환. SSAO를 샘플링하기 위해 정규화된 ScreenSpaceUV 필요
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV,
                                                                    surfaceData.occlusion);
    
    //meshRenderer에서 전달받은 Mesh의 RenderingLayer
    uint meshRenderingLayers = GetMeshRenderingLayer();
    
    //MainLight = 밝기가 가장 강하거나 Sun Source로 지정된 Directional Light. 그 외는 모두 Additional Light로 처리
    Light mainLight = GetMainLight(inputData, inputData.shadowMask, aoFactor);
    
    //LightMap의 Subtractive 모드가 켜져있는 경우에만 연산. Subtractive 모드를 사용하지 않는 경우 제거해도 무방
    //Subtractive 모드일 때에 실시간 그림자를 Baked GI 위에 그리는 연산. Realtime Shadow Color를 여기서 사용
    // MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    //Lighting Data 초기화
    LightingData lightingData = (LightingData)0;
    
    // GI 연산
    lightingData.giColor = GlobalIllumination(brdfData, inputData, aoFactor.indirectAmbientOcclusion);
    
    //메인 라이트 연산
    //LightLayer가 켜져있으면 메인라이트의 레이어가 동일한 경우에만 연산하고, 그렇지 않은 경우는 이 라이트 환경을 건너 뜀 
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
        #endif
        lightingData.mainLightColor = DefaultPBRLighting(brdfData, inputData, mainLight, true);
    
    //추가 라이트 연산
    #if defined(_ADDITIONAL_LIGHTS)
        uint lightCount = GetAdditionalLightsCount();
        // Forward +인 경우
        #if USE_FORWARD_PLUS
            for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                    lightIndex++)
            {
                FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);

                #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                    #endif
                    lightingData.additionalLightsColor += DefaultPBRLighting(brdfData, inputData, light,
                                                                                enableSpecularHighlight * 0.8);
            }
        #endif

        // Forward인 경우
        LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);

        #ifdef _LIGHT_LAYERS
            if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                #endif
                lightingData.additionalLightsColor += DefaultPBRLighting(brdfData, inputData, light,
                                                                            enableSpecularHighlight) * 0.8;
        LIGHT_LOOP_END
    #endif

    //Vertex Lighting 연산
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

    //Emission 연산
    lightingData.emissionColor = surfaceData.emission;

    return CalculateLightingColor(lightingData, surfaceData.alpha);
}

half4 OptiGGXPBR(InputData inputData, SurfaceData surfaceData)
{
    BRDFData brdfData = (BRDFData)0;
    InitBRDFData(surfaceData, brdfData);
    bool enableSpecularHighlight = true;

    //Support DeubgDisplay
    #if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
        return debugColor;
    #endif

    //AmbientOcclusion 선택 SSAO가 없을 때에는 surfaceData.occlusion 반환. SSAO를 샘플링하기 위해 정규화된 ScreenSpaceUV 필요
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, surfaceData.occlusion);
    //meshRenderer에서 전달받은 Mesh의 RenderingLayer
    uint meshRenderingLayers = GetMeshRenderingLayer();
    //MainLight = 밝기가 가장 강하거나 Sun Source로 지정된 Directional Light. 그 외는 모두 Additional Light로 처리된다.
    Light mainLight = GetMainLight(inputData, inputData.shadowMask, aoFactor);
    //LightMap의 Subtractive 모드가 켜져있는 경우에만 연산. Subtractive 모드를 사용하지 않는 경우 제거해도 무방
    //Subtractive 모드일 때에 실시간 그림자를 Baked GI 위에 그리는 연산. Realtime Shadow Color를 여기서 사용.
    // MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    //Lighting Data 초기화
    LightingData lightingData = (LightingData)0;
    // GI 연산
    lightingData.giColor = GlobalIllumination(brdfData, inputData, aoFactor.indirectAmbientOcclusion);

    //메인 라이트 연산
    //LightLayer가 켜져있으면 메인라이트의 레이어가 동일한 경우에만 연산하고, 그렇지 않은 경우는 이 라이트 환경을 건너 뛴다 
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
        #endif
        lightingData.mainLightColor = OptiGGXPBRLighting(brdfData, inputData, mainLight, true);
    
    //추가 라이트 연산
    #if defined(_ADDITIONAL_LIGHTS)
        uint lightCount = GetAdditionalLightsCount();
    #if USE_FORWARD_PLUS
        for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
        {
            FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
            Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);

            #ifdef _LIGHT_LAYERS
            if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                #endif
                lightingData.additionalLightsColor += OptiGGXPBRLighting(brdfData, inputData, light, enableSpecularHighlight);
        }
    #endif

    LIGHT_LOOP_BEGIN(lightCount)
    Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            #endif
            lightingData.additionalLightsColor += OptiGGXPBRLighting(brdfData, inputData, light, enableSpecularHighlight);
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

    //Emission 연산
    lightingData.emissionColor = surfaceData.emission;
    return  CalculateLightingColor(lightingData, surfaceData.alpha);
}

half4 ToonPBR(InputData inputData, SurfaceData surfaceData, ToonLightingData toonLightingData)
{
    BRDFData brdfData = (BRDFData)0;
    InitBRDFData(surfaceData, brdfData);
    bool enableSpecularHighlight = true;

    //Support DeubgDisplay
    #if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
        return debugColor;
    #endif

    //AmbientOcclusion 선택 SSAO가 없을 때에는 surfaceData.occlusion 반환. SSAO를 샘플링하기 위해 정규화된 ScreenSpaceUV 필요
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, surfaceData.occlusion);
    //meshRenderer에서 전달받은 Mesh의 RenderingLayer
    uint meshRenderingLayers = GetMeshRenderingLayer();
    //MainLight = 밝기가 가장 강하거나 Sun Source로 지정된 Directional Light. 그 외는 모두 Additional Light로 처리된다.
    Light mainLight = GetMainLight(inputData, inputData.shadowMask, aoFactor);
    //LightMap의 Subtractive 모드가 켜져있는 경우에만 연산. Subtractive 모드를 사용하지 않는 경우 제거해도 무방
    //Subtractive 모드일 때에 실시간 그림자를 Baked GI 위에 그리는 연산. Realtime Shadow Color를 여기서 사용.
    // MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    //Lighting Data 초기화
    LightingData lightingData = (LightingData)0;
    // GI 연산
    lightingData.giColor = GlobalIllumination(brdfData, inputData, aoFactor.indirectAmbientOcclusion);
    
    //메인 라이트 연산
    //LightLayer가 켜져있으면 메인라이트의 레이어가 동일한 경우에만 연산하고, 그렇지 않은 경우는 이 라이트 환경을 건너 뛴다 
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
        #endif
        lightingData.mainLightColor = ToonPBRLighting(brdfData, inputData, mainLight, toonLightingData, true);
    
    //추가 라이트 연산
    #if defined(_ADDITIONAL_LIGHTS)
        uint lightCount = GetAdditionalLightsCount();
    #if USE_FORWARD_PLUS
        for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
        {
            FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
            Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);

            #ifdef _LIGHT_LAYERS
            if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                #endif
                lightingData.additionalLightsColor += ToonPBRLighting(brdfData, inputData, light, toonLightingData, enableSpecularHighlight);
        }
    #endif

    LIGHT_LOOP_BEGIN(lightCount)
    Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            #endif
            lightingData.additionalLightsColor += ToonPBRLighting(brdfData, inputData, light, toonLightingData, enableSpecularHighlight);
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif
    //return aoFactor.indirectAmbientOcclusion;
        //return float4(lightingData.mainLightColor,1);
    //Emission 연산
    lightingData.emissionColor = surfaceData.emission;
    return  CalculateLightingColor(lightingData, surfaceData.alpha);
}
void InitToonData(InputData inputData, Light mainLight, inout ToonLightingData toonLightingData)
{
    toonLightingData.lambert = LambertModel(inputData.normalWS, mainLight.direction);
    float halfLambert = toonLightingData.lambert * 0.5 + 0.5;
    toonLightingData.shadow1 = smoothstep(toonLightingData.shadowTerm.x - toonLightingData.shadowTerm.y, toonLightingData.shadowTerm.x ,halfLambert * saturate((mainLight.shadowAttenuation+0.1)));
    toonLightingData.shadow2 = smoothstep(toonLightingData.shadowTerm.z - toonLightingData.shadowTerm.w, toonLightingData.shadowTerm.z ,halfLambert);
    toonLightingData.ndotV = dot(inputData.normalWS, inputData.viewDirectionWS);

    float3 normal = lerp(toonLightingData.meshNormal, inputData.normalWS, toonLightingData.normalTex);
    toonLightingData.rimShadow = saturate(saturate(dot(normal, mainLight.direction)) * (mainLight.shadowAttenuation+0.2));
    toonLightingData.rimNdotV = dot(normal, inputData.viewDirectionWS);
}
half4 ToonPBR_Character(InputData inputData, SurfaceData surfaceData, inout ToonLightingData toonLightingData)
{
    BRDFData brdfData = (BRDFData)0;
    InitBRDFData(surfaceData, brdfData);
    bool enableSpecularHighlight = true;

    //Support DeubgDisplay
    #if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
        return debugColor;
    #endif

    //AmbientOcclusion 선택 SSAO가 없을 때에는 surfaceData.occlusion 반환. SSAO를 샘플링하기 위해 정규화된 ScreenSpaceUV 필요
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, surfaceData.occlusion);
    //meshRenderer에서 전달받은 Mesh의 RenderingLayer
    uint meshRenderingLayers = GetMeshRenderingLayer();
    //MainLight = 밝기가 가장 강하거나 Sun Source로 지정된 Directional Light. 그 외는 모두 Additional Light로 처리된다.
    Light mainLight = GetMainLight(inputData, inputData.shadowMask, aoFactor);
    //LightMap의 Subtractive 모드가 켜져있는 경우에만 연산. Subtractive 모드를 사용하지 않는 경우 제거해도 무방
    //Subtractive 모드일 때에 실시간 그림자를 Baked GI 위에 그리는 연산. Realtime Shadow Color를 여기서 사용.
    // MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);
    
    //Lighting Data 초기화
    LightingData lightingData = (LightingData)0;
    //라이팅, 림라이트 연산에 필요한 기본 영역 연산 미리 진행
    InitToonData(inputData,mainLight,toonLightingData);
    lightingData.giColor = GlobalIllumination_Character(brdfData, inputData, aoFactor.indirectAmbientOcclusion);
    
    //메인 라이트 연산
    //LightLayer가 켜져있으면 메인라이트의 레이어가 동일한 경우에만 연산하고, 그렇지 않은 경우는 이 라이트 환경을 건너 뛴다 
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
        #endif
        lightingData.mainLightColor = ToonPBRLighting_Character(brdfData, inputData, mainLight, toonLightingData,true);
   
    //추가 라이트 연산
    #if defined(_ADDITIONAL_LIGHTS)
        uint lightCount = GetAdditionalLightsCount();
    #if USE_FORWARD_PLUS
        for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
        {
            FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
            Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);

            #ifdef _LIGHT_LAYERS
            if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                #endif
                lightingData.additionalLightsColor += ToonPBRLighting_Character(brdfData, inputData, light, toonLightingData, true);
        }
    #endif

    LIGHT_LOOP_BEGIN(lightCount)
    Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            #endif
            lightingData.additionalLightsColor += ToonPBRLighting_Character(brdfData, inputData, light,toonLightingData, true);
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif
    
    //Emission 연산
    lightingData.emissionColor = surfaceData.emission;
    return  CalculateLightingColor(lightingData, surfaceData.alpha);
}

#endif




