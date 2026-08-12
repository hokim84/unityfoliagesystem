#ifndef PLAYWITH_CUSTOMSTRUCTS_DATA_INCLUDED
#define PLAYWITH_CUSTOMSTRUCTS_DATA_INCLUDED

struct AdditionalFragmentData
{
    float2 UV;
    float4 positionCS;
    float3 normalWS;
    half3 normalTS;
    float3 positionWS;
    float4 tangentWS;
    float4 vertexColor;
    float3 positionOS;
    half3x3 tangentToWorld;
    float4 screenPos;
    float3 viewDirectionWS;
};

#endif