/*
MIT License

Copyright (c) 2022 Pascal Zwick

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

Shader "Hidden/TemporalAAShader"
{
    Properties
    {
		_MainTex ("Main Texture", 2D) = "white" {}
		_TemporalFade("Temporal Fade", Range(0, 1)) = 0.0
    }
    SubShader
    {
        // No culling or depth
        Cull Back ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

			sampler2D _MainTex;
			
			//sampler2D _MotionVectorTexture;
            sampler2D _CameraDepthTexture;

			sampler2D _TemporalAATexture;

			float _TemporalFade;
            float _MovementBlending;

            float4x4 _invP;
            float4x4 _FrameMatrix;

            float sampleDepth(float2 uv) {
                //float rd = _CameraDepthTexture.Load(int3(pix, 0)).x;
                float rd = tex2D(_CameraDepthTexture, uv).x;
                return Linear01Depth(rd);
            }

            /*
            Box intersection by IQ, modified for neighbourhood clamping
            https://www.iquilezles.org/www/articles/boxfunctions/boxfunctions.htm
            */
            float2 boxIntersection(in float3 ro, in float3 rd, in float3 rad)
            {
                float3 m = 1.0 / rd;
                float3 n = m * ro;
                float3 k = abs(m) * rad;
                float3 t1 = -n - k;
                float3 t2 = -n + k;

                float tN = max(max(t1.x, t1.y), t1.z);
                float tF = min(min(t2.x, t2.y), t2.z);

                return float2(tN, tF);
            }

            /*
            * GLSL Color Spaces by tobspr
            * https://github.com/tobspr/GLSL-Color-Spaces
            */
            // RGB to YCbCr, ranges [0, 1]
            float3 rgb_to_ycbcr(float3 rgb) {
                float y = 0.299 * rgb.r + 0.587 * rgb.g + 0.114 * rgb.b;
                float cb = (rgb.b - y) * 0.565;
                float cr = (rgb.r - y) * 0.713;

                return float3(y, cb, cr);
            }

            // YCbCr to RGB
            float3 ycbcr_to_rgb(float3 yuv) {
                return float3(
                    yuv.x + 1.403 * yuv.z,
                    yuv.x - 0.344 * yuv.y - 0.714 * yuv.z,
                    yuv.x + 1.770 * yuv.y
                );
            }
            //###

            float3 clipLuminance(float3 col, float3 minCol, float3 maxCol){
                float3 c0 = rgb_to_ycbcr(col);
                //float3 c1 = rgb_to_ycbcr(minCol);
                //float3 c2 = rgb_to_ycbcr(maxCol);

                //c0 = clamp(c0, c1, c2);
                c0 = clamp(c0, minCol, maxCol);

                return ycbcr_to_rgb(c0);
            }

            fixed4 frag(v2f i) : SV_Target
            {

                float4 curCol = tex2D(_MainTex, i.uv);

                //temporal reprojection
                float d0 = sampleDepth(i.uv);
                float d01 = (d0 * (_ProjectionParams.z - _ProjectionParams.y) + _ProjectionParams.y) / _ProjectionParams.z;
                float3 pos = float3(i.uv * 2.0 - 1.0, 1.0);
                float4 rd = mul(_invP, float4(pos, 1));
                rd.xyz /= rd.w;

                float4 temporalUV = mul(_FrameMatrix, float4(rd.xyz * d01, 1));
                temporalUV /= temporalUV.w;

                //float2 temporalUV = i.uv - tex2D(_MotionVectorTexture, i.uv).xy;
                float3 lastCol = tex2D(_TemporalAATexture, temporalUV*0.5+0.5).xyz;
                if (abs(temporalUV.x) > 1 || abs(temporalUV.y) > 1)
                    lastCol = curCol;
                //

                // Neighbourhood clipping
                //float3 ya = rgb_to_ycbcr(curCol);
                float3 ya = curCol;
                float3 minCol = ya;
                float3 maxCol = ya;

                for (int x = -1; x <= 1; ++x) {
                    for (int y = -1; y <= 1; ++y) {
                        float2 duv = float2(x, y) / _ScreenParams.xy;
                        //float3 s = rgb_to_ycbcr(tex2D(_MainTex, i.uv + duv).xyz);
                        float3 s = tex2D(_MainTex, i.uv + duv).xyz;

                        minCol = min(minCol, s);
                        maxCol = max(maxCol, s);
                    }
                }
                //lastCol = mul(YCoCgToRGBMatrix, clamp(mul(RGBToYCoCgMatrix, lastCol), minCol, maxCol));
                lastCol = clamp(lastCol, minCol, maxCol);
                //lastCol = clipLuminance(lastCol, minCol, maxCol);
                //

                float temporalMultiplier = exp(-_MovementBlending * length(pos.xy - temporalUV));
                float3 finalCol = lerp(curCol, lastCol, _TemporalFade * temporalMultiplier);
                //finalCol = length(minCol - maxCol).xxx;

                return float4(finalCol, 1);
            }
            ENDCG
        }
    }
}
