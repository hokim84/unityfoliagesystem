Shader "FoliageEngine/DensityOverlayURP"
{
    Properties
    {
        _BaseMap ("Main Texture", 2D) = "white" {}
        _BaseSDF ("Base SDF Texture", 2D) = "white" {}
        _Distance ("SDF Distance", Range(-10, 10)) = 0
        _Sampling_R ("Sampling Red", Range(0, 1)) = 0
        _Inverse_R ("Inverse Red", Range(0, 1)) = 0
        _Sampling_G ("Sampling Green", Range(0, 1)) = 0
        _Inverse_G ("Inverse Green", Range(0, 1)) = 0
        _Sampling_B ("Sampling Blue", Range(0, 1)) = 0
        _Inverse_B ("Inverse Blue", Range(0, 1)) = 0
        _Sampling_A ("Sampling Alpha", Range(0, 1)) = 0        
        _Inverse_A ("Inverse Alpha", Range(0, 1)) = 0        
        _MaskTex ("Mask Texture", 2D) = "white" {}        
        _SDFMaskTex ("SDF Mask Texture", 2D) = "white" {}
        _SDFMaskDistance ("SDF Mask Distance", Range(0, 10)) = 0
        _SDFMaskBias ("SDF Mask Bias", Range(0, 1)) = 0.5
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseScale ("Noise Scale (tiling)", Vector) = (1,1,0,0)
        _NoiseOffset ("Noise Offset", Vector) = (0,0,0,0)
        _NoiseRotation ("Noise Rotation (deg)", Range(0, 360)) = 0
        _OverlayAlpha ("Overlay Alpha", Range(0, 1)) = 1
        _Cutoff ("_Cutoff", Range(0,1)) = 0.5
        _Edge ("_Smoothness", Range(0.0, 0.5)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BaseSDF); SAMPLER(sampler_BaseSDF);            
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            TEXTURE2D(_SDFMaskTex); SAMPLER(sampler_SDFMaskTex);            
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Distance;
                float4 _NoiseTex_ST;                
                float2 _NoiseScale;
                float2 _NoiseOffset;
                float _Sampling_R;
                float _Inverse_R;
                float _Sampling_G;
                float _Inverse_G;
                float _Sampling_B;
                float _Inverse_B;
                float _Sampling_A;
                float _Inverse_A;                
                float _OverlayAlpha;
                float _NoiseRotation;
                float _Cutoff;
                float _Smoothness;
                float _SDFMaskDistance;
                float _SDFMaskBias;                
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);               
                float4 SDFColor = SAMPLE_TEXTURE2D(_BaseSDF, sampler_BaseSDF, IN.uv);
                float4 SDFMaskAlpha = SAMPLE_TEXTURE2D(_SDFMaskTex, sampler_SDFMaskTex, IN.uv);
                float4 maskAlpha = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv);
                                
                float ratioR = _Inverse_R ? (1 - baseColor.r) : baseColor.r;
                float ratioG = _Inverse_G ? (1 - baseColor.g) : baseColor.g;
                float ratioB = _Inverse_B ? (1 - baseColor.b) : baseColor.b;
                float ratioA = _Inverse_A ? (1 - baseColor.a) : baseColor.a;

                float r = saturate((ratioR * _Sampling_R) - (SDFColor.r * _Distance));
                float g = saturate((ratioG * _Sampling_G) - (SDFColor.g * _Distance));
                float b = saturate((ratioB * _Sampling_B) - (SDFColor.b * _Distance));
                float a = saturate((ratioA * _Sampling_A) - (SDFColor.a * _Distance));

                float maxValue = max(max(r, g), max(b, a));
                float edge = min(_Smoothness, _Cutoff);
                float greyscale = smoothstep(_Cutoff - edge, _Cutoff, maxValue);

                float4 finalColor = float4(1, 1, 1, greyscale);
                
                float2 noiseUV = (IN.uv * _NoiseScale) + _NoiseOffset;
                noiseUV = TRANSFORM_TEX(noiseUV, _NoiseTex);

                
                float rad = radians(_NoiseRotation);
                float s = sin(rad);
                float c = cos(rad);                
                float2 centered = noiseUV - 0.5;
                float2 rotated = float2(c * centered.x - s * centered.y, s * centered.x + c * centered.y) + 0.5;

                float4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, rotated);
                finalColor.rgba *=noise.rgba;
                                
                float maskedAlpha = saturate(SDFMaskAlpha.r - _SDFMaskBias);                
                maskedAlpha = saturate(finalColor.a - (maskedAlpha * _SDFMaskDistance));                
                maskedAlpha = saturate(maskedAlpha - maskAlpha.r);
                finalColor.a = maskedAlpha * _OverlayAlpha;
                // finalAlpha = saturate(finalAlpha - step(_Mask2Cutoff, mask2.a));
                // finalColor.a = finalAlpha * _OverlayAlpha;
                return finalColor; 
            }
            ENDHLSL
        }
    }
}