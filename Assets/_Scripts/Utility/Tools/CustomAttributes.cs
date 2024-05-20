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
	/// <summary>
	/// A class-only attribute that adds the name of a minigame as metadata.
	/// <example>
	/// [MinigameName("FunGame")]
	/// public class FunGame: Minigame
	/// {
	///    /* ... */
	/// }
	/// </summary>
	[AttributeUsage(System.AttributeTargets.Class)]
	class MinigameNameAttribute : System.Attribute
	{
		public String Name { get; set; }
		
		public MinigameNameAttribute(String name)
		{ 
			Name = name;
		}
	}

	/// <summary>
	/// A Unity post-processor that will run every time the asset library is refreshed
	/// and look for every instance of the MinigameName attribute.
	/// </summary>
	class MetedataWriter: AssetPostprocessor
	{
		/// <summary>
		/// An implementetion of the <c cref="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AssetPostprocessor">AssetPostprocessor</c>'s
		/// <c cref="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AssetPostprocessor.OnPostprocessAllAssets.html">OnPostprocessAllAssets</c> member.
		/// Looks for every class that extends <c cref="CosmicShore.Game.Arcade.MiniGame"/> (but not <c>Minigame</c> itself) and
		/// collects all of the <c>name</c> arguments added to them through instances of the <c cref="MinigameNameAttribute" /> attribute.
		///
		/// None of the arguments of the method are used in this implementation.
		/// </summary>
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
