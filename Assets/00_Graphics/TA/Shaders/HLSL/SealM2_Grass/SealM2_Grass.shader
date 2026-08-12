Shader "Playwith/SealM2/Grass"
{
    Properties
    {
    	[Header (Baking)]
        [Space (10)]
    	[Toggle] _Baking("Baking",Float) = 1
        _ConvertUnits("ConvertUnits", Float) = 0.01
    	
    	[Header (Surface Options)]
        [Space (10)]
        [Toggle(_RECEIVE_SHADOWS_OFF)] _ReciveShadow("Disable Receive Shadows", Float) = 0.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Face", float) = 2.0
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Enable Alpha Clipping", float) = 0
        _Cutoff ("Alpha Clip Threshold", Range(0,1)) = 0.5
	    _MipScale ("Mip Level Alpha Scale", Range(0,1)) = 0.25
    	
//        [Header (Shadow Options)]
//    	[Space (10)]
//    	[Toggle] _EnableGlobalShadow ("Enable Global Shadows", float) = 1
//    	_MidShadowColor ("Mid Shadow Color", Color) = (0,0,0,1)
//    	_ShadowColor ("Shadow Color", Color) = (0,0,0,1)
//	    _ShadowTerm ("Shadow Term (Mid High, Mid Low, Smooth, Alpha)", Vector) = (0.5,0,0.5,1)
        
        [Header (Surface Inputs)]
        [Space (10)]
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
    	_BottomColor ("Bottom Color", Color) = (1,1,1,1)
    	_BottomColorLevel("Bottom Color Level", Float) = 0.00
    	_BottomColorFade("Bottom Color Feather", Float) = 0.00
    	///_Random_Color("Random Color", Color) =(1,1,1,1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
    	
    	// Surface Normal
	    [Space (10)]
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "white" {}
        _BumpScale ("Normal Strength", Range(-3.0, 3.0)) = 1.0
    	
    	// Surface Mask
    	[Space (10)]
    	[NoScaleOffset] _MaskMap ("Mask Map (R=Metallic | G=Smoothness | B=AO)", 2D) = "white" {}
        //_Smoothness ("Smoothness", Range(0, 1)) = 1.0
        //_Metallic ("Metallic", Range(0, 1)) = 1.0
	    _AmbientOcclusion ("Ambient Occlusion", Range(0, 1)) = 1.0
    	
	    [Header (Emission Effect)]
    	[Space (10)]
    	_EmissionIntensity ("Intensity", Float) = 0
    	[HDR]_EmissionColor ("Emission", Color) = (0,0,0,0)
    	
    	[Header (Grass Options)]
    	[Space (10)]
	    _Grass_Tension("Grass Tension", Float) = 1
	    _Wind_Direction("Wind Direction", Vector) = (1, 0, 0, 0)
        _Wind_Speed("Wind Speed", Float) = 0.5
    	_Wind_Wave_Scale("Wind Wave Scale",Vector) = (3,0.05,0,0)
    	_Wind_Pattern_Scale("Wind Pattern Scale", Float) = 1.5
	    _Wind_Intensity("Wind Intensity", Float) = 0.8
	    //_Wave_Light_Intensity("Wave Light Intensity", Range(0,5)) = 1.5
    	
      	[Header (Interaction Options)]
    	[Space (10)]  	
    	_Interaction_Distance("Interaction Distance",Float) = 0.8
    	_Interaction_Strength("Interaction Strength",Float) = 0.7
    	
    }
	

    SubShader
    {
    	
        //전체 태그
        Tags 
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue"="AlphaTest"
   		}
        
		Pass
		{
            Name "Universal Forward"
            //Pass 태그
            Tags { "Lightmode" = "UniversalForward" }

            Cull [_Cull]
            Blend One Zero
            ZTest LEqual
            ZWrite On
			//MSAA 옵션이므로, MSAA 옵션이 활성화 되었을때 Define 처리가 필요하다
			AlphaToMask [_AlphaClip]
            
            HLSLPROGRAM
		    //Vertex, Fragment Shader
			#pragma vertex LitPassVert
            #pragma fragment LitPassFrag
            
            //키워드 세팅
		    //Material Keyword
		    #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
		    #pragma shader_feature_local _ALPHATEST_ON
		    
			//Pipeline Keyword
		    #pragma multi_compile _ INDIRECT_INSTANCING_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN		    
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS

            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

		    //--------------------------------------
            // GPU Instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
		    #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
		    
            //Include 세팅
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            //실제 연산 세팅
		    #include "Assets/00_Graphics/TA/Shaders/HLSL/SealM2_Grass/Grass_LitInput.hlsl"
		    #include "Assets/00_Graphics/TA/Shaders/HLSL/SealM2_Grass/Grass_LitForwardPass.hlsl"
		    ENDHLSL
		}//Universal Forward Pass End
          	
    	Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
			#pragma multi_compile _ INDIRECT_INSTANCING_ON
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            
            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            
            // GPU Instancing
		    #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            
            // -------------------------------------
            // Includes
            #include "Grass_LitInput.hlsl"
			#if defined(LOD_FADE_CROSSFADE)
			    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
			#endif
            
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
			    float4 positionCS  : SV_POSITION;
			    float2 uv          : TEXCOORD0;
			    half3 normalWS     : TEXCOORD1;
			    half4 tangentWS    : TEXCOORD2;
			    half3 positionWS : TEXCOORD3;
				float2 uv1 : TEXCOORD4;
				float2 uv2 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			// Varyings DepthNormalsVertex(Attributes i)
			// {
			// 	Varyings o = (Varyings)0;
			
			// 	UNITY_SETUP_INSTANCE_ID(i);
			// 	UNITY_TRANSFER_INSTANCE_ID(i, o);
				
			// 	o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
			// 	VertexNormalInputs normalInputs = GetVertexNormalInputs(i.normalOS, i.tangentOS);
			// 	o.normalWS = normalInputs.normalWS;
			// 	real sign = i.tangentOS.w ;
			// 	const half4 tangentWS = half4(normalInputs.tangentWS.xyz, sign);
			// 	o.tangentWS = tangentWS;
				
			// 	VertexPositionInputs vertexInputs = GetVertexPositionInputs(i.positionOS.xyz);
			
			// 	float3 positionWS = vertexInputs.positionWS;
			
			// 	//objectToWorld 메트릭스 
			// 	float4x4 transformationMatrix = GetObjectToWorldMatrix();
			
			// 	//오브젝트 위치 가져오기
			// 	float3 moveOS = GetAbsolutePositionWS(transformationMatrix._m03_m13_m23);
			
			// 	//맥스 스크립트로 위치값 베이킹 된 오브젝트일 떄 사용
			// 	float3 bakingOS = loaclPosition(i.uv2, _ConvertUnits);
			
			// 	//베이킹 유무 
			// 	float3 eachOS = positionWS - moveOS;
			// 	float3 PosOSBaking = isBaking(_Baking,bakingOS + eachOS,eachOS);    
			// 	float3 targetBaking = isBaking(_Baking,moveOS - bakingOS,moveOS);
				
			// 	//Wind Axis 바람이 불어오는 위치 지정
			// 	float3 WindAxis = cross(float3(0,1,0), normalize(float3(_Wind_Direction.x, 0, _Wind_Direction.y)));
			
			// 	//노이즈로 바람 제어
			// 	float2 windSpeed = _Time.y * _Wind_Speed * normalize(-1 * _Wind_Direction);
			// 	float2 worldPositionUV = windSpeed + targetBaking.xz;
			// 	float windWave = GradientNoise(worldPositionUV,_Wind_Wave_Scale) * 1.2;
			// 	float windpattern = GradientNoise(worldPositionUV,_Wind_Pattern_Scale);
			// 	float windRocationFin = saturate(windpattern * windWave);
					
			// 	//풀이 심어진 곳에서 바닥 부분이 떨어지지 않도록 함, 바람 세기 조절
			// 	float grassTension = pow(abs(i.uv1.y),_Grass_Tension);
			// 	float2 windFin = grassTension * windRocationFin * _Wind_Intensity;
			// 	float3 positionRotate = RotateAboutAxis_Radians(PosOSBaking, WindAxis, windFin);
			
			// 	//풀 스쳐지나가기 
			// 	//고꾸라질 풀 범위 설정. 거리 비교 시 풀 주변은 항상 1~인 상황. 1~인 부분이 풀이랑 상호작용되기 때문에 반전 시켜서 플레이어 주변이 1~가 되도록 함
			// 	//float2 distancePlayer= saturate(clamp(Remap(distance(_Player.xz, targetBaking.xz),float2(0, _Interaction_Distance), float2(1, 0)), 0, _Interaction_Distance) * _Interaction_Strength);
			// 	float2 distancePlayer= saturate(clamp(1 - distance(_GrassTargetPosition.xz, targetBaking.xz)/_Interaction_Distance, 0, _Interaction_Distance) * _Interaction_Strength);
			
			// 	//풀이 자꾸 늘어나보여서...존 구기기
			// 	float3 interactionTension = float3(distancePlayer.x, grassTension * 0.6, distancePlayer.y);
			
			// 	//풀이 어느 방향으로 돌아갈 건지 플레이어 위치 - 풀 위치로 계산 
			// 	float2 distanceNormalize = normalize(_GrassTargetPosition.xz - targetBaking.xz);
			// 	float3 distancePlayerAxis = cross(float3(distanceNormalize.x, 0, distanceNormalize.y),float3(0, 1, 0));
				
			// 	float3 interactionFin = RotateAboutAxis_Radians( positionRotate, distancePlayerAxis, interactionTension);
			
			// 	float3 fin = lerp(positionRotate, interactionFin, interactionTension);
				
			// 	float3 MoveWStoOS = fin + moveOS;
			// 	float3 bakingWStoOS = MoveWStoOS - bakingOS;
			// 	float3 WStoOS = isBaking(_Baking,bakingWStoOS,MoveWStoOS);
				
			// 	i.positionOS = float4(TransformWorldToObject(WStoOS), i.positionOS.w);
			// 	o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
			// 	o.positionCS = TransformWorldToHClip(o.positionWS);
							
			// 	return o;
			// }

			Varyings DepthNormalsVertex(Attributes i)
			{
			    Varyings o = (Varyings)0;

				UNITY_SETUP_INSTANCE_ID(i);
			    UNITY_TRANSFER_INSTANCE_ID(i, o);

			#if defined(INDIRECT_INSTANCING_ON)				
				float4x4 modelMatrix = _InstanceTransforms[i.instanceID];
				VertexPositionInputs vertexInputs = GetVertexPositionInputs_Matrix(modelMatrix, i.positionOS.xyz);
				VertexNormalInputs normalInputs = GetVertexNormalInputs_Matrix(modelMatrix, i.tangentOS);														    
			    float3 moveOS = GetAbsolutePositionWS(modelMatrix._m03_m13_m23);
			#else				
			    VertexPositionInputs vertexInputs = GetVertexPositionInputs(i.positionOS.xyz);
				VertexNormalInputs normalInputs = GetVertexNormalInputs(i.normalOS, i.tangentOS);
				float4x4 transformationMatrix = GetObjectToWorldMatrix();			    
			    float3 moveOS = GetAbsolutePositionWS(transformationMatrix._m03_m13_m23);				
			#endif
			   				
			    o.uv = TRANSFORM_TEX(i.uv, _BaseMap);				

				float3 positionWS = vertexInputs.positionWS;
				o.normalWS = normalInputs.normalWS;
				
				real sign = i.tangentOS.w ;
				const half4 tangentWS = half4(normalInputs.tangentWS.xyz, sign);
				o.tangentWS = tangentWS;
			    //objectToWorld 메트릭스 
			   

			    //맥스 스크립트로 위치값 베이킹 된 오브젝트일 떄 사용
			    float3 bakingOS = loaclPosition(i.uv2, _ConvertUnits);

			    //베이킹 유무 
			    float3 eachOS = positionWS - moveOS;
			    float3 PosOSBaking = isBaking(_Baking,bakingOS + eachOS,eachOS);    
			    float3 targetBaking = isBaking(_Baking,moveOS - bakingOS,moveOS);
			    
			    //Wind Axis 바람이 불어오는 위치 지정
			    float3 WindAxis = cross(float3(0,1,0), normalize(float3(_Wind_Direction.x, 0, _Wind_Direction.y)));

			    //노이즈로 바람 제어
			    float2 windSpeed = _Time.y * _Wind_Speed * normalize(-1 * _Wind_Direction);
			    float2 worldPositionUV = windSpeed + targetBaking.xz;
			    float windWave = GradientNoise(worldPositionUV,_Wind_Wave_Scale) * 1.2;
			    float windpattern = GradientNoise(worldPositionUV,_Wind_Pattern_Scale);
			    float windRocationFin = saturate(windpattern * windWave);
			        
			    //풀이 심어진 곳에서 바닥 부분이 떨어지지 않도록 함, 바람 세기 조절
			    float grassTension = pow(abs(i.uv1.y),_Grass_Tension);
			    float2 windFin = grassTension * windRocationFin * _Wind_Intensity;
			    float3 positionRotate = RotateAboutAxis_Radians(PosOSBaking, WindAxis, windFin);

			    //풀 스쳐지나가기 
			    //고꾸라질 풀 범위 설정. 거리 비교 시 풀 주변은 항상 1~인 상황. 1~인 부분이 풀이랑 상호작용되기 때문에 반전 시켜서 플레이어 주변이 1~가 되도록 함
			    //float2 distancePlayer= saturate(clamp(Remap(distance(_Player.xz, targetBaking.xz),float2(0, _Interaction_Distance), float2(1, 0)), 0, _Interaction_Distance) * _Interaction_Strength);
			    float2 distancePlayer= saturate(clamp(1 - distance(_GrassTargetPosition.xz, targetBaking.xz)/_Interaction_Distance, 0, _Interaction_Distance) * _Interaction_Strength);

			    //풀이 자꾸 늘어나보여서...존 구기기
			    float3 interactionTension = float3(distancePlayer.x, grassTension * 0.6, distancePlayer.y);

			    //풀이 어느 방향으로 돌아갈 건지 플레이어 위치 - 풀 위치로 계산 
			    float2 distanceNormalize = normalize(_GrassTargetPosition.xz - targetBaking.xz);
			    float3 distancePlayerAxis = cross(float3(distanceNormalize.x, 0, distanceNormalize.y),float3(0, 1, 0));
			    
			    float3 interactionFin = RotateAboutAxis_Radians( positionRotate, distancePlayerAxis, interactionTension);

			    float3 fin = lerp(positionRotate, interactionFin, interactionTension);
			    
			    float3 MoveWStoOS = fin + moveOS;
			    float3 bakingWStoOS = MoveWStoOS - bakingOS;
			    float3 WStoOS = isBaking(_Baking,bakingWStoOS,MoveWStoOS);

				#if defined(INDIRECT_INSTANCING_ON)
					//i.positionOS = float4(TransformWorldToObject(WStoOS), i.positionOS.w);
					o.positionWS = positionWS;				
					o.positionCS = TransformWorldToHClip(o.positionWS);				
				#else
					i.positionOS = float4(TransformWorldToObject(WStoOS), i.positionOS.w);
					o.positionWS = vertexInputs.positionWS;
					o.positionCS = vertexInputs.positionCS;
				#endif

			    return o;
			}

			void DepthNormalsFragment( Varyings i , out half4 outNormalWS : SV_Target0
			                            #ifdef _WRITE_RENDERING_LAYERS
			                                , out float4 outRenderingLayers : SV_Target1
			                            #endif
			                            )
			{

				UNITY_SETUP_INSTANCE_ID(i);
				
			    SurfaceData surfaceData = (SurfaceData)0;
			    AdditionalFragmentData additionalFragmentData = (AdditionalFragmentData)0;
			    additionalFragmentData.UV = i.uv;
			    additionalFragmentData.positionCS = i.positionCS;
			    additionalFragmentData.normalWS = i.normalWS;
			    additionalFragmentData.positionWS = i.positionWS;
			    additionalFragmentData.tangentWS = i.tangentWS;
			    
			    FragmentStage(additionalFragmentData, surfaceData);

			    #ifdef _ALPHATEST_ON
			        AlphaDiscard(surfaceData.alpha, _Cutoff);
			    #endif
			    
			    #if defined(LOD_FADE_CROSSFADE)
			        LODFadeCrossFade(i.positionCS);
			    #endif
			    
			    float3 biTangent = i.tangentWS.w * cross(i.normalWS.xyz, i.tangentWS.xyz);
			    const half3x3 TBN = half3x3(i.tangentWS.xyz, biTangent, i.normalWS.xyz);
			    outNormalWS.rgb = TransformTangentToWorld(surfaceData.normalTS, TBN);
			    outNormalWS.rgb = NormalizeNormalPerPixel(outNormalWS.rgb);
			    outNormalWS.a = 0;
			    outNormalWS = float4(1, 0, 0, 1);
			    #ifdef _WRITE_RENDERING_LAYERS
			        uint renderingLayers = GetMeshRenderingLayer();
			        outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
			    #endif
			}
            
            ENDHLSL
        }// DepthNormal Pass End
    	
    	Pass
        {
            Name "Depth"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            
            // -------------------------------------
            // Universal Pipeline keywords

            // GPU Instancing
		    #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Includes
            #include "Grass_LitInput.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
			    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
			#endif
            
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
			    float4 positionCS  : SV_POSITION;
			    float2 uv          : TEXCOORD0;
			    half3 normalWS     : TEXCOORD1;
			    half4 tangentWS    : TEXCOORD2;
			    half3 positionWS : TEXCOORD3;
				float2 uv1 : TEXCOORD4;
				float2 uv2 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};


			Varyings DepthVertex(Attributes i)
			{
			    Varyings o = (Varyings)0;

				UNITY_SETUP_INSTANCE_ID(i);
			    UNITY_TRANSFER_INSTANCE_ID(i, o);
				o.uv = TRANSFORM_TEX(i.uv, _BaseMap);

			#if defined(INDIRECT_INSTANCING_ON)				
				float4x4 modelMatrix = _InstanceTransforms[i.instanceID];				
				VertexNormalInputs normalInputs = GetVertexNormalInputs_Matrix(modelMatrix, i.tangentOS);
				o.normalWS = normalInputs.normalWS;
				real sign = i.tangentOS.w ;
				const half4 tangentWS = half4(normalInputs.tangentWS.xyz, sign);
				o.tangentWS = tangentWS;
				VertexPositionInputs vertexInputs = GetVertexPositionInputs_Matrix(modelMatrix, i.positionOS.xyz);
			#else			   
				VertexNormalInputs normalInputs = GetVertexNormalInputs(i.normalOS, i.tangentOS);
			    o.normalWS = normalInputs.normalWS;
			    real sign = i.tangentOS.w;
			    const half4 tangentWS = half4(normalInputs.tangentWS.xyz, sign);
			    o.tangentWS = tangentWS;			
			    VertexPositionInputs vertexInputs = GetVertexPositionInputs(i.positionOS.xyz);
			#endif
			    float3 positionWS = vertexInputs.positionWS;

			    //objectToWorld 메트릭스 
			    float4x4 transformationMatrix = GetObjectToWorldMatrix();

			    //오브젝트 위치 가져오기
			    float3 moveOS = GetAbsolutePositionWS(transformationMatrix._m03_m13_m23);

			    //맥스 스크립트로 위치값 베이킹 된 오브젝트일 떄 사용
			    float3 bakingOS = loaclPosition(i.uv2, _ConvertUnits);

			    //베이킹 유무 
			    float3 eachOS = positionWS - moveOS;
			    float3 PosOSBaking = isBaking(_Baking,bakingOS + eachOS,eachOS);    
			    float3 targetBaking = isBaking(_Baking,moveOS - bakingOS,moveOS);
			    
			    //Wind Axis 바람이 불어오는 위치 지정
			    float3 WindAxis = cross(float3(0,1,0), normalize(float3(_Wind_Direction.x, 0, _Wind_Direction.y)));

			    //노이즈로 바람 제어
			    float2 windSpeed = _Time.y * _Wind_Speed * normalize(-1 * _Wind_Direction);
			    float2 worldPositionUV = windSpeed + targetBaking.xz;
			    float windWave = GradientNoise(worldPositionUV,_Wind_Wave_Scale) * 1.2;
			    float windpattern = GradientNoise(worldPositionUV,_Wind_Pattern_Scale);
			    float windRocationFin = saturate(windpattern * windWave);
			        
			    //풀이 심어진 곳에서 바닥 부분이 떨어지지 않도록 함, 바람 세기 조절
			    float grassTension = pow(abs(i.uv1.y),_Grass_Tension);
			    float2 windFin = grassTension * windRocationFin * _Wind_Intensity;
			    float3 positionRotate = RotateAboutAxis_Radians(PosOSBaking, WindAxis, windFin);

			    //풀 스쳐지나가기 
			    //고꾸라질 풀 범위 설정. 거리 비교 시 풀 주변은 항상 1~인 상황. 1~인 부분이 풀이랑 상호작용되기 때문에 반전 시켜서 플레이어 주변이 1~가 되도록 함
			    //float2 distancePlayer= saturate(clamp(Remap(distance(_Player.xz, targetBaking.xz),float2(0, _Interaction_Distance), float2(1, 0)), 0, _Interaction_Distance) * _Interaction_Strength);
			    float2 distancePlayer= saturate(clamp(1 - distance(_GrassTargetPosition.xz, targetBaking.xz)/_Interaction_Distance, 0, _Interaction_Distance) * _Interaction_Strength);

			    //풀이 자꾸 늘어나보여서...존 구기기
			    float3 interactionTension = float3(distancePlayer.x, grassTension * 0.6, distancePlayer.y);

			    //풀이 어느 방향으로 돌아갈 건지 플레이어 위치 - 풀 위치로 계산 
			    float2 distanceNormalize = normalize(_GrassTargetPosition.xz - targetBaking.xz);
			    float3 distancePlayerAxis = cross(float3(distanceNormalize.x, 0, distanceNormalize.y),float3(0, 1, 0));
			    
			    float3 interactionFin = RotateAboutAxis_Radians( positionRotate, distancePlayerAxis, interactionTension);

			    float3 fin = lerp(positionRotate, interactionFin, interactionTension);
			    
			    float3 MoveWStoOS = fin + moveOS;
			    float3 bakingWStoOS = MoveWStoOS - bakingOS;
			    float3 WStoOS = isBaking(_Baking,bakingWStoOS,MoveWStoOS);
			    
			    o.positionWS = vertexInputs.positionWS;
				o.positionCS = vertexInputs.positionCS;	
				return  o;
			}

            void DepthFragment( Varyings i , out half outDepth : SV_Target0)
			{
				UNITY_SETUP_INSTANCE_ID(i);
				
			    SurfaceData surfaceData = (SurfaceData)0;
			    AdditionalFragmentData additionalFragmentData = (AdditionalFragmentData)0;
			    additionalFragmentData.UV = i.uv;
			    
			    FragmentStage(additionalFragmentData, surfaceData);

			    #ifdef _ALPHATEST_ON
			    AlphaDiscard(surfaceData.alpha, _Cutoff);
			    #endif
			    
			    #if defined(LOD_FADE_CROSSFADE)
			        LODFadeCrossFade(i.positionCS);
			    #endif

			    outDepth = float4(1, 0, 0, 1);
			}
            ENDHLSL
        }// Depth Pass End
    }
}
