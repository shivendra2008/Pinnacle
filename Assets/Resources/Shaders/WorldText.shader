// Text printed on objects in the world (book titles, labels). Unity's default TextMesh material uses the GUI text
// shader, whose depth test follows the on-screen UI - so the text showed through covers, pages and shelves.
// This one is depth-tested like any other surface, single-sided, and pulled a hair towards the camera so it never
// flickers against the surface it is printed on.
Shader "Pinnacle/WorldText"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off
        Cull Back
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color;
                c.a *= tex2D(_MainTex, i.uv).a;
                clip(c.a - 0.01);
                return c;
            }
            ENDCG
        }
    }
}
