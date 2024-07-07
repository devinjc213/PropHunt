using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace PropHunt;

public class Config : BasePluginConfig
{
    [JsonPropertyName("shuffle")] public bool Shuffle { get; set; } = true;
    [JsonPropertyName("maxHunters")] public int MaxHunters { get; set; } = 2;
    [JsonPropertyName("freezeTime")] public float FreezeTime { get; set; } = 30f;
}

public partial class PropHunt : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "PropHunt";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleDescription => "A Prop Hunt plugin for CS2";
    public override string ModuleAuthor => "boose - https://devsdev.dev";

    public Config Config { get; set; } = null!;
    public void OnConfigParsed(Config config) { Config = config; }

    public float _currentYaw = 0f;
    public const float RotationThreshold = 45f;

    public bool _isFreezePeriod = false;
    public Dictionary<int, Vector> _frozenPlayerPositions = new Dictionary<int, Vector>();
    public float _freezeDuration = 30f;
    public float _freezeEndTime;

    public readonly Dictionary<string, string> _props = new();
    
    public static Dictionary<CCSPlayerController, CDynamicProp> thirdPersons
      = new Dictionary<CCSPlayerController, CDynamicProp>();

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

        AddCommand("css_tp", "Third person", (player, commandInfo) => 
        {
            if (player == null) return;

            DefaultThirdPerson(player);
        });

        AddCommand("css_thirdperson", "Third person", (player, commandInfo) => 
        {
            if (player == null) return;

        });

        RegisterListener<Listeners.OnTick>(HandleOnTick);
        RegisterListener<Listeners.OnMapStart>(HandleOnMapStart);
        RegisterListener<Listeners.OnServerPrecacheResources>(HandleOnServerPrecache);

        RegisterEventHandler<EventRoundStart>(HandleRoundStart);
        RegisterEventHandler<EventPlayerTeam>(HandlePlayerTeam);
        RegisterEventHandler<EventPlayerDeath>(HandlePlayerDeath);

        Console.WriteLine("PropHunt loaded.");
    }

    private void ShufflePlayers()
    {
        var ts = Utilities.GetPlayers()
          .Where(player => player.TeamNum == (int)CsTeam.Terrorist)
          .ToList();

        var cts = Utilities.GetPlayers()
          .Where(player => player.TeamNum == (int)CsTeam.CounterTerrorist)
          .ToList();

        if (ts.Count < Config.MaxHunters || cts.Count < Config.MaxHunters)
        {
          return;
        }

        var random = new Random();
        var playersToSwap = ts.OrderBy(x => random.Next()).Take(Config.MaxHunters).ToList();

        foreach (var player in playersToSwap)
        {
            player.SwitchTeam(CsTeam.CounterTerrorist);
        }

        foreach (var player in cts)
        {
          player.SwitchTeam(CsTeam.Terrorist);
        }
    }

    private void PropMenu(CCSPlayerController player)
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
                      Utilities.SetStateChanged(
                          player.PlayerPawn.Value,
                          "CBaseModelEntity",
                          "m_nModelIndex"
                      );
                  });
              }
            );
        }

        MenuManager.OpenChatMenu(player, menu);
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


    private Dictionary<string, string> LoadMapModels(string mapName)
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
