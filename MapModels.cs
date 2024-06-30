namespace PropHunt;

public class MapModels
{
  public static readonly Dictionary<string, Dictionary<string, string>> maps = new()
  {
    {
      "de_mills", new Dictionary<string, string>
        {
          { "Soccer Ball", "models/props/de_dust/hr_dust/dust_soccerball/dust_soccer_ball001.vmdl" },
          { "Cardboard Trash", "models/props/ar_dizzy/dizzy_trash/dizzy_cardboard_trash_02.vmdl" },
          { "Dead Chicken", "models/props/cs_italy/dead_chicken.vmdl" },
          { "Snowman Head", "models/props/cs_office/snowman_head.vmdl" },
          { "Crate", "models/props/de_dust/hr_dust/dust_crates/dust_crate_style_02_72x36x72b_tarp.vmdl" },
          { "Trash Bag", "models/props/de_dust/hr_dust/dust_garbage_container/dust_trash_bag.vmdl" },
          { "Patio Umbrella (Closed)", "models/props/de_dust/hr_dust/dust_patio_set/dust_patio_umbrella_closed.vmdl" }
        }
    },
    {
      "de_inferno", new Dictionary<string, string>
      {
        { "Snowman Head", "models/props/cs_office/snowman_head.vmdl" },
      }
    },
    {
      "de_dust2", new Dictionary<string, string>
      {}
    },
    {
      "de_anubis", new Dictionary<string, string>
      {}
    },
    {
      "cs_italy", new Dictionary<string, string>
      {}
    },
    {
      "cs_office", new Dictionary<string, string>
      {}
    },
    {
      "default", new Dictionary<string, string>
      {
        { "Soccer Ball", "models/props/de_dust/hr_dust/dust_soccerball/dust_soccer_ball001.vmdl" },
      }
    }
  };
}
