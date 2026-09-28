Shader "JU Assets/JU Simple Gaussian Blur [URP]"
{
    Properties
    {
        _BlurSize("Blur Size", Range(0, 10)) = 2
        _Samples("Samples", Range(4, 64)) = 24
        _Tint("Tint Color", Color) = (1,1,1,0.5)

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
    }

        SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ColorMask[_ColorMask]

        Pass
        {
            Name "UIBlur"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define GOLDEN_ANGLE 2.39996323

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float _BlurSize;
            int _Samples;
            float4 _Tint;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.screenPos = ComputeScreenPos(o.positionHCS);
                o.color = v.color;
                return o;
            }

            float3 ClampHighlights(float3 c)
            {
                return min(c, 5.0);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float2 texelSize = _ScreenParams.zw;

                float4 col = 0;
                float totalWeight = 0;

                float4 center = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
                center.rgb = ClampHighlights(center.rgb);

                col += center;
                totalWeight += 1.0;

                for (int s = 0; s < _Samples; s++)
                {
                    float t = (s + 0.5) / _Samples;
                    float r = pow(t, 0.75);
                    float theta = s * GOLDEN_ANGLE;

                    float2 dir = float2(cos(theta), sin(theta));
                    float2 offset = dir * r * _BlurSize * texelSize;

                    float weight = exp(-r * r * 4.0);

                    float4 sample = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + offset);
                    sample.rgb = ClampHighlights(sample.rgb);

                    col += sample * weight;
                    totalWeight += weight;
                }

                col /= totalWeight;

                float4 finalColor = col;

                finalColor.rgb *= _Tint.rgb * i.color.rgb;
                finalColor.a *= _Tint.a * i.color.a;

                return finalColor;
            }

            ENDHLSL
        }
    }
}