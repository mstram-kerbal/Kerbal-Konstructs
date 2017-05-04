﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KerbalKonstructs.Utilities;

namespace KerbalKonstructs.Core
{
	public class StaticDatabase
	{
		//Groups are stored by name within the body name
		private Dictionary<string, Dictionary<string, StaticGroup>> groupList = new Dictionary<string,Dictionary<string,StaticGroup>>();
        private Dictionary<string, StaticModel> modelList = new Dictionary<string, StaticModel>();
        private List<StaticModel> allStaticModels = new List<StaticModel>();

		private string activeBodyName = "";

		public void ChangeGroup(StaticObject obj, string newGroup)
		{
			String bodyName = ((CelestialBody)obj.getSetting("CelestialBody")).bodyName;
			String groupName = (string)obj.getSetting("Group");

			groupList[bodyName][groupName].RemoveStatic(obj);
			obj.setSetting("Group", newGroup);
			AddStatic(obj);
		}

		public void AddStatic(StaticObject obj)
		{
			String bodyName = ((CelestialBody) obj.getSetting("CelestialBody")).bodyName;
			String groupName = (string) obj.getSetting("Group");

			//Debug.Log("Creating object in group " + obj.groupName);

			if (!groupList.ContainsKey(bodyName))
				groupList.Add(bodyName, new Dictionary<string, StaticGroup>());

			if (!groupList[bodyName].ContainsKey(groupName))
			{
				//StaticGroup group = new StaticGroup(bodyName, groupName);
				StaticGroup group = new StaticGroup(groupName, bodyName);
				//Ungrouped objects get individually cached. New acts the same as Ungrouped but stores unsaved statics instead.
				if (groupName == "Ungrouped")
				{
					group.alwaysActive = true;
					//group.active = true;
				}
				
				group.active = true;
				
				groupList[bodyName].Add(groupName, group);
			}

			groupList[bodyName][groupName].AddStatic(obj);
		}

		public void ToggleActiveAllStatics(bool bActive = true)
		{
            Log.Debug("StaticDatabase.ToggleActiveAllStatics");

			foreach (StaticObject obj in KerbalKonstructs.instance.getStaticDB().GetAllStatics())
			{
				InstanceUtil.SetActiveRecursively(obj, bActive);
			}
		}

		public void ToggleActiveStaticsOnPlanet(CelestialBody cBody, bool bActive = true, bool bOpposite = false)
		{
            Log.Debug("StaticDatabase.ToggleActiveStaticsOnPlanet " + cBody.bodyName);

			foreach (StaticObject obj in KerbalKonstructs.instance.getStaticDB().GetAllStatics())
			{
				if ((CelestialBody)obj.getSetting("CelestialBody") == cBody)
					InstanceUtil.SetActiveRecursively(obj, bActive);
				else
					if (bOpposite)
						InstanceUtil.SetActiveRecursively(obj, !bActive);
			}
		}

		public void ToggleActiveStaticsInGroup(string sGroup, bool bActive = true, bool bOpposite = false)
		{
            Log.Debug("StaticDatabase.ToggleActiveStaticsInGroup");

			foreach (StaticObject obj in KerbalKonstructs.instance.getStaticDB().GetAllStatics())
			{
				if ((string)obj.getSetting("Group") == sGroup)
					InstanceUtil.SetActiveRecursively(obj, bActive);
				else
					if (bOpposite)
						InstanceUtil.SetActiveRecursively(obj, !bActive);
			}
		}

		public void CacheAll()
		{
			if (activeBodyName == "")
			{
                Log.Debug("StaticDatabase.cacheAll() skipped. No activeBodyName.");
				
				return;
			}

			if (groupList.ContainsKey(activeBodyName))
			{
                Log.Debug("StaticDatabase.cacheAll(): groupList containsKey " + activeBodyName);

				foreach (StaticGroup group in groupList[activeBodyName].Values)
				{
                    Log.Debug("StaticDatabase.cacheAll(): cacheAll() " + group.groupName);
					
					if (group.active)
						group.CacheAll();

					if (!group.alwaysActive)
					{
						group.active = false;
                        Log.Debug("StaticDatabase.cacheAll(): group is not always active. group.active is set false for " + group.groupName);
					}
				}
			}
			else
			{
                Log.Debug("StaticDatabase.cacheAll(): groupList DOES NOT containsKey " + activeBodyName);
			}
		}

		public void LoadObjectsForBody(String bodyName)
		{
			activeBodyName = bodyName;

			if (groupList.ContainsKey(bodyName))
			{
				foreach (KeyValuePair<String, StaticGroup> bodyGroups in groupList[bodyName])
				{
					bodyGroups.Value.active = true;
				}
			}
		}

		public void OnBodyChanged(CelestialBody body)
		{
			if (body != null)
			{
                Log.Debug("StaticDatabase.onBodyChanged(): body is not null.");

				if (body.bodyName != activeBodyName)
				{
                    Log.Debug("StaticDatabase.onBodyChanged(): bodyName is not activeBodyName. cacheAll(). Load objects for body. Set activeBodyName to body.");
                    Log.Debug("bodyName " + body.bodyName + " activeBodyName " + activeBodyName);

					CacheAll();
					LoadObjectsForBody(body.bodyName);
					activeBodyName = body.bodyName;
				}
			}
			else
			{
                Log.Debug("StaticDatabase.onBodyChanged(): body is null. cacheAll(). Set activeBodyName empty " + activeBodyName);
				CacheAll();
				activeBodyName = "";
			}
		}

		public void UpdateCache(Vector3 playerPos)
		{
            Log.Debug("StaticDatabase.updateCache(): activeBodyName is " + activeBodyName);

			Vector3 vPlayerPos = Vector3.zero;

			if (FlightGlobals.ActiveVessel != null)
			{
				vPlayerPos = FlightGlobals.ActiveVessel.GetTransform().position;
                Log.Debug("StaticDatabase.updateCache(): using active vessel " + FlightGlobals.ActiveVessel.vesselName);
			}
			else
				vPlayerPos = playerPos;

			if (vPlayerPos == Vector3.zero)
			{
                    Log.Warning("StaticDatabase.updateCache(): vPlayerPos is still v3.zero ");
			}
			
			if (groupList.ContainsKey(activeBodyName))
			{
				foreach (StaticGroup group in groupList[activeBodyName].Values)
				{
					if (!group.bLiveUpdate)
					{
                        Log.Debug("StaticDatabase.updateCache(): live update (updateCacheSettings) of group " + group.groupName);
						
						group.UpdateCacheSettings();
						group.bLiveUpdate = true;
					}

					if (!group.alwaysActive)
					{
						var center = group.centerPoint;
						var dist = Vector3.Distance(center, vPlayerPos);

						List<StaticObject> groupchildObjects = group.childObjects;

						foreach (StaticObject obj in groupchildObjects)
						{
							dist = Vector3.Distance(vPlayerPos, obj.gameObject.transform.position);
                            Log.Debug("StaticDatabase.updateCache(): distance to first group object is " + dist.ToString() + " for " + group.groupName);

							break;
						}

						if (center == Vector3.zero)
						{
                            Log.Debug("StaticDatabase.updateCache(): center of group is still v3.zero " + group.groupName);
						}
						
						//if (KerbalKonstructs.instance.DebugMode)
						//	Debug.Log("KK: StaticDatabase.updateCache(): dist is " + dist.ToString() + " to " + group.groupName);
						
						Boolean bGroupIsClose = dist < group.visibilityRange;
                        Log.Debug("StaticDatabase.updateCache(): group visrange is " + group.visibilityRange.ToString() + " for " + group.groupName);
						
						if (!bGroupIsClose)
						{
                            Log.Debug("StaticDatabase.updateCache(): Group is not close. cacheAll()  " + group.groupName);
							group.CacheAll();
						}
						
						group.active = bGroupIsClose;
					}
					else
					{
						Log.Debug("StaticDatabase.updateCache(): Group is always active. Check if updateCache goes off. " + group.groupName);
						group.active = true;
					}

					if (group.active)
					{
                        Log.Debug("StaticDatabase.updateCache(): Group is active. group.updateCache() " + group.groupName);
						group.UpdateCache(vPlayerPos);
					}
					else
					{
                        Log.Debug("StaticDatabase.updateCache(): Group is not active " + group.groupName);
					}
				}
			}

		}

		public void DeleteObject(StaticObject obj)
		{
			String bodyName = ((CelestialBody)obj.getSetting("CelestialBody")).bodyName;
			String groupName = (string)obj.getSetting("Group");

			if (groupList.ContainsKey(bodyName))
			{
				if (groupList[bodyName].ContainsKey(groupName))
				{
					Debug.Log("KK: StaticDatabase deleteObject");
					groupList[bodyName][groupName].DeleteObject(obj);
				}
			}
		}

		public List<StaticObject> GetAllStatics()
		{
			List<StaticObject> objects = new List<StaticObject>();
			foreach (Dictionary<string, StaticGroup> groups in groupList.Values)
			{
				foreach (StaticGroup group in groups.Values)
				{
					foreach (StaticObject obj in group.GetStatics())
					{
						objects.Add(obj);
					}
				}
			}
			return objects;
		}

		public void RegisterModel(StaticModel model, string name)
		{
            allStaticModels.Add(model);
            if (modelList.ContainsKey(name))
            {
                Log.UserInfo("duplicate model name: " + name + " ,found in: "  + model.configPath + " . This might be OK.");
                return;
            }
            else
            {
                modelList.Add(name, model);
            }
		}

		public List<StaticModel> GetModels()
		{
			return allStaticModels;
		}

        public StaticModel GetModel(string name)
        {
            if (!modelList.ContainsKey(name))
            {
                Log.UserError("No StaticModel found with name: " + name);
                return null;
            }
            else
            {
                return modelList[name];   
            }
        }

        public List<StaticObject> GetDirectInstancesFromModel(StaticModel model)
        {
            return (from obj in GetAllStatics() where obj.configPath == model.configPath select obj).ToList();
        }

        public List<StaticObject> GetObjectsFromModel(StaticModel model)
		{
			return (from obj in GetAllStatics() where obj.model == model select obj).ToList();
		}
	}
}
