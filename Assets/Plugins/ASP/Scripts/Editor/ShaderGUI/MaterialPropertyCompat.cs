// Copyright (c) ASP
// Cross-version compatibility shim for MaterialProperty type/flag APIs.
using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering;
#endif

namespace LWGUI
{
	public static class MaterialPropertyCompat
	{
		public static bool IsFloat(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return prop.propertyType == ShaderPropertyType.Float;
#else
			return prop.type == MaterialProperty.PropType.Float;
#endif
		}

		public static bool IsRange(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return prop.propertyType == ShaderPropertyType.Range;
#else
			return prop.type == MaterialProperty.PropType.Range;
#endif
		}

		public static bool IsTexture(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return prop.propertyType == ShaderPropertyType.Texture;
#else
			return prop.type == MaterialProperty.PropType.Texture;
#endif
		}

		public static bool IsColor(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return prop.propertyType == ShaderPropertyType.Color;
#else
			return prop.type == MaterialProperty.PropType.Color;
#endif
		}

		public static bool IsVector(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return prop.propertyType == ShaderPropertyType.Vector;
#else
			return prop.type == MaterialProperty.PropType.Vector;
#endif
		}

#if UNITY_2021_1_OR_NEWER
		public static bool IsInt(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return prop.propertyType == ShaderPropertyType.Int;
#else
			return prop.type == MaterialProperty.PropType.Int;
#endif
		}
#endif

		public static bool IsHDR(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return (prop.propertyFlags & ShaderPropertyFlags.HDR) != ShaderPropertyFlags.None;
#else
			return (prop.flags & MaterialProperty.PropFlags.HDR) != MaterialProperty.PropFlags.None;
#endif
		}

		public static bool IsHideInInspector(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return (prop.propertyFlags & ShaderPropertyFlags.HideInInspector) != ShaderPropertyFlags.None;
#else
			return (prop.flags & MaterialProperty.PropFlags.HideInInspector) != 0;
#endif
		}

		/// <summary>Returns the property type as a display string (for log messages).</summary>
		public static string GetPropTypeDisplayString(this MaterialProperty prop)
		{
#if UNITY_6000_1_OR_NEWER
			return prop.propertyType.ToString();
#else
			return prop.type.ToString();
#endif
		}
	}
}
