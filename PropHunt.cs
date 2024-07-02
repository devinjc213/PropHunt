using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Text.Json;

namespace PropHunt;

public class Config : BasePluginConfig
{
    [JsonPropertyName("shuffle")] public bool Shuffle { get; set; } = true;
    [JsonPropertyName("maxHunters")] public int MaxHunters { get; set; } = 2;
    [JsonPropertyName("freezeTime")] public float FreezeTime { get; set; } = 30f;
}

public class PropHunt : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "PropHunt";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleDescription => "A Prop Hunt plugin for CS2";
    public override string ModuleAuthor => "boose - https://devsdev.dev";

    public Config Config { get; set; } = null!;
    public void OnConfigParsed(Config config) { Config = config; }

    private float _currentYaw = 0f;
    private const float RotationThreshold = 45f;

    private bool _isFreezePeriod = false;
    private Dictionary<int, Vector> _frozenPlayerPositions = new Dictionary<int, Vector>();
    private float _freezeDuration = 30f;
    private float _freezeEndTime;

    private readonly Dictionary<string, string> _props = new();

    public override void Load(bool hotReload)
    {
        AddCommand("css_props", "Open the prop menu!", (player, commandInfo) =>
        {
            if (player == null) return;

            if (player.TeamNum == (int)CsTeam.Terrorist)
            {
                PropMenu(player);
            }
            else
            {
                player.PrintToChat("You must be a Terrorist to be a prop!");
            }
        });

        RegisterListener<Listeners.OnTick>(HandleOnTick);
        RegisterListener<Listeners.OnMapStart>(HandleOnMapStart);
        RegisterListener<Listeners.OnServerPrecacheResources>(HandleOnServerPrecache);

        RegisterEventHandler<EventRoundStart>(HandleRoundStart);
        RegisterEventHandler<EventPlayerTeam>(HandlePlayerTeam);
        RegisterEventHandler<EventPlayerDeath>(HandlePlayerDeath);

        Console.WriteLine("PropHunt loaded.");
    }

    private void HandleOnServerPrecache(ResourceManifest manifest)
    {
        foreach (var prop in _props)
        {
            manifest.AddResource(prop.Value.Replace('/', Path.DirectorySeparatorChar));
        }

        string charModel = "characters/models/tm_leet/tm_leet_varianta.vmdl";
        manifest.AddResource(charModel.Replace('/', Path.DirectorySeparatorChar));
    }

    private void HandleOnMapStart(string mapName)
    {
        _props.Clear();
        foreach (var model in LoadMapModels(mapName))
        {
            _props.Add(model.Key, model.Value);
        }
    }

    private HookResult HandlePlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        var playerPawn = player?.PlayerPawn.Get();

        if (player == null || playerPawn == null) return HookResult.Continue;

        // Client will crash if player dies as a non-player model.
        Server.NextFrame(() =>
        {
            if (playerPawn.IsValid)
            {
                playerPawn.SetModel("characters/models/tm_leet/tm_leet_varianta.vmdl");
            }
        });

        return HookResult.Continue;
    }

    public void PropMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Pick your prop!");

        foreach (var prop in _props)
        {
            menu.AddMenuOption($"{prop.Key}", (player, option) =>
              {
                  if (player.PlayerPawn.Value == null) return;

                  player.RemoveWeapons();
                  Server.NextFrame(() =>
                  {
                      player.PlayerPawn.Value.SetModel($"{prop.Value}");
                  });
              }
            );
        }

        MenuManager.OpenChatMenu(player, menu);
    }

    private HookResult HandlePlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        var playerPawn = player?.Pawn.Get();

        if (player == null
            || playerPawn == null
            || !player.IsValid
            || player.IsBot) return HookResult.Continue;

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

        if (player.TeamNum == (int)CsTeam.Terrorist)
        {
            AddTimer(
              0.1f,
              () => UpdatePropRotation(player),
              TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE
            );
        }

        return HookResult.Continue;
    }

    private HookResult HandleRoundStart(EventRoundStart @event, GameEventInfo info)
    {
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
            if (player.IsBot || !player.IsValid || !player.PawnIsAlive) return;

            var playerPawn = player.PlayerPawn.Value;

            if (playerPawn == null) return;

            if (player.TeamNum == (int)CsTeam.Terrorist)
            {
                player.RemoveWeapons();
            }
            else if (player.TeamNum == (int)CsTeam.CounterTerrorist)
            {

                player.RemoveWeapons();
                player.GiveNamedItem("weapon_knife");

                _frozenPlayerPositions[player.Slot] = playerPawn.AbsOrigin;
            }

            player.PrintToCenter($"Prop Hunt starts in {_freezeDuration} seconds!");
        });
        Server.NextFrame(() =>
        {
            Server.ExecuteCommand("sv_cheats 0");
        });

        return HookResult.Continue;
    }

    private void HandleOnTick()
    {
        if (!_isFreezePeriod) return;

        if (Server.CurrentTime >= _freezeEndTime)
        {
            EndFreeze();
            return;
        }

        Utilities.GetPlayers().ForEach(player =>
        {
            if (player.IsBot || !player.IsValid || !player.PawnIsAlive) return;

            if (player.TeamNum == (int)CsTeam.CounterTerrorist && _frozenPlayerPositions.ContainsKey(player.Slot))
            {
                var pawn = player.PlayerPawn.Value;
                if (pawn != null)
                {
                    QAngle downwardAngle = new QAngle(89, pawn.EyeAngles.Y, 0);
                    pawn.Teleport(_frozenPlayerPositions[player.Slot], downwardAngle, new Vector(0, 0, 0));
                }
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

    private void EndFreeze()
    {
        _isFreezePeriod = false;
        _frozenPlayerPositions.Clear();

        Utilities.GetPlayers().ForEach(player =>
        {
            if (player.IsBot || !player.IsValid || !player.PawnIsAlive) return;
            
            if (player.TeamNum == (int)CsTeam.CounterTerrorist)
            {
                player.PrintToCenter("Prop Hunt has begun!  Happy hunting! :)");
            }
            else if (player.TeamNum == (int)CsTeam.Terrorist)
            {
                player.PrintToCenter("Prop Hunt has begun!  Good luck! :)");
            }
        });
    }

    public void UpdatePropRotation(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive || player.TeamNum != (int)CsTeam.Terrorist) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        var eyeAngles = playerPawn.EyeAngles;
        var position = playerPawn.AbsOrigin;

        float normalizedEyeYaw = (eyeAngles.Y + 360) % 360;
        float normalizedCurrentYaw = (_currentYaw + 360) % 360;

        float yawDifference = normalizedEyeYaw - normalizedCurrentYaw;

        if (yawDifference > 180) yawDifference -= 360;
        if (yawDifference < -180) yawDifference += 360;

        if (Math.Abs(yawDifference) >= RotationThreshold)
        {
            int steps = (int)(yawDifference / RotationThreshold);
            _currentYaw += steps * RotationThreshold;

            _currentYaw = (_currentYaw + 360) % 360;

            var newAngles = new QAngle(0, _currentYaw, eyeAngles.Z);

            playerPawn.Teleport(position, newAngles, null);
        }
    }

    public Dictionary<string, string> LoadMapModels(string mapName)
    {
        string filePath = Path.Combine(ModuleDirectory, "models", $"{mapName}.json");
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        }

        filePath = Path.Combine(ModuleDirectory, "models", "default.json");
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        }

        return new Dictionary<string, string>();
    }
}
