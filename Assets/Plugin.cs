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

    private DMXSceneDefinition CreateSceneDefinition()
    {
        DMXSceneDefinition sceneDefinition = new DMXSceneDefinition();
        sceneDefinition.LanternDefinitions = new List<DmxLanternLayoutDefinition>();
        sceneDefinition.GridDefinitions = new List<DmxGridLayoutDefinition>();

        DmxLanternLayoutDefinition lanternDef = new DmxLanternLayoutDefinition();
        lanternDef.Name = "Lantern1";
        lanternDef.HorizontalPanelPixelCount = 16;
        lanternDef.VerticalPanelPixelCount = 32;
        lanternDef.PhysicalRadiusMeters = 0.051f;
        lanternDef.PhysicalHightMeters = 0.32f;
        lanternDef.PanelCount = 2;
        lanternDef.Transform = new DmxTransform()
        {
            XPosMeters = 1.0f,
            YPosMeters = 1.0f,
            ZPosMeters = 3.0f,
            YRotationAngle = 0.0f
        };
        lanternDef.Device = new DmxDeviceDefinition()
        {
            DeviceIP = "192.128.1.100",
            LedCount = 512,
            StartUniverse = 1
        };
        sceneDefinition.LanternDefinitions.Add(lanternDef);

        DmxGridLayoutDefinition gridDef = new DmxGridLayoutDefinition();
        gridDef.Name = "PixelGrid";
        gridDef.HorizontalPanelPixelCount = 48;
        gridDef.VerticalPanelPixelCount = 24;
        gridDef.Layout = DmxGridLayoutDefinition.ePixelGridLayout.HorizontalLinesZigZag;
        gridDef.PhysicalWidthMeters = 3.0f;
        gridDef.PhysicalHightMeters = 1.0f;
        gridDef.Transform = new DmxTransform()
        {
            XPosMeters = 0.0f,
            YPosMeters = 1.0f,
            ZPosMeters = 1.0f,
            YRotationAngle = 90.0f
        };
        gridDef.Devices = new List<DmxDeviceDefinition>();
        gridDef.Devices.Add(new DmxDeviceDefinition()
        {
            DeviceIP = "192.128.1.100",
            LedCount = 400,
            StartUniverse = 1
        });
        gridDef.Devices.Add(new DmxDeviceDefinition()
        {
            DeviceIP = "192.128.1.100",
            LedCount = 400,
            StartUniverse = 3
        });
        gridDef.Devices.Add(new DmxDeviceDefinition()
        {
            DeviceIP = "192.128.1.100",
            LedCount = 352,
            StartUniverse = 5
        });
        sceneDefinition.GridDefinitions.Add(gridDef);

        return sceneDefinition;
    }
}
