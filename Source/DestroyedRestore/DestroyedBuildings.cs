﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using RimWorld;
using Harmony;

namespace Replace_Stuff.DestroyedRestore
{
	[HarmonyPatch(typeof(ThingUtility), nameof(ThingUtility.CheckAutoRebuildOnDestroyed))]
	static class SaveDestroyedBuildings
	{
		//public static void CheckAutoRebuildOnDestroyed(Thing thing, DestroyMode mode, Map map, BuildableDef buildingDef)
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo PlaceBlueprintForBuildInfo = AccessTools.Method(typeof(GenConstruct), nameof(GenConstruct.PlaceBlueprintForBuild));

			foreach (CodeInstruction i in instructions)
			{
				yield return i;
				if (i.opcode == OpCodes.Call && i.operand == PlaceBlueprintForBuildInfo)
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);//Thing thing
					yield return new CodeInstruction(OpCodes.Ldarg_2);//Map map
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DestroyedBuildings), nameof(DestroyedBuildings.SaveBuilding)));//SaveBuilding(thing)
				}
			}
		}
	}

	public class DestroyedBuildings : MapComponent
	{
		public Dictionary<IntVec3, Thing> destroyedBuildings;
		//Actually want this to be deep-ref since it's despawned!

		public DestroyedBuildings(Map map) : base(map)
		{
			destroyedBuildings = new Dictionary<IntVec3, Thing>();
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref destroyedBuildings, "destroyedBuildings", LookMode.Value, LookMode.Deep);
			Log.Message($"Mode: {Scribe.mode}:{destroyedBuildings.ToStringSafeEnumerable()}");
		}
		

		public static void SaveBuilding(Thing thing, Map map)
		{
			if (thing is Frame) return;

			DestroyedBuildings comp = map.GetComponent<DestroyedBuildings>();
			Log.Message($"Saving {thing} to {map}:{thing.Position}");
			comp.destroyedBuildings[thing.Position] = thing;
			thing.ForceSetStateToUnspawned();
		}

		public static Thing FindBuilding(IntVec3 pos, Map map)
		{
			DestroyedBuildings comp = map.GetComponent<DestroyedBuildings>();
			if (comp.destroyedBuildings.TryGetValue(pos, out Thing building))
			{
				Log.Message($"got {building}");
				building.stackCount = 1;
				comp.destroyedBuildings.Remove(pos);

				return building;
			}
			return null;
		}

		public static void RemoveAt(IntVec3 pos, Map map)
		{
			DestroyedBuildings comp = map.GetComponent<DestroyedBuildings>();
			if (comp.destroyedBuildings.TryGetValue(pos, out Thing building))
			{
				Log.Message($"Removed destroyed: {building}");
				//Probably should set building.mapIndexOrState to -2
				comp.destroyedBuildings.Remove(pos);
			}
		}
	}
}