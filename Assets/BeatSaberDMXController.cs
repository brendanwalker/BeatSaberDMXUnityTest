using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeatSaberDMXController : MonoBehaviour
{
    public static BeatSaberDMXController Instance = null;
    public SaberManager GameSaberManager;

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public bool GetLedInteractionSegment(
        GameObject overlappingGameObject,
        out Vector3 segmentStart,
        out Vector3 segmentEnd,
        out Color32 segmentColor)
    {
        segmentStart = Vector3.zero;
        segmentEnd = Vector3.zero;
        segmentColor = Color.white;

        if (GameSaberManager != null)
        {
            Saber saber = overlappingGameObject.GetComponent<Saber>();
            if (saber != null)
            {
                segmentColor =
                    (GameSaberManager.leftSaber == saber)
                    ? new Color32(255, 0, 0, 255)
                    : new Color32(0, 0, 255, 255);

                segmentStart = saber.saberBladeBottomPos;
                segmentEnd = saber.saberBladeTopPos;

                //Plugin.Log?.Warn($"{overlappingGameObject.name} start {segmentStart.x},{segmentStart.y},{segmentStart.z}");
                //Plugin.Log?.Warn($"{overlappingGameObject.name} end {segmentEnd.x},{segmentEnd.y},{segmentEnd.z}");

                return true;
            }
        }

        return false;
    }
}
