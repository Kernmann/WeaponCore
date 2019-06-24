﻿using System;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;

namespace WeaponCore.Support
{ 
    public partial class WeaponComponent
    {
        internal void RegisterEvents(bool register = true)
        {
            if (register)
            {
                Turret.AppendingCustomInfo += AppendingCustomInfo;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);
                BlockInventory.ContentsAdded += OnContentsAdded;
                BlockInventory.ContentsRemoved += OnContentsRemoved;
            }
            else
            {
                Turret.AppendingCustomInfo -= AppendingCustomInfo;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
                BlockInventory.ContentsAdded -= OnContentsAdded;
                BlockInventory.ContentsRemoved -= OnContentsRemoved;
            }
        }

        internal void OnContentsAdded(MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                var defId = item.Content.GetId();

                int weaponId;
                if (!Platform.Structure.AmmoToWeaponIds.TryGetValue(defId, out weaponId)) return;

                var weapon = Platform.Weapons[weaponId];

                Session.Instance.InventoryEvent.Enqueue(new InventoryChange(weapon, item, amount, InventoryChange.ChangeType.Add));
            }
            catch (Exception ex) { Log.Line($"Exception in OnContentsAdded: {ex}"); }
        }

        internal void OnContentsRemoved(MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                var defId = item.Content.GetId();

                int weaponId;
                if (!Platform.Structure.AmmoToWeaponIds.TryGetValue(defId, out weaponId)) return;
                var weapon = Platform.Weapons[weaponId];
                Session.ComputeStorage(weapon);
                //weapon.SuspendAmmoTick = 0;
                //weapon.UnSuspendAmmoTick = 0;
            }
            catch (Exception ex) { Log.Line($"Exception in OnContentsRemoved: {ex}"); }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsWorking = myCubeBlock.IsWorking;
            IsFunctional = myCubeBlock.IsFunctional;
            State.Value.Online = IsWorking && IsFunctional;
        }

        internal string GetShieldStatus()
        {
            if (!State.Value.Online && !MyCube.IsFunctional) return "[Controller Faulty]";
            if (!State.Value.Online && !MyCube.IsWorking) return "[Controller Offline]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetShieldStatus();
                if (status == "[Shield Up]" || status == "[Shield Down]" || status == "[Shield Offline]" || status == "[Insufficient Power]")
                {
                    stringBuilder.Append(status +
                                         "\n" +
                                         "\n[Shield Power]: " + SinkCurrentPower.ToString("0.0") + " Mw");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }
    }
}