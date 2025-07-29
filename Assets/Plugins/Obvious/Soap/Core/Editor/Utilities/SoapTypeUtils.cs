using System;
using System.Collections.Generic;
using UnityEngine;


public static class SoapTypeUtils
{
    private static readonly Dictionary<string, string> intrinsicTypes = new Dictionary<string, string>
    {
        { "byte", "System.Byte" },
        { "sbyte", "System.SByte" },
        { "char", "System.Char" },
        { "decimal", "System.Decimal" }, //not serializable by unity [DO NOT USE]. Use float or double instead.
        { "double", "System.Double" },
        { "uint", "System.UInt32" },
        { "nint", "System.IntPtr" },
        { "nuint", "System.UIntPtr" },
        { "long", "System.Int64" },
        { "ulong", "System.UInt64" },
        { "short", "System.Int16" },
        { "ushort", "System.UInt16" },
        { "int", "System.Int32" },
        { "float", "System.Single" },
        { "string", "System.String" },
        { "object", "System.Object" },
        { "bool", "System.Boolean" }
    };
    
    private static readonly HashSet<Type> unityTypes = new HashSet<Type>()
    {
        typeof( string ), typeof( Vector4 ), typeof( Vector3 ), typeof( Vector2 ), typeof( Rect ),
        typeof( Quaternion ), typeof( Color ), typeof( Color32 ), typeof( LayerMask ), typeof( Bounds ),
        typeof( Matrix4x4 ), typeof( AnimationCurve ), typeof( Gradient ), typeof( RectOffset ),
        typeof( bool[] ), typeof( byte[] ), typeof( sbyte[] ), typeof( char[] ), 
        typeof( double[] ), typeof( float[] ), typeof( int[] ), typeof( uint[] ), typeof( long[] ),
        typeof( ulong[] ), typeof( short[] ), typeof( ushort[] ), typeof( string[] ),
        typeof( Vector4[] ), typeof( Vector3[] ), typeof( Vector2[] ), typeof( Rect[] ),
        typeof( Quaternion[] ), typeof( Color[] ), typeof( Color32[] ), typeof( LayerMask[] ), typeof( Bounds[] ),
        typeof( Matrix4x4[] ), typeof( AnimationCurve[] ), typeof( Gradient[] ), typeof( RectOffset[] ),
        typeof( List<bool> ), typeof( List<byte> ), typeof( List<sbyte> ), typeof( List<char> ), 
        typeof( List<double> ), typeof( List<float> ), typeof( List<int> ), typeof( List<uint> ), typeof( List<long> ),
        typeof( List<ulong> ), typeof( List<short> ), typeof( List<ushort> ), typeof( List<string> ),
        typeof( List<Vector4> ), typeof( List<Vector3> ), typeof( List<Vector2> ), typeof( List<Rect> ),
        typeof( List<Quaternion> ), typeof( List<Color> ), typeof( List<Color32> ), typeof( List<LayerMask> ), typeof( List<Bounds> ),
        typeof( List<Matrix4x4> ), typeof( List<AnimationCurve> ), typeof( List<Gradient> ), typeof( List<RectOffset> ),
        typeof( Vector3Int ), typeof( Vector2Int ), typeof( RectInt ), typeof( BoundsInt ),
        typeof( Vector3Int[] ), typeof( Vector2Int[] ), typeof( RectInt[] ), typeof( BoundsInt[] ),
        typeof( List<Vector3Int> ), typeof( List<Vector2Int> ), typeof( List<RectInt> ), typeof( List<BoundsInt> )
    };


    public static bool IsIntrinsicType(string typeName)
    {
        if (intrinsicTypes.TryGetValue(typeName, out var qualifiedName))
            typeName = qualifiedName;

        Type type = Type.GetType(typeName);
        if (type?.Namespace != null && type.Namespace.StartsWith("System"))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a type name is valid.
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public static bool IsTypeNameValid(string typeName)
    {
        var valid = System.CodeDom.Compiler.CodeGenerator.IsValidLanguageIndependentIdentifier(typeName);
        return valid;
    }
    
    /// <summary>
    /// Checks if a namespace name is valid. An empty namespace is valid.
    /// </summary>
    /// <param name="namespaceName">The namespace name to validate.</param>
    /// <returns>True if the namespace name is valid; otherwise, false.</returns>
    internal static bool IsNamespaceValid(string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
            return true;
        var parts = namespaceName.Split('.');
        foreach (var part in parts)
        {
            if (!System.CodeDom.Compiler.CodeGenerator.IsValidLanguageIndependentIdentifier(part))
                return false; 
        }

        if (namespaceName.StartsWith(".") || namespaceName.EndsWith(".") || namespaceName.Contains(".."))
            return false;

        return true;
    }
    
    internal static bool IsUnityType(Type type) => unityTypes.Contains(type);
    
    internal static bool IsSerializable(Type type)
    {
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return true;

        if (type.IsArray)
        {
            //dont support multi-dimensional arrays
            if (type.GetArrayRank() != 1)
                return false;

            type = type.GetElementType();
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return true;
        }
        else if (type.IsGenericType)
        {
            // Generic types are allowed on 2020.1 and later
#if UNITY_2020_1_OR_NEWER
				if(type.GetGenericTypeDefinition() == typeof(List<>))
				{
					type = type.GetGenericArguments()[0];

					if(typeof(UnityEngine.Object).IsAssignableFrom(type))
						return true;
				}
#else
            if (type.GetGenericTypeDefinition() != typeof(List<>))
                return false;

            type = type.GetGenericArguments()[0];
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return true;
#endif
        }

#if !UNITY_2020_1_OR_NEWER
        if (type.IsGenericType)
            return false;
#endif

        return Attribute.IsDefined(type, typeof(SerializableAttribute), false);
    }
    
    // public static bool IsSerializableLazy(Type type)
    // {
    //     var isSerializable = false;
    //     isSerializable |= type.IsSerializable;
    //     isSerializable |= type.Namespace == "UnityEngine";
    //     isSerializable |= type.IsSubclassOf(typeof(MonoBehaviour));
    //     return isSerializable;
    // }
}