using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeatSaberDMXController : MonoBehaviour
{
    public static BeatSaberDMXController Instance = null;
    public SaberManager GameSaberManager;

    public List<Transform> ColorANotes = new List<Transform>();
    public List<Transform> ColorBNotes = new List<Transform>();
    public Color ColorA = Color.red;
    public Color ColorB = Color.blue;

    public GameObject ColorANotePrefab;
    public GameObject ColorBNotePrefab;
    public Transform NoteSpawnOrigin;
    public Transform NoteDespawnOrigin;
    public float NoteSpawnRadius = 1.0f;
    public float NoteMoveSpeed = 0.5f; // m/s
    public float NoteSpawnRate = 0.5f; // seconds

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;

        StartCoroutine(SpawnRandomNote());
    }

    void OnDestroy()
    {
        StopCoroutine(SpawnRandomNote());
    }

    // Update is called once per frame
    void Update()
    {
        UpdateNotes(ColorANotes);
        UpdateNotes(ColorBNotes);
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

    private IEnumerator SpawnRandomNote()
    {
        while (true)
        {
            Vector2 NoteOffset = Random.insideUnitCircle * NoteSpawnRadius;
            Quaternion StartOrientation = NoteSpawnOrigin.rotation;
            Vector3 StartPosition =
                NoteSpawnOrigin.position +
                NoteSpawnOrigin.right * NoteOffset.x +
                NoteSpawnOrigin.up * NoteOffset.y;

            if (Random.value < 0.5)
            {
                GameObject NoteGameObject = Instantiate(ColorANotePrefab, StartPosition, StartOrientation);
                ColorANotes.Add(NoteGameObject.transform);
            }
            else
            {
                GameObject NoteGameObject = Instantiate(ColorBNotePrefab, StartPosition, StartOrientation);
                ColorBNotes.Add(NoteGameObject.transform);
            }

            yield return new WaitForSeconds(NoteSpawnRate);
        }
    }

    private void UpdateNotes(List<Transform> Notes)
    {
        Vector3 despawnPlaneCenter = NoteDespawnOrigin.position;
        Vector3 despawnPlaneNormal = NoteDespawnOrigin.forward;

        // Despawn any notes past the despawn plane
        for (int Index = Notes.Count - 1; Index >= 0; Index--)
        {
            Transform noteTransform = Notes[Index];
            Vector3 noteOffset= noteTransform.position - despawnPlaneCenter;

            if (Vector3.Dot(noteOffset, despawnPlaneNormal) > 0.0f)
            {
                Object.Destroy(noteTransform.gameObject);
                Notes.RemoveAt(Index);
            }
        }

        // Update the rest of the notes
        foreach(Transform transform in Notes)
        {
            transform.position += transform.forward * NoteMoveSpeed * Time.deltaTime;
        }
    }
}
