using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Saber : MonoBehaviour
{
    public Vector3 saberBladeTopPos { get; private set; }
    public Vector3 saberBladeBottomPos { get; private set; }

    public float XAmplitude = 0.25f;
    public float XPeriod = 5;
    public float YAmplitude = 0.05f;
    public float YPeriod = 1;
    public float SpinPeriod = 1;
    public bool Animate = false;
    private Vector3 initialPosition;

    // Start is called before the first frame update
    void Start()
    {
        initialPosition = gameObject.transform.position;
        RefreshSaberLocations();
    }

    // Update is called once per frame
    void Update()
    {
        if (Animate)
        {
            float xoffset = XAmplitude * Mathf.Sin(Mathf.Repeat(Time.time / XPeriod, 1.0f) * 360.0f);
            float yoffset = YAmplitude * Mathf.Sin(Mathf.Repeat(Time.time / YPeriod, 1.0f) * 360.0f);

            gameObject.transform.position = new Vector3(
              initialPosition.x + xoffset,
              initialPosition.y + yoffset,
              initialPosition.z
            );
            gameObject.transform.rotation = Quaternion.Euler(0.0f, Mathf.Repeat(Time.time / SpinPeriod, 1.0f) * 360.0f, 0.0f);
        }

        RefreshSaberLocations();
    }

    void RefreshSaberLocations()
    {
        saberBladeBottomPos = gameObject.transform.position;
        saberBladeTopPos = saberBladeBottomPos + gameObject.transform.forward * 2.0f;
    }
}
