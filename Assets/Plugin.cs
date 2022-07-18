using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plugin : MonoBehaviour
{
    internal static UnityLogAdapter Log { get; private set; }

    void Start()
    {
        Plugin.Log = new UnityLogAdapter();

        DmxSceneManager.Instance.TryUpdateDMXScenePath();
        DmxSceneManager.Instance.LoadDMXScene(this.transform);
    }
}
