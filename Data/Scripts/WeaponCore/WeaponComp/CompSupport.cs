﻿
using Sandbox.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent 
    {
        internal void TerminalRefresh(bool update = true)
        {
            Turret.RefreshCustomInfo();
            if (update && InControlPanel && InThisTerminal)
            {
                var mousePos = MyAPIGateway.Input.GetMousePosition();
                var startPos = new Vector2(800, 700);
                var endPos = new Vector2(1070, 750);
                var match1 = mousePos.Between(ref startPos, ref endPos);
                var match2 = mousePos.Y > 700 && mousePos.Y < 760 && mousePos.X > 810 && mousePos.X < 1070;
                if (!(match1 && match2)) MyCube.UpdateTerminal();
            }
        }

        private void SaveAndSendAll()
        {
            _firstSync = true;
            if (!_isServer) return;
            Set.SaveSettings();
            Set.NetworkUpdate();
            State.SaveState();
            State.NetworkUpdate();
        }

        internal void UpdatePivotPos(Weapon weapon)
        {
            if (!PivotLengthSet)
            {
                var blockUpDir = MyCube.PositionComp.WorldMatrix.Up;
                var testPos = MyCube.PositionComp.WorldAABB.Center - (blockUpDir * 20);
                var sphere = weapon.EntityPart.PositionComp.WorldVolume;
                var ray = new RayD(testPos, blockUpDir);
                var intersect = sphere.Intersects(ray);
                var dist = intersect ?? 0d;
                var pivotPos = (testPos + ray.Direction * (dist + sphere.Radius));
                MyPivotOffset = Vector3D.Distance(MyCube.PositionComp.WorldAABB.Center, pivotPos);
                PivotLengthSet = true;
            }

            MyPivotDir = weapon.EntityPart.PositionComp.WorldMatrix.Forward;
            MyPivotPos = MyCube.PositionComp.WorldAABB.Center + (MyCube.WorldMatrix.Up * MyPivotOffset);
            //var myPivotPos = MyCube.PositionComp.WorldAABB.Center;
            //myPivotPos += MyCube.PositionComp.WorldMatrix.Up * upPivotOffsetLen;
            // MyPivotPos = myPivotPos;
        }
    }
}