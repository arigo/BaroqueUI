//======= Copyright (c) Valve Corporation, All rights reserved. ===============
//
// Purpose: Used to show the outline of the object
//
//=============================================================================

// With additional modifications from Armin Rigo for BaroqueUI


// UNITY_SHADER_NO_UPGRADE
Shader "BaroqueUI/FromValve/Silhouette"
{
	//-------------------------------------------------------------------------------------------------------------------------------------------------------------
	Properties
	{
		g_vOutlineColor( "Outline Color", Color ) = ( .5, .5, .5, 1 )
		g_vMaskedOutlineColor("Masked Outline Color", Color) = (.4, .4, .4, 1)
		g_flOutlineWidth( "Outline width (world units)", Range ( .001, 0.03 ) ) = 0.0042
	}

	//-------------------------------------------------------------------------------------------------------------------------------------------------------------
	CGINCLUDE

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		#pragma target 5.0

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		#include "UnityCG.cginc"
		#include "UnityShaderVariables.cginc"

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		float4 g_vOutlineColor;
		float4 g_vMaskedOutlineColor;
		float g_flOutlineWidth;

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		struct VS_INPUT
		{
			float4 vPositionOs : POSITION;
			float3 vNormalOs : NORMAL;
		};

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		struct PS_INPUT
		{
			float3 vPositionOs : TEXCOORD0;
			float3 vNormalOs : TEXCOORD1;
			float4 vPositionPs : SV_POSITION;
		};

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		PS_INPUT MainVs( VS_INPUT i )
		{
			PS_INPUT o;

#if UNITY_VERSION >= 540
			o.vPositionPs = UnityObjectToClipPos(i.vPositionOs.xyzw);
#else
			o.vPositionPs = mul(UNITY_MATRIX_MVP, i.vPositionOs.xyzw);
#endif
			o.vPositionOs = i.vPositionOs;
			o.vNormalOs = i.vNormalOs;

			return o;
		}

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		PS_INPUT Extrude( PS_INPUT i )
		{
			PS_INPUT extruded = i;

			float3 normal = mul(UNITY_MATRIX_M, float4(i.vNormalOs.xyz, 0.0));
			normal = normalize(normal) * g_flOutlineWidth;
			float3 p = mul(UNITY_MATRIX_M, float4(i.vPositionOs.xyz, 1.0)) + normal;
			extruded.vPositionPs = mul(UNITY_MATRIX_VP, float4(p, 1.0));
			return extruded;
		}

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		[maxvertexcount(18)]
		void ExtrudeGs( triangle PS_INPUT inputTriangle[3], inout TriangleStream<PS_INPUT> outputStream )
		{
		    PS_INPUT extrudedTriangle0 = Extrude( inputTriangle[0] );
		    PS_INPUT extrudedTriangle1 = Extrude( inputTriangle[1] );
		    PS_INPUT extrudedTriangle2 = Extrude( inputTriangle[2] );

		    outputStream.Append( inputTriangle[0] );
		    outputStream.Append( extrudedTriangle0 );
		    outputStream.Append( extrudedTriangle1 );
		    outputStream.Append( inputTriangle[0] );
		    outputStream.Append( extrudedTriangle1 );
		    outputStream.Append( inputTriangle[1] );

		    outputStream.Append( inputTriangle[1] );
		    outputStream.Append( extrudedTriangle1 );
		    outputStream.Append( extrudedTriangle2 );
		    outputStream.Append( inputTriangle[1] );
		    outputStream.Append( extrudedTriangle2 );
		    outputStream.Append( inputTriangle[2] );

		    outputStream.Append( inputTriangle[2] );
		    outputStream.Append( extrudedTriangle2 );
		    outputStream.Append( extrudedTriangle0 );
		    outputStream.Append( inputTriangle[2] );
		    outputStream.Append( extrudedTriangle0 );
		    outputStream.Append( inputTriangle[0] );
		}

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		fixed4 MainPs( PS_INPUT i ) : SV_Target
		{
			return g_vOutlineColor;
		}

		fixed4 MaskedPs(PS_INPUT i) : SV_Target
		{
			return g_vMaskedOutlineColor;
		}

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		fixed4 NullPs( PS_INPUT i ) : SV_Target
		{
			return float4( 1.0, 0.0, 1.0, 1.0 );
		}

	ENDCG

	SubShader
	{
		Tags { "RenderType"="Outline" "Queue" = "Geometry+50"  }

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		// Render the object with stencil=1 to mask out the part that isn't the silhouette
		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		Pass
		{
			Tags { "LightMode" = "Always" }
			ColorMask 0
			Cull Off
			ZTest Always
			ZWrite Off
			Stencil
			{
				Ref 1
				Comp always
				Pass replace
			}
		
			CGPROGRAM
				#pragma vertex MainVs
				#pragma fragment NullPs
			ENDCG
		}

		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		// Render the outline by extruding along vertex normals and using the stencil mask previously rendered. Only render depth, so that the final pass executes
		// once per fragment (otherwise alpha blending will look bad).
		//-------------------------------------------------------------------------------------------------------------------------------------------------------------
		Pass
		{
			Tags{ "LightMode" = "Always" }
			Cull Off
			ZTest Greater
			Offset -1, -1
			ZWrite Off
			Stencil
			{
				Ref 1
				Comp notequal
				Pass keep
				Fail keep
			}
			CGPROGRAM
				#pragma vertex MainVs
				#pragma geometry ExtrudeGs
				#pragma fragment MaskedPs
			ENDCG
		}
		Pass
		{
			Tags { "LightMode" = "Always" }
			Cull Off
			ZTest LEqual
			Offset -1, -1
			ZWrite On
			Stencil
			{
				Ref 1
				Comp notequal
				Pass keep
				Fail keep
			}

			CGPROGRAM
				#pragma vertex MainVs
				#pragma geometry ExtrudeGs
				#pragma fragment MainPs
			ENDCG
		}
	}
}
