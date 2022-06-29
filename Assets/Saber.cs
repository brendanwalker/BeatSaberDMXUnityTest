﻿using System.Collections;
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
    float xoffset = XAmplitude * Mathf.Sin(Mathf.Repeat(Time.time / XPeriod, 1.0f) * 2.0f * Mathf.PI);
    float yoffset = YAmplitude * Mathf.Sin(Mathf.Repeat(Time.time / YPeriod, 1.0f) * 2.0f * Mathf.PI);

    gameObject.transform.position = new Vector3(
      initialPosition.x + xoffset,
      initialPosition.y + yoffset,
      initialPosition.z
    );

    RefreshSaberLocations();
  }

  void RefreshSaberLocations()
  {
    saberBladeBottomPos = gameObject.transform.position;
    saberBladeTopPos = saberBladeBottomPos + gameObject.transform.forward * 2.0f;
  }
}
