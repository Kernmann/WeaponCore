﻿using Sandbox.ModAPI;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        private bool EntityAlive()
        {
            _tick = Session.Instance.Tick;
            if (MyGrid?.Physics == null) return false;
            if (!_firstSync && _readyToSync) SaveAndSendAll();
            if (!_isDedicated && _count == 29) TerminalRefresh();

            if (!_allInited && !PostInit()) return false;

            if (ClientUiUpdate || SettingsUpdated) UpdateSettings();
            return true;
        }

        private bool PostInit()
        {
            if (!_isServer && _clientNotReady) return false;
            Session.Instance.CreateLogicElements(Turret);
            WepUi.CreateUi(Turret);
            if (!Session.Instance.WepAction)
            {
                Session.Instance.WepAction = true;
                Session.AppendConditionToAction<IMyLargeTurretBase>((a) => Session.Instance.WepActions.Contains(a.Id), (a, b) => b.GameLogic.GetAs<WeaponComponent>() != null && Session.Instance.WepActions.Contains(a.Id));
            }
            if (_isServer && !IsFunctional) return false;

            if (_mpActive && _isServer) State.NetworkUpdate();

            _allInited = true;
            return true;
        }

        private void StorageSetup()
        {
            var isServer = MyAPIGateway.Multiplayer.IsServer;
            if (Set == null) Set = new LogicSettings(Turret);
            if (State == null) State = new LogicState(Turret);

            if (Turret.Storage == null) State.StorageInit();

            Set.LoadSettings();
            if (!State.LoadState() && !isServer) _clientNotReady = true;
            UpdateSettings(Set.Value);
            if (isServer)
            {
                State.Value.Overload = false;
                State.Value.Heat = 0;
            }
        }
    }
}