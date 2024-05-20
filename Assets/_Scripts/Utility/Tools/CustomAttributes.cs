using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using CosmicShore.Game.Arcade;
using Mono.CSharp;
using System.Linq;
using QFSW.QC.Utilities;

namespace CosmicShore.Utility.Attributes
{
	[AttributeUsage(System.AttributeTargets.Class)]
	class MinigameNameAttribute : System.Attribute
	{
		public String Name { get; set; }
		
		public MinigameNameAttribute(String name)
		{ 
			Name = name;
		}
	}
	
	class MetedataWriter: AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
		{
			Type[] minigameTypes = typeof(MiniGame).Assembly.GetTypes();
			
			foreach (Type gameType in minigameTypes)
			{
				if (!gameType.IsSubclassOf(typeof(MiniGame)) || gameType == typeof(MiniGame))
				{
					continue;
				}
				// gameType is certain to derive from MiniGame.

				MinigameNameAttribute attr = gameType.GetTypeInfo().GetCustomAttribute<MinigameNameAttribute>(true);
				if (attr == null)
				{
					continue;
				}
				Debug.Log($"Name of minigame: {attr.Name}") ;
			}
		}
	}
}
