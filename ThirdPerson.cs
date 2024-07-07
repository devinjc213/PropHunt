using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;

namespace PropHunt;

public partial class PropHunt
{
    public void DefaultThirdPerson(CCSPlayerController caller)
    {
        if (!thirdPersons.ContainsKey(caller))
        {
            CDynamicProp? _cameraProp = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");

            if (_cameraProp == null) return;

            _cameraProp.DispatchSpawn();
            _cameraProp.SetColor(Color.FromArgb(0, 255, 255, 255));
            _cameraProp.Teleport(caller.CalculatePositionInFront(-110, 90), caller.PlayerPawn.Value!.V_angle, new Vector());

            caller.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = _cameraProp.EntityHandle.Raw;
            Utilities.SetStateChanged(caller.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");
            thirdPersons.Add(caller, _cameraProp);

            AddTimer(0.5f, () =>
            {
                _cameraProp.Teleport(caller.CalculatePositionInFront(-110, 90), caller.PlayerPawn.Value.V_angle, new Vector());
            });
        }
        else
        {
            caller!.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue;
            AddTimer(0.3f, () => Utilities.SetStateChanged(caller.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices"));
            if (thirdPersons[caller] != null && thirdPersons[caller].IsValid) thirdPersons[caller].Remove();
            thirdPersons.Remove(caller);
        }
    }

    public void DebugPlayerInfo(CCSPlayerController player)
    {
        if (!player.Validity()) return;

        AddTimer(1.0f, () =>
        {
            var pawn = player.PlayerPawn.Value;
            var camera = thirdPersons.ContainsKey(player) ? thirdPersons[player] : null;

            Console.WriteLine($"--- Debug Info for Player {player.UserId} ---");
            Console.WriteLine($"Position: {pawn!.AbsOrigin}");
            Console.WriteLine($"Rotation: {pawn.AbsRotation}");
            Console.WriteLine($"EyeAngles: {pawn.EyeAngles}");
            Console.WriteLine($"Velocity: {pawn.AbsVelocity}");

            if (camera != null)
            {
                Console.WriteLine($"Camera Position: {camera.AbsOrigin}");
                Console.WriteLine($"Camera Angles: {camera.AbsRotation}");
            }

            Console.WriteLine("-----------------------------------");
        }, TimerFlags.REPEAT);
    }
}
