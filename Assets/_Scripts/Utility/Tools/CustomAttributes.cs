using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Utility.Attributes
{
	[AttributeUsage(System.AttributeTargets.Class)]
	class MinigameNameAttribute : System.Attribute
	{
		private String Name;
		
		public MinigameNameAttribute(String name)
		{ 
			Name = name;
		}
	}
	
	class MetedataWriter: AssetPostprocessor
	{
		public void OnPostprocessAllAssets()
		{
			Debug.Log("Before or not");
		}
	}
}
