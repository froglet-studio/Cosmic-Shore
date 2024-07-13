using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Core;

public class Worm : MonoBehaviour
{
    public WormManager Manager { get; set; }
    public List<BodySegmentFauna> Segments { get; private set; } = new List<BodySegmentFauna>();

    public Teams Team { get; set; }

    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float turnSpeed = 2f;
    [SerializeField] private float segmentSpacing = 1f;

    public bool hasHead;
    public bool hasTail;

    private Vector3 targetPosition;

    private void Start()
    {
        InitializeWorm();
    }

    private void Update()
    {
        MoveWorm();
    }

    private void InitializeWorm()
    {
        // Initial setup of the worm will be done by the WormManager
    }

    private void MoveWorm()
    {
        if (Segments.Count == 0) return;

        BodySegmentFauna head = Segments[0];

        // Move the head towards the target
        Vector3 direction = (targetPosition - head.transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        head.transform.rotation = Quaternion.Slerp(head.transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        head.transform.position += head.transform.forward * movementSpeed * Time.deltaTime;

        // Move the rest of the body
        for (int i = 1; i < Segments.Count; i++)
        {
            Vector3 previousSegmentPosition = Segments[i - 1].transform.position;
            Vector3 currentPosition = Segments[i].transform.position;

            Vector3 newPosition = Vector3.Lerp(currentPosition, previousSegmentPosition - previousSegmentPosition.normalized * segmentSpacing, movementSpeed * Time.deltaTime);

            Segments[i].transform.position = newPosition;
            Segments[i].transform.LookAt(previousSegmentPosition);
        }
    }

    public void SetTarget(Vector3 target)
    {
        targetPosition = target;
    }

    public void AddSegment(BodySegmentFauna newSegment, int index = -1)
    {
        if (index == -1 || index >= Segments.Count)
        {
            Segments.Add(newSegment);
            index = Segments.Count - 1;
        }
        else
        {
            Segments.Insert(index, newSegment);
        }

        newSegment.ParentWorm = this;

        if (index == 0)
        {
            hasHead = true;
            newSegment.IsHead = true;
            if (Segments.Count > 1)
            {
                Segments[1].IsHead = false;
                newSegment.NextSegment = Segments[1];
                Segments[1].PreviousSegment = newSegment;
            }
        }
        else if (index == Segments.Count - 1)
        {
            hasTail = true;
            newSegment.IsTail = true;
            if (Segments.Count > 1)
            {
                Segments[Segments.Count - 2].IsTail = false;
                newSegment.PreviousSegment = Segments[Segments.Count - 2];
                Segments[Segments.Count - 2].NextSegment = newSegment;
            }
        }
        else
        {
            newSegment.PreviousSegment = Segments[index - 1];
            newSegment.NextSegment = Segments[index + 1];
            Segments[index - 1].NextSegment = newSegment;
            Segments[index + 1].PreviousSegment = newSegment;
        }

        UpdateSegmentScales();
    }

    public void RemoveSegment(BodySegmentFauna segment)
    {
        int index = Segments.IndexOf(segment);
        if (index != -1)
        {
            Segments.RemoveAt(index);

            if (index > 0 && index < Segments.Count)
            {
                Segments[index - 1].NextSegment = index < Segments.Count ? Segments[index] : null;
                if (index < Segments.Count)
                    Segments[index].PreviousSegment = Segments[index - 1];
            }

            UpdateSegmentScales();
        }
    }

    public void SplitWorm(BodySegmentFauna splitSegment)
    {
        int splitIndex = Segments.IndexOf(splitSegment);
        if (splitIndex <= 0 || splitIndex >= Segments.Count - 1) return;

        List<BodySegmentFauna> newWormSegments = Segments.GetRange(splitIndex + 1, Segments.Count - splitIndex - 1);
        Segments.RemoveRange(splitIndex, Segments.Count - splitIndex);

        UpdateSegmentScales();

        Worm newWorm = Manager.CreateWorm(newWormSegments);
        newWorm.UpdateSegmentScales();
    }

    public void RegenerateSegment(BodySegmentFauna lostSegment)
    {
        if (lostSegment.IsHead)
        {
            BodySegmentFauna newHead = Manager.CreateSegment(transform.position, 1f);
            AddSegment(newHead, 0);
        }
        else if (lostSegment.IsTail)
        {
            BodySegmentFauna newTail = Manager.CreateSegment(Segments[Segments.Count - 1].transform.position, 0.6f);
            AddSegment(newTail);
        }
    }

    private void UpdateSegmentScales()
    {
        for (int i = 0; i < Segments.Count; i++)
        {
            if (i == 0) // Head
                Segments[i].SetScale(1f);
            else if (i >= Segments.Count - 2) // Last two segments (tail)
                Segments[i].SetScale(0.6f);
            else // Middle segments
            {
                float scale = Mathf.Max(0.6f, 1f - (i * 0.1f));
                Segments[i].SetScale(scale);
            }
        }
    }
}