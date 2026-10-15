Shader "Custom/CRTShader"
{
    Properties
    {
        _Curvature ("Curvature", Range(0, 0.5)) = 0.12
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.25
        _ScanlineCount ("Scanline Count", Float) = 360
        _ScanlineSpeed ("Scanline Scroll Speed", Float) = 0.5
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.01)) = 0.0015
        _VignetteStrength ("Vignette Strength", Range(0, 2)) = 0.6
        _Brightness ("Brightness", Range(0.5, 2)) = 1.15
        _Noise ("Noise", Range(0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "CRT"

            HLSLPROGRAM

            // Vert comes from Blit.hlsl: it builds the fullscreen triangle from
            // SV_VertexID, which is what FullScreenPassRendererFeature draws.
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Curvature;
            float _ScanlineStrength;
            float _ScanlineCount;
            float _ScanlineSpeed;
            float _ChromaticAberration;
            float _VignetteStrength;
            float _Brightness;
            float _Noise;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // Samples the camera image at a 0-1 screen position. The source texture can be
            // bigger than the screen (render textures keep their largest size), so map through
            // _BlitScaleBias and keep the sample inside the valid area.
            half3 SampleScreen(float2 screenUV)
            {
                float2 halfPixel = 0.5 / _ScreenParams.xy;
                screenUV = clamp(screenUV, halfPixel, 1.0 - halfPixel);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV * _BlitScaleBias.xy + _BlitScaleBias.zw).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Work in 0-1 screen space rather than raw texture space.
                float2 uv = (input.texcoord - _BlitScaleBias.zw) / _BlitScaleBias.xy;

                // Barrel distortion
                float2 centered = uv - 0.5;
                uv = centered * (1.0 + dot(centered, centered) * _Curvature * 4.0) + 0.5;

                // Black outside the curved screen, with the bezel edge anti-aliased over a
                // couple of pixels instead of a hard cut.
                float2 edgeWidth = 1.5 / _ScreenParams.xy;
                float2 edge = smoothstep(0.0, edgeWidth, uv) * smoothstep(0.0, edgeWidth, 1.0 - uv);
                float screenMask = edge.x * edge.y;

                if (screenMask <= 0.0)
                {
                    return half4(0, 0, 0, 1);
                }

                // Chromatic aberration: split R and B away from the centre
                float2 offset = (uv - 0.5) * _ChromaticAberration * 2.0;
                half3 colour;
                colour.r = SampleScreen(uv + offset).r;
                colour.g = SampleScreen(uv).g;
                colour.b = SampleScreen(uv - offset).b;

                // Scanlines
                float scanline = sin((uv.y + _Time.y * _ScanlineSpeed * 0.01) * _ScanlineCount * PI * 2.0) * 0.5 + 0.5;
                colour *= 1.0 - _ScanlineStrength * scanline;

                // Vignette
                float2 v = uv * (1.0 - uv);
                float vignette = saturate(pow(max(v.x * v.y * 16.0, 0.0), _VignetteStrength * 0.5));
                colour *= vignette;

                // Grain, re-rolled 24 times a second like film grain rather than every frame.
                // Clamp so dark pixels can't go negative, which corrupts the colour buffer
                // (shows as a full-screen cyan wash in dark scenes).
                float grainFrame = floor(_Time.y * 24.0);
                colour += (Hash(floor(uv * _ScreenParams.xy) + grainFrame) - 0.5) * _Noise;
                colour = max(colour, 0.0);

                return half4(colour * _Brightness * screenMask, 1);
            }

            ENDHLSL
        }
    }
}
