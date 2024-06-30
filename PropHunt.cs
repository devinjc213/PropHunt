using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;

namespace PropHunt;

public class Config : BasePluginConfig
{
  [JsonPropertyName("shuffle")] public bool Shuffle { get; set; } = true;
  [JsonPropertyName("maxHunters")] public int MaxHunters { get; set; } = 2;
}

public class PropHunt : BasePlugin, IPluginConfig<Config>
{
  public override string ModuleName => "PropHunt";
  public override string ModuleVersion => "0.0.1";
  public override string ModuleDescription => "A Prop Hunt plugin for CS2";
  public override string ModuleAuthor => "boose - https://devsdev.dev";

  public Config Config { get; set; } = null!;
  public void OnConfigParsed(Config config) { Config = config; }
  private string _mapName = Server.MapName;
  private float _currentYaw = 0f;
  private const float RotationThreshold = 45f;

  private readonly Dictionary<string, string> _props = new(MapModels.maps["de_mills"]);

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

    RegisterListener<Listeners.OnServerPrecacheResources>((manifest) => {
      foreach (var prop in _props)
      {
        manifest.AddResource(prop.Value.Replace('/', Path.DirectorySeparatorChar));
      }

      string charModel = "characters/models/tm_leet/tm_leet_varianta.vmdl";
      manifest.AddResource(charModel.Replace('/', Path.DirectorySeparatorChar));
    });

    RegisterEventHandler<EventRoundStart>((@event, info) =>
    {
        HandleRoundStart();

        return HookResult.Continue;
    });

    RegisterEventHandler<EventPlayerTeam>((@event, info) => 
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

        return HookResult.Continue;
    });

    RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
    {

      return HookResult.Continue;
    });

    RegisterEventHandler<EventPlayerDeath>((@event, info) => 
    {
      var player = @event.Userid;
      var playerPawn = player?.PlayerPawn.Get();

      if (player == null || playerPawn == null) return HookResult.Continue;

      // Client will crash if player dies as a non-player model.
      Server.NextFrame(() => {
          playerPawn.SetModel("characters/models/tm_leet/tm_leet_varianta.vmdl");
      });

      return HookResult.Continue;
    });

    RegisterEventHandler<EventPlayerConnectFull>((@event, info)=>
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        AddTimer(0.1f, () => UpdatePropRotation(player), TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    });

    Console.WriteLine("PropHunt loaded.");
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
          Server.NextFrame(() => {
              player.PlayerPawn.Value.SetModel($"{prop.Value}");
          });
        }
      );
    }

    MenuManager.OpenChatMenu(player, menu);
  }

  public void HandleRoundStart()
  {
    var bombEntities = Utilities.FindAllEntitiesByDesignerName<CC4>("weapon_c4");

    if (bombEntities.Any())
    {
      var bomb = bombEntities.FirstOrDefault();
      if (bomb != null) bomb.Remove();
    }

    Server.ExecuteCommand("sv_cheats 1");
    Utilities.GetPlayers().ForEach(player =>
    {
      if (player.IsBot || !player.IsValid || !player.PawnIsAlive) return;

      if (player.TeamNum == (int)CsTeam.Terrorist)
      {
        var thirdperson = ConVar.Find("thirdperson");
        if (thirdperson != null) thirdperson.SetValue(true);

        var thirdpersonMaya = ConVar.Find("thirdperson_mayamode");
        if (thirdpersonMaya != null) thirdpersonMaya.SetValue(true);

        player.RemoveWeapons();
      }
      else if (player.TeamNum == (int)CsTeam.CounterTerrorist)
      {
        player.RemoveWeapons();
        player.GiveNamedItem("weapon_knife");
      }
    });
    Server.ExecuteCommand("sv_cheats 0");
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
}
