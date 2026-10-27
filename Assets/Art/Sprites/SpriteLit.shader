// Pixel-art sprites standing in the lit 3D world (characters, guns). Cut out at the sprite's
// edge (alpha test), lit by the scene's lights by distance and cone only, ignoring the quad's
// facing, so a sprite is lit the same whichever side a light is on (like Gungeon's sprites),
// and shadowed by whatever stands between it and a light. Ambient light comes from the scene.
// _BaseColor tints it and _EmissionColor adds a glow, so an enemy's hit flash (a property block
// on those two) works as it does on the models. Shadows are sampled _SelfShadowBias toward each
// light so a sprite isn't shadowed by its own caster (it would go dark in the flashlight). Each
// light's falloff is capped at full strength, so a light right next to a sprite (a muzzle flash)
// can't blow it out the way an inverse-square falloff would close up. Character quads stand upright but are drawn
// stretched up the screen to undo the camera's tilt (PixelSprite); their shadows are cast at
// true height by squashing them back by _SpriteShadowSquash (a global set by PixelSprite).
// When sprites face the camera (_SpriteLean 1, CameraFollow), quads marked to lean (TEXCOORD1.x,
// set by PixelSprite on upright and standing sprites) are drawn tilted back about their origin
// so they face the camera along _SpriteUp, and are lit and cast shadows as if upright. Their depth
// is the upright plane through their origin (facing the camera, across the screen) seen along
// each pixel's own line of sight: everything north of a sprite's feet is behind it and only
// what stands south of them can cover it.
Shader "Roguelike/SpriteLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Sprite Sheet", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _AmbientBoost ("Ambient Boost", Range(0, 4)) = 1
        _SelfShadowBias ("Self Shadow Bias", Range(0, 1)) = 0.35
        _Blood ("Blood", Range(0, 1)) = 0
        _Wounds ("Wounds", Range(0, 1)) = 0
        _BloodSeed ("Blood Seed", Float) = 0
        _FrameSize ("Frame Size (px)", Vector) = (32, 32, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _Cutoff;
            half _AmbientBoost;
            half _SelfShadowBias;
            half _Blood;
            half _Wounds;
            float _BloodSeed;
            float4 _FrameSize;
            float4 _BaseMap_TexelSize;
        CBUFFER_END

        float BloodHash(float2 p)
        {
            return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        // A hurt character's blood, on whole pixels of its frame (so it sits on the art's grid and
        // keeps its place frame to frame): clotted patches of 3 px with specks round them, more as
        // _Blood rises; _Wounds (badly hurt bosses) opens dark, wet gashes in 4 px blocks.
        half3 Bloody(half3 albedo, float2 uv)
        {
            if (_Blood <= 0 && _Wounds <= 0)
                return albedo;

            float2 px = floor(uv * _BaseMap_TexelSize.zw);
            float2 local = fmod(px, max(_FrameSize.xy, 1));
            float seed = _BloodSeed * 17.31;
            float patch = BloodHash(floor(local / 3) + seed);
            float speck = BloodHash(local + seed * 1.7);
            float amount = patch * 0.75 + speck * 0.25;

            if (_Wounds > 0 && BloodHash(floor(local / 4) + seed * 3.1) < _Wounds * 0.22)
                return lerp(half3(0.09, 0.0, 0.01), half3(0.3, 0.02, 0.02), speck * speck);

            if (amount < _Blood * 0.42)
                return lerp(albedo * half3(0.5, 0.08, 0.06), speck > 0.5 ? half3(0.5, 0.04, 0.03) : half3(0.3, 0.02, 0.02), 0.75);

            return albedo;
        }

        float _SpriteShadowSquash;
        float3 _SpriteUp;
        float _SpriteLean;

        // Clip position of a sprite vertex: leaned back to face the camera when it should, with the
        // depth where its line of sight meets the upright plane through the sprite's origin.
        float4 SpriteClipPosition(float3 positionWS, float lean)
        {
            float4 upright = TransformWorldToHClip(positionWS);

            if (lean * _SpriteLean <= 0)
                return upright;

            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float rise = positionWS.y - origin.y;
            float3 leanedWS = positionWS + (_SpriteUp - float3(0, 1, 0)) * rise;
            float4 leaned = TransformWorldToHClip(leanedWS);

            // The upright plane faces the camera across the floor (its normal is the view's
            // direction flattened onto the floor).
            float3 forward = -UNITY_MATRIX_V[2].xyz;
            float3 normal = float3(forward.x, 0, forward.z);

            if (dot(normal, normal) < 0.0001)
                return leaned;

            normal = normalize(normal);
            float3 ray = unity_OrthoParams.w > 0.5 ? forward : leanedWS - _WorldSpaceCameraPos;
            float along = dot(ray, normal);

            if (abs(along) < 0.0001)
                return leaned;

            float4 onPlane = TransformWorldToHClip(leanedWS + ray * (dot(origin - leanedWS, normal) / along));
            leaned.z = onPlane.z / onPlane.w * leaned.w;
            return leaned;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 lean : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = SpriteClipPosition(output.positionWS, input.lean.x);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);
                albedo.rgb = Bloody(albedo.rgb, input.uv);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 light = SampleSH(half3(0, 1, 0)) * _AmbientBoost;
                uint meshRenderingLayers = GetMeshRenderingLayer();

                // Shadows are looked up a little way toward each light: the sprite's own shadow
                // caster lies in its plane and would otherwise shadow the sprite itself.
                Light mainLight = GetMainLight();
                mainLight.shadowAttenuation = GetMainLight(TransformWorldToShadowCoord(input.positionWS + mainLight.direction * _SelfShadowBias)).shadowAttenuation;
                #if defined(_LIGHT_LAYERS)
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
                #endif
                    light += mainLight.color * (min(mainLight.distanceAttenuation, 1) * mainLight.shadowAttenuation);

                // Forward+ (the cluster loop) brings in every light without _ADDITIONAL_LIGHTS.
                #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light additional = GetAdditionalLight(lightIndex, input.positionWS);
                    additional.shadowAttenuation = GetAdditionalLight(lightIndex, input.positionWS + additional.direction * _SelfShadowBias, half4(1, 1, 1, 1)).shadowAttenuation;
                    #if defined(_LIGHT_LAYERS)
                    if (IsMatchingLightLayer(additional.layerMask, meshRenderingLayers))
                    #endif
                        light += additional.color * (min(additional.distanceAttenuation, 1) * additional.shadowAttenuation);
                LIGHT_LOOP_END
                #endif

                return half4(albedo.rgb * light + _EmissionColor.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                // Back to the sprite's real height (the quad's origin is at its feet).
                float3 positionOS = input.positionOS.xyz;
                positionOS.y *= _SpriteShadowSquash > 0 ? _SpriteShadowSquash : 1;

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 lean : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = SpriteClipPosition(TransformObjectToWorld(input.positionOS.xyz), input.lean.x);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
