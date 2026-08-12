#ifndef PLAYWITH_LIT_PASS_INPUT_INCLUDED
#define PLAYWITH_LIT_PASS_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Assets/00_Graphics/TA/Shaders/HLSL/Common/Playwith_ShadingModel.hlsl"
#include "Assets/00_Graphics/TA/Shaders/HLSL/Common/Playwith_CustomStructs.hlsl"

#if defined(LOD_FADE_CROSSFADE)
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Assets/00_Graphics/TA/Shaders/HLSL/Common/Playwith_ToonLightingData.hlsl"
// #include "../Common/Functions/ParallaxOcclusionMapping.hlsl"

#if defined(INDIRECT_INSTANCING_ON)
StructuredBuffer<float4x4> _InstanceTransforms;
#endif

struct Attributes
{
	float4 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	float2 uv : TEXCOORD0;
	float2 uv1 : TEXCOORD1;	
	float2 uv2 : TEXCOORD2;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	 	
	#if defined(INDIRECT_INSTANCING_ON)
	uint instanceID : SV_InstanceID;
	#endif
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float3 positionWS : TEXCOORD3;
	float3 normalWS : NORMAL;
	float4 tangentWS : TANGENT;
	float2 uv : TEXCOORD0;
	float2 uv1 : TEXCOORD1;
	float2 uv2 : TEXCOORD2;
	#ifdef _ADDITIONAL_LIGHTS_VERTEX
		half4 fogFactorAndVertexLight : TEXCOORD4; // x: fogFactor, yzw: vertex light
	#else
		half  fogFactor : TEXCOORD5;
	#endif
	
	float4 shadowCoord : TEXCOORD6;
	DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
	
	float3 viewDirTS : TEXCOORD8;
	float3 positionOS : TEXCOORD9;

	UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Vertex Shader
Varyings LitPassVert(Attributes i)
{
	Varyings o = (Varyings)0;
	UNITY_SETUP_INSTANCE_ID(i); 
	UNITY_TRANSFER_INSTANCE_ID(i, o);
	
#if defined(INDIRECT_INSTANCING_ON)
	//VertexStage(i.positionOS, i.normalOS, i.uv, i.uv1, i.uv2);
	float4x4 modelMatrix = _InstanceTransforms[i.instanceID];
	VertexPositionInputs vertexInputs = GetVertexPositionInputs_Matrix(modelMatrix, i.positionOS.xyz);
	VertexNormalInputs normalInputs = GetVertexNormalInputs_Matrix(modelMatrix, i.tangentOS);
#else
	VertexStage(i.positionOS, i.normalOS, i.uv, i.uv1, i.uv2);
	VertexPositionInputs vertexInputs = GetVertexPositionInputs(i.positionOS.xyz);	
	VertexNormalInputs normalInputs = GetVertexNormalInputs(i.normalOS, i.tangentOS);
#endif

	//기본 UV는 무조건 BaseMap을 따라감
	o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
	o.uv1 = i.uv1;
	o.uv2 = i.uv2;

	//NormalWS, TangentWS 연산
	o.normalWS = normalInputs.normalWS;
	real sign = i.tangentOS.w ;
	const half4 tangentWS = half4(normalInputs.tangentWS.xyz, sign);
	o.tangentWS = tangentWS;
	
	const half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInputs.positionWS);
	o.viewDirTS = GetViewDirectionTangentSpace(o.tangentWS, o.normalWS, viewDirWS);

	//실제 사용되는 Fog Factor는 Fragment에서 생성함
	//Fog가 Vertex에서 연산되는 경우는 현재 없음
	half fogFactor = 0;

	// 가벼워지긴 하겠지만 퀄리티가 너무 떨어지는 편이라 사용하지 않을 것으로 예상
	#ifdef _ADDITIONAL_LIGHTS_VERTEX
		half3 vertexLight = VertexLighting(vertexInputs.positionWS, normalInputs.normalWS);
		o.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
	#else
		o.fogFactor = fogFactor;
	#endif
		
	o.shadowCoord = GetShadowCoord(vertexInputs);
	o.vertexSH = SampleSHVertex(o.normalWS);
	
	o.positionWS = vertexInputs.positionWS;
	o.positionCS = vertexInputs.positionCS;
	o.positionOS = i.positionOS;
    return o;
}

void InitToonLightingData(inout ToonLightingData toonLightingData)
{
	// if(_EnableGlobalShadow)
	// {
	// 	toonLightingData.midShadowColor = _GlobalMidShadowColor.rgb;
	// 	toonLightingData.shadowColor = _GlobalShadowColor.rgb;
	// 	toonLightingData.shadowTerm = _GlobalShadowTerm;   
	// }
	// else
	// {
	// 	toonLightingData.midShadowColor = _MidShadowColor.rgb;
	// 	toonLightingData.shadowColor = _ShadowColor.rgb;
	// 	toonLightingData.shadowTerm = _ShadowTerm;    
	// }
}

void InitInputData(Varyings i, half3 normalTS, out InputData inputData)
{
	inputData = (InputData) 0;

	//Position
	inputData.positionWS = i.positionWS;
	inputData.positionCS = i.positionCS;

	//NormalWS
	float3 biTangent = i.tangentWS.w * cross(i.normalWS.xyz, i.tangentWS.xyz);
	const half3x3 TBN = half3x3(i.tangentWS.xyz, biTangent, i.normalWS.xyz);
	
	inputData.normalWS = lerp(i.normalWS, TransformTangentToWorld(normalTS, TBN), _BumpScale);
	inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);

	//viewDirectionWS
	inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
	
	//shadowCoord
	#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
	inputData.shadowCoord = i.shadowCoord;
	#else 
	inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
	#endif

	//fog & vertexLighting
	#ifdef _ADDITIONAL_LIGHTS_VERTEX
	inputData.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactorAndVertexLight.x);
	inputData.vertexLighting = i.fogFactorAndVertexLight.yzw;
	#else
	inputData.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
	#endif
	
	inputData.bakedGI = SampleSHPixel(i.vertexSH, inputData.normalWS);
	inputData.shadowMask = SAMPLE_SHADOWMASK(i.staticLightmapUV);
	
	inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
	
	//Support Rendering Debugger
	#if defined(DEBUG_DISPLAY)
	#if defined(DYNAMICLIGHTMAP_ON)
	inputData.dynamicLightmapUV = i.dynamicLightmapUV;
	#endif
	#if defined(LIGHTMAP_ON)
	inputData.staticLightmapUV = i.staticLightmapUV;
	#else
	inputData.vertexSH = i.vertexSH;
	#endif
	#endif
}

//Fragment Shader
void LitPassFrag(Varyings i, out half4 outColor : SV_Target)
{
	UNITY_SETUP_INSTANCE_ID(i);
	
	// #if defined(PARALLAXOCCLUSION)
	// i.uv = ApplyParallax(i.viewDirTS, i.uv, _ParallaxStrength, _ParallaxOcclusionStep);
	// #endif
	//Setting SurfaceData
	SurfaceData surfaceData = (SurfaceData)0;
	AdditionalFragmentData additionalFragmentData = (AdditionalFragmentData)0;
	additionalFragmentData.UV = i.uv;
	additionalFragmentData.positionCS = i.positionCS;
	additionalFragmentData.normalWS = i.normalWS;
	additionalFragmentData.positionWS = i.positionWS;
	additionalFragmentData.tangentWS = i.tangentWS;
	additionalFragmentData.positionOS = i.positionOS;
    
	FragmentStage(additionalFragmentData, surfaceData);
	#ifdef _ALPHATEST_ON
	// clip(surfaceData.alpha - _Cutoff);
	AlphaDiscard(surfaceData.alpha, _Cutoff);
	#endif

	ToonLightingData toonLightingData = (ToonLightingData)0;
	InitToonLightingData(toonLightingData);
	
	#ifdef LOD_FADE_CROSSFADE
		LODFadeCrossFade(i.positionCS);
	#endif
	
	//Setting InputData
	InputData inputData;
	InitInputData(i, surfaceData.normalTS, inputData);
	

	//Support Dbuffer Decal
	#ifdef _DBUFFER
		ApplyDecalToSurfaceData(i.positionCS, surfaceData, inputData);
	#endif
	
	//Custom Shading Model
	half4 color = ToonPBR(inputData, surfaceData, toonLightingData);
	
	//Fog
	color.rgb = MixFog(color.rgb, inputData.fogCoord);
	outColor = color;
}
#endif