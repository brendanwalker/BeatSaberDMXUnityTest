using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PluginConfig : MonoBehaviour
{
  public static PluginConfig Instance { get; set; }

  public float SaberPaintRadius = 0.05f;
  public float SaberPaintDecayRate = 2.0f;
  public string DMXSceneFilePath = "DMXSceneFile.json";

    // Start is called before the first frame update
    void Awake()
  {
    Instance = this;
  }
}
