using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plugin : MonoBehaviour
{
    internal static UnityLogAdapter Log { get; private set; }

    void Start()
    {
        Plugin.Log = new UnityLogAdapter();

        DMXSceneDefinition sceneDef = DMXSceneDefinition.LoadSceneFile(PluginConfig.Instance.DMXSceneFilePath);
        DmxSceneInstance sceneInstance = new DmxSceneInstance();
        sceneInstance.Initialize(sceneDef, this.transform);
    }
}
