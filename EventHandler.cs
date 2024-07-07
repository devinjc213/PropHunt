using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace PropHunt;

public partial class PropHunt
{
    public void HandleOnServerPrecache(ResourceManifest manifest)
    {
        foreach (var prop in _props)
        {
            manifest.AddResource(prop.Value.Replace('/', Path.DirectorySeparatorChar));
        }

        string charModel = "characters/models/tm_leet/tm_leet_varianta.vmdl";
        manifest.AddResource(charModel.Replace('/', Path.DirectorySeparatorChar));
    }

    public void HandleOnMapStart(string mapName)
    {
        _props.Clear();
        foreach (var model in LoadMapModels(mapName))
        {
            _props.Add(model.Key, model.Value);
        }
    }

    private void HandleOnTick()
    {
        foreach (var player in thirdPersons.Keys)
        {
            thirdPersons[player].UpdateCamera(player);
        }

        if (!_isFreezePeriod) return;

        if (Server.CurrentTime >= _freezeEndTime)
        {
            EndFreeze();
            return;
        }

        Utilities.GetPlayers().ForEach(player =>
        {
            if (!player.Validity()) return;

            if
            (
                player.TeamNum == (int)CsTeam.CounterTerrorist
                && _frozenPlayerPositions.ContainsKey(player.Slot)
            )
            {
                var pawn = player.PlayerPawn.Value;
                QAngle downwardAngle = new QAngle(89, pawn!.EyeAngles.Y, 0);
                pawn.Teleport(_frozenPlayerPositions[player.Slot], downwardAngle, new Vector(0, 0, 0));
            }
        });

        // Update countdown
        float remainingSeconds = _freezeEndTime - Server.CurrentTime;
        if (remainingSeconds % 1 < 0.1) // Update roughly every second
        {
            int roundedSeconds = (int)Math.Ceiling(remainingSeconds);
            Utilities.GetPlayers().ForEach(player =>
            {
                player.PrintToCenter($"Prop Hunt starts in {roundedSeconds} seconds!");
            });
        }
    }

    private HookResult HandlePlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        var playerPawn = player?.PlayerPawn.Get();

        if (player == null || !player.Validity()) return HookResult.Continue;

        // Client will crash if player dies as a non-player model.
        Server.NextFrame(() =>
        {
            if (playerPawn!.IsValid)
            {
                playerPawn.SetModel("characters/models/tm_leet/tm_leet_varianta.vmdl");
                Utilities.SetStateChanged(playerPawn, "CBaseModelEntity", "m_nModelIndex");
            }
        });

        return HookResult.Continue;
    }

    private HookResult HandleRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        Server.NextFrame(() => { ShufflePlayers(); });

        return HookResult.Continue;
    }

    private HookResult HandlePlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        var playerPawn = player?.Pawn.Get();

        if (player == null || !player.Validity()) return HookResult.Continue;

        var isHunter = player.TeamNum == (int)CsTeam.CounterTerrorist;

        var playerEntities = Utilities.GetPlayers();

        int hunterCount = 0;
        foreach (var p in playerEntities)
        {
            if (p.TeamNum == (int)CsTeam.CounterTerrorist) hunterCount++;
        }

        if (isHunter && hunterCount >= Config.MaxHunters)
        {
            player.ChangeTeam(CsTeam.Terrorist);
        }

        return HookResult.Continue;
    }

    private HookResult HandleRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        thirdPersons.Clear();
        _isFreezePeriod = true;
        _frozenPlayerPositions.Clear();
        _freezeEndTime = Server.CurrentTime + _freezeDuration;

        var bombEntities = Utilities.FindAllEntitiesByDesignerName<CC4>("weapon_c4");

        if (bombEntities.Any())
        {
            var bomb = bombEntities.FirstOrDefault();
            bomb!.Remove();
        }

        Utilities.GetPlayers().ForEach(player =>
        {
            if (!player.Validity()) return;

            var playerPawn = player.PlayerPawn.Value;

            if (player.TeamNum == (int)CsTeam.Terrorist)
            {
                player.RemoveWeapons();
            }
            else if (player.TeamNum == (int)CsTeam.CounterTerrorist)
            {

                player.RemoveWeapons();
                player.GiveNamedItem("weapon_knife");

                _frozenPlayerPositions[player.Slot] = playerPawn!.AbsOrigin!;
            }

            player.PrintToCenter($"Prop Hunt starts in {_freezeDuration} seconds!");
        });

        return HookResult.Continue;
    }
}
