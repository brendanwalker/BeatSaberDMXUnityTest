
namespace BeatSaberDMX.Configuration
{
  internal class PluginConfig
  {
    public static PluginConfig Instance { get; set; }

    // Must be 'virtual' if you want BSIPA to detect a value change and save the config automatically.
    public float SaberPaintRadius = 0.05f;
    public float SaberPaintDecayRate = 2.0f;

    void Awake()
    {
      Instance = this;
    }
  }
}