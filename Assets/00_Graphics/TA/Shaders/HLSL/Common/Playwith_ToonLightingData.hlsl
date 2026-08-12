#ifndef PLAYWITH_TOONLIGHTING_DATA_INCLUDED
#define PLAYWITH_TOONLIGHTING_DATA_INCLUDED

struct ToonLightingData
{
    half3 midShadowColor;
    half3 shadowColor;
    float4 shadowTerm;

    half rimLight;
    half normalTex;
    float3 meshNormal;
    half3 rimLightFrontColor;
    half3 rimLightBackColor;
    float rimLightRange;
    float rimLightFeather;

    float lambert;
    float shadow1;
    float shadow2;
    float rimShadow;
    float ndotV;
    float rimNdotV;
};

half3 _GlobalMidShadowColor;
half3 _GlobalShadowColor;
half4 _GlobalShadowTerm;

#endif