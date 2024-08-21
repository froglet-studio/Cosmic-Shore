using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Utility;

public class Worm : MonoBehaviour
{
    public WormManager Manager { get; set; }
    public Teams Team { get; set; }

    [SerializeField] private BodySegmentFauna headPrefab;
    [SerializeField] private BodySegmentFauna middleSegmentPrefab;
    [SerializeField] private BodySegmentFauna tailPrefab;
    [SerializeField] public List<BodySegmentFauna> initialSegments;

    public Vector3 headSpacing;
    public Vector3 tailSpacing;
    public Vector3 middleSpacing;

    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float headTurnSpeed = 2f;
    [SerializeField] private float bodyTurnSpeed = 5f;

    [SerializeField] private float segmentDelay = 0.1f;
    [SerializeField] private float rotationDamping = 0.5f;

    private List<Quaternion> targetRotations;
    private List<float> rotationTimes;

    List<BodySegmentFauna> segments = new List<BodySegmentFauna>();
    public bool hasHead;
    public bool hasTail;

    private Vector3 targetPosition;
    public bool isInitialized;

    public void UpdateHeadStatus(bool hasHead)
    {
        this.hasHead = hasHead;
    }

    public void UpdateTailStatus(bool hasTail)
    {
        this.hasTail = hasTail;
    }

    private void Start()
    {
        if (!isInitialized) InitializeWorm();
        InitializeRotationArrays();
    }

    private void InitializeRotationArrays()
    {
        targetRotations = new List<Quaternion>(segments.Count);
        rotationTimes = new List<float>(segments.Count);

        for (int i = 0; i < segments.Count; i++)
        {
            targetRotations.Add(segments[i].transform.rotation);
            rotationTimes.Add(0f);
        }
    }


    private void Update()
    {
        if (hasHead && segments.Count > 0)
        {
            //MoveWorm();
        }
    }

    private void MoveWorm()
    {
        BodySegmentFauna head = segments[0];

        // Calculate direction to target
        Vector3 directionToTarget = (targetPosition - head.transform.position).normalized;

        // Smoothly rotate the head towards the target
        Quaternion targetHeadRotation = Quaternion.LookRotation(directionToTarget);
        head.transform.rotation = Quaternion.Slerp(head.transform.rotation, targetHeadRotation, headTurnSpeed * Time.deltaTime);

        // Move the head forward
        head.transform.position += head.transform.forward * movementSpeed * Time.deltaTime;

        // Update target rotations
        targetRotations[0] = head.transform.rotation;
        rotationTimes[0] = Time.time;

        // Rotate body segments with delay
        for (int i = 1; i < segments.Count; i++)
        {
            //UpdateSegmentRotation(i);
        }

    }

    private void UpdateSegmentRotation(int index)
    {
        float targetTime = rotationTimes[index - 1] - segmentDelay;

        if (Time.time >= targetTime)
        {
            // Calculate the desired rotation to look at the parent
            Quaternion targetRotation = Quaternion.LookRotation(
                segments[index - 1].transform.position - segments[index].transform.position,
                segments[index - 1].transform.up
            );

            // Apply damping to reduce rigidity
            targetRotation = Quaternion.Slerp(targetRotations[index], targetRotation, 1 - rotationDamping);

            // Smoothly rotate towards the target rotation
            segments[index].transform.rotation = Quaternion.Slerp(
                segments[index].transform.rotation,
                targetRotation,
                bodyTurnSpeed * Time.deltaTime
            );

            // Update target rotation and time for this segment
            targetRotations[index] = targetRotation;
            rotationTimes[index] = Time.time;
        }
    }


    public void InitializeWorm()
    {
        if (initialSegments.Count > 0) 
        {
            segments = new List<BodySegmentFauna>(initialSegments);
            hasHead = segments[0].IsHead;
            hasTail = segments[segments.Count - 1].IsTail;
        }
        isInitialized = true;
        UpdateSegments();
    }

    public void UpdateSegments()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].ParentWorm = this;
            if (i == 0) segments[0].transform.parent = transform;
            else
            {
                segments[i].transform.SetParent(segments[i - 1].transform, true);
                segments[i].transform.localScale = new Vector3(.95f,.95f,.95f);
            }
            segments[i].PreviousSegment = i > 0 ? segments[i - 1] : null;
            segments[i].NextSegment = i < segments.Count - 1 ? segments[i + 1] : null;
        }
    }

    public void SetTarget(Vector3 target)
    {
        targetPosition = target;
    }

    public void AddSegment()
    {
        if (hasHead)
            if (hasTail)
            {
                BodySegmentFauna newSegment = Instantiate(middleSegmentPrefab, transform);
                newSegment.transform.position = segments[0].transform.position - headSpacing;
                newSegment.transform.LookAt(segments[0].transform.position);
                if (segments.Count > 1)
                    StartCoroutine(LerpUtilities.LerpingCoroutine(segments[1].transform.position, segments[1].transform.position + middleSpacing, .5f, (x) => { segments[1].transform.position = x; }));
                segments.Insert(1, newSegment); // Insert after the head
            }
            else
            {
                BodySegmentFauna newSegment = Instantiate(tailPrefab, transform);
                newSegment.transform.position = segments[segments.Count - 1].transform.position + tailSpacing;
                newSegment.transform.LookAt(segments[segments.Count - 1].transform.position);
                segments.Add(newSegment);
                hasTail = true;
            }
        else
        {
            BodySegmentFauna newSegment = Instantiate(headPrefab, transform); 
            newSegment.transform.position = segments[0].transform.position + headSpacing;
            newSegment.transform.LookAt(segments[0].transform.position + (4 * (segments[0].transform.position - segments[1].transform.position)));
            segments.Insert(0, newSegment);
            hasHead = true;
        }
        UpdateSegments();
    }

    public void RemoveSegment(BodySegmentFauna segment)
    {
        int index = segments.IndexOf(segment);
        if (index != -1)
        {
            segments.RemoveAt(index);
            if (index == 0) hasHead = false;
            if (index == segments.Count) hasTail = false;
            UpdateSegments();
        }
    }

    public Worm SplitWorm(BodySegmentFauna splitSegment)
    {
        int splitIndex = segments.IndexOf(splitSegment);
        if (splitIndex <= 0 || splitIndex >= segments.Count - 1) return null;

        List<BodySegmentFauna> newWormSegments = segments.GetRange(splitIndex, segments.Count - splitIndex);
        segments.RemoveRange(splitIndex, segments.Count - splitIndex);

        hasTail = false;

        UpdateSegments();

        Worm newWorm = Manager.CreateWorm(newWormSegments);
        return newWorm;
    }
}