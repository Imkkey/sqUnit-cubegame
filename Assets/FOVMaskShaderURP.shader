Shader "Custom/FOVMaskShaderURP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FOVAngle ("FOV Angle", Float) = 90
        _FOVRadius ("FOV Radius", Float) = 5
        _PlayerPos ("Player Position", Vector) = (0, 0, 0, 0)
        _TeamID ("Team ID", Float) = 0
        _ViewerTeamID ("Viewer Team ID", Float) = 0
        _TeamColor ("Team Color", Color) = (1, 1, 1, 1)
        _WallDistance ("Wall Distance", Float) = 0
        _IsBullet ("Is Bullet", Float) = 0 // 1 для пуль, 0 для игроков
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float _FOVAngle;
            float _FOVRadius;
            float4 _PlayerPos;
            float _TeamID;
            float _ViewerTeamID;
            float4 _TeamColor;
            float _WallDistance;
            float _IsBullet;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                col *= _TeamColor;

                // Вычисляем направление и расстояние до пикселя
                float2 dir = IN.worldPos.xy - _PlayerPos.xy;
                float distance = length(dir);
                float angle = degrees(atan2(dir.y, dir.x)) - _PlayerPos.z;
                angle = abs(angle) > 180 ? abs(angle) - 360 : angle;

                // Для пуль игнорируем совпадение команд, всегда проверяем FOV
                if (_IsBullet == 0 && (_TeamID == _ViewerTeamID || _TeamID == 0))
                {
                    return col; // Игроки одной команды видят друг друга
                }

                // Скрываем, если вне FOV или за стеной
                if (distance > _FOVRadius || abs(angle) > _FOVAngle / 2 || distance > _WallDistance)
                {
                    col.a = 0;
                }

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}