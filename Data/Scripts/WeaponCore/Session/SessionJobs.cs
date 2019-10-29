﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using WeaponCore.Support;
using static WeaponCore.Support.TargetingDefinition.BlockTypes;
using Task = ParallelTasks.Task;

namespace WeaponCore
{
    public partial class Session
    {
        public void UpdateDbsInQueue()
        {
            DbsUpdating = true;
            if (DbTask.IsComplete && DbTask.valid && DbTask.Exceptions != null)
                TaskHasErrors(ref DbTask, "DbTask");

            DbTask = MyAPIGateway.Parallel.Start(ProcessDbs, ProcessDbsCallBack);
        }

        private void ProcessDbs()
        {
            for (int i = 0; i < DbsToUpdate.Count; i++) DbsToUpdate[i].Scan();
        }

        private void ProcessDbsCallBack()
        {
            DsUtil.Start("db");
            for (int d = 0; d < DbsToUpdate.Count; d++)
            {
                var db = DbsToUpdate[d];
                if (db.MyPlanetTmp != null)
                {
                    var gridBox = db.MyGrid.PositionComp.WorldAABB;
                    if (db.MyPlanetTmp.IntersectsWithGravityFast(ref gridBox)) db.MyPlanetInfo();
                    else if (db.MyPlanet != null) db.MyPlanetInfo(clear: true);
                }

                for (int i = 0; i < db.SubGridsTmp.Count; i++) db.SubGrids.Add(db.SubGridsTmp[i]);
                db.SubGridsTmp.Clear();

                for (int i = 0; i < db.SortedTargets.Count; i++) db.TargetInfoPool.Return(db.SortedTargets[i]);
                db.SortedTargets.Clear();
                db.Targets.Clear();

                var newEntCnt = db.NewEntities.Count;
                db.SortedTargets.Capacity = newEntCnt;
                for (int i = 0; i < newEntCnt; i++)
                {
                    var detectInfo = db.NewEntities[i];
                    var ent = detectInfo.Parent;

                    if (ent.Physics == null) continue;
                    var grid = ent as MyCubeGrid;
                    var targetInfo = db.TargetInfoPool.Get();
                    if (grid == null)
                        targetInfo.Init(ref detectInfo, false, 1, db.MyGrid, db, null);
                    else
                    {
                        GridAi targetAi;
                        GridTargetingAIs.TryGetValue(grid, out targetAi);
                        targetInfo.Init(ref detectInfo, true, GridToFatMap[grid].Count, db.MyGrid, db, targetAi);
                    }

                    db.SortedTargets.Add(targetInfo);
                    db.Targets[ent] = targetInfo;
                }
                db.NewEntities.Clear();
                db.SortedTargets.Sort(db.TargetCompare1);

                db.Threats.Clear();
                db.Threats.AddRange(db.TargetAisTmp);
                db.ThreatsTmp.Clear();

                db.TargetAis.Clear();
                db.TargetAis.AddRange(db.TargetAisTmp);
                db.TargetAisTmp.Clear();

                db.Obstructions.Clear();
                db.Obstructions.AddRange(db.ObstructionsTmp);
                db.ObstructionsTmp.Clear();

                db.StaticsInRange.Clear();
                if (db.PlanetSurfaceInRange) db.StaticsInRangeTmp.Add(db.MyPlanet);
                var staticCount = db.StaticsInRangeTmp.Count;
                db.StaticsInRange.AddRange(db.StaticsInRangeTmp);
                db.StaticEntitiesInRange = staticCount > 0;
                db.StaticsInRangeTmp.Clear();

                db.DbReady = db.SortedTargets.Count > 0 || db.Threats.Count > 0 || db.FirstRun;
                db.MyShield = db.MyShieldTmp;
                db.ShieldNear = db.ShieldNearTmp;
                db.BlockCount = db.MyGrid.BlocksCount;

                if (db.FirstRun)
                    db.UpdateBlockGroups();

                db.FirstRun = false;
                Interlocked.Exchange(ref db.DbUpdating, 0);
            }
            DbsToUpdate.Clear();
            DbsUpdating = false;
            DsUtil.Complete("db", true);
        }

        internal void CheckDirtyGrids()
        {
            if (!NewGrids.IsEmpty)
                AddGridToMap();

            if ((!GameLoaded || Tick20) && DirtyGrids.Count > 0)
            {
                if (GridTask.valid && GridTask.Exceptions != null)
                    TaskHasErrors(ref GridTask, "GridTask");
                if (!GameLoaded) UpdateGrids();
                else GridTask = MyAPIGateway.Parallel.StartBackground(UpdateGrids);
            }
        }

        private void UpdateGrids()
        {
            //Log.Line($"[UpdateGrids] DirtTmp:{DirtyGridsTmp.Count} - Dirt:{DirtyGrids.Count}");
            //DsUtil2.Start("UpdateGrids");

            DirtyGridsTmp.Clear();
            DirtyGridsTmp.AddRange(DirtyGrids);
            DirtyGrids.Clear();
            for (int i = 0; i < DirtyGridsTmp.Count; i++)
            {
                var grid = DirtyGridsTmp[i];
                MyConcurrentList<MyCubeBlock> allFat;
                var newTypeMap = BlockTypePool.Get();
                newTypeMap[Offense] = ConcurrentListPool.Get();
                newTypeMap[Utility] = ConcurrentListPool.Get();
                newTypeMap[Thrust] = ConcurrentListPool.Get();
                newTypeMap[Steering] = ConcurrentListPool.Get();
                newTypeMap[Jumping] = ConcurrentListPool.Get();
                newTypeMap[Power] = ConcurrentListPool.Get();
                newTypeMap[Production] = ConcurrentListPool.Get();

                ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>> noFatTypeMap;
                if (GridToFatMap.TryGetValue(grid, out allFat))
                {
                    for (int j = 0; j < allFat.Count; j++)
                    {
                        var fat = allFat[j];
                        if (fat == null) continue;

                        using (fat.Pin())
                        {
                            if (fat.MarkedForClose) continue;
                            if (fat is IMyProductionBlock) newTypeMap[Production].Add(fat);
                            else if (fat is IMyPowerProducer) newTypeMap[Power].Add(fat);
                            else if (fat is IMyGunBaseUser || fat is IMyWarhead || fat is MyConveyorSorter && WeaponPlatforms.ContainsKey(fat.BlockDefinition.Id.SubtypeId)) newTypeMap[Offense].Add(fat);
                            else if (fat is IMyUpgradeModule || fat is IMyRadioAntenna) newTypeMap[Utility].Add(fat);
                            else if (fat is MyThrust) newTypeMap[Thrust].Add(fat);
                            else if (fat is MyGyro) newTypeMap[Steering].Add(fat);
                            else if (fat is MyJumpDrive) newTypeMap[Jumping].Add(fat);
                        }
                    }

                    ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>> oldTypeMap; 
                    if (GridToBlockTypeMap.TryGetValue(grid, out oldTypeMap))
                    {
                        GridToBlockTypeMap[grid] = newTypeMap;
                        foreach (var item in oldTypeMap)
                        {
                            item.Value.Clear();
                            ConcurrentListPool.Return(item.Value);
                        }
                        BlockTypePool.Return(oldTypeMap);
                    }
                    else GridToBlockTypeMap[grid] = newTypeMap;
                }
                else if (GridToBlockTypeMap.TryRemove(grid, out noFatTypeMap))
                {
                    foreach (var item in noFatTypeMap)
                    {
                        item.Value.Clear();
                        ConcurrentListPool.Return(item.Value);
                    }
                    BlockTypePool.Return(noFatTypeMap);
                }
            }
            DirtyGridsTmp.Clear();
            //DsUtil2.Complete("UpdateGrids", false, true);
        }
    }
}