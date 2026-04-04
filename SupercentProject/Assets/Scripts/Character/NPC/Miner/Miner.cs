using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Miner : Character
{
    private static readonly int MoveHash = Animator.StringToHash("bMove");
    private static readonly int PickingHash = Animator.StringToHash("tPicking");
    private const float ArrivalDistance = 0.05f;
    private const float FacingAngleThreshold = 1f;
    private const float DefaultRetargetDelay = 0.5f;
    private const float MiningTriggerInterval = 0.5f;
    private const float MiningConsumeDelay = 2f;
    private const int MiningTriggerCount = 2;

    [SerializeField]
    private List<Resource> targetOreList;
    [SerializeField]
    private float targetOreRayDistance = 20f;
    [SerializeField]
    private Vector3 targetOreRayOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField]
    private Vector3 targetVisitOffset = new Vector3(0f, 0f, 0.7f);
    [SerializeField]
    private float retargetDelay = DefaultRetargetDelay;

    [SerializeField]
    private PortableResource portableOre;
    [SerializeField]
    private PObj_Factory targetFactory;
    [SerializeField]
    private float portableOreTransferDuration = 0.5f;
    [SerializeField]
    private float portableOreJumpPower = 1f;
    [SerializeField]
    private int portableOreJumpCount = 1;

    [SerializeField, ReadOnly]
    private int currentTargetIndex = -1;
    [SerializeField, ReadOnly]
    private Resource currentTargetOre;
    [SerializeField, ReadOnly]
    private bool isMining;

    private Coroutine miningCoroutine;

    private void OnEnable()
    {
        ResolveFactory();
        StartMining();
    }

    private void OnDisable()
    {
        StopMining();
    }

    public void SetTargetOre()
    {
        if (targetOreList == null)
        {
            targetOreList = new List<Resource>();
        }
        else
        {
            targetOreList.Clear();
        }

        Vector3 origin = transform.position + targetOreRayOffset;
        RaycastHit[] hits = Physics.RaycastAll(origin, transform.forward, targetOreRayDistance);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        HashSet<Resource> uniqueResources = new HashSet<Resource>();

        for (int i = 0; i < hits.Length; ++i)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || !hitCollider.CompareTag("Ore"))
            {
                continue;
            }

            Resource targetOre = hitCollider.GetComponentInParent<Resource>();
            if (targetOre == null || !uniqueResources.Add(targetOre))
            {
                continue;
            }

            targetOreList.Add(targetOre);
        }

        Vector3 minerPosition = transform.position;
        targetOreList.Sort((left, right) =>
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            float leftDistance = (left.transform.position - minerPosition).sqrMagnitude;
            float rightDistance = (right.transform.position - minerPosition).sqrMagnitude;
            return leftDistance.CompareTo(rightDistance);
        });

        currentTargetIndex = -1;
        currentTargetOre = null;
    }

    public void StartMining()
    {
        if (isMining && miningCoroutine != null)
        {
            return;
        }

        StopMining();

        if (!isActiveAndEnabled)
        {
            return;
        }

        isMining = true;
        miningCoroutine = StartCoroutine(MiningRoutine());
    }

    public void StopMining()
    {
        if (miningCoroutine != null)
        {
            StopCoroutine(miningCoroutine);
            miningCoroutine = null;
        }

        isMining = false;
        currentTargetOre = null;
        currentTargetIndex = -1;
        SetMoveAnimation(false);
    }

    private IEnumerator MiningRoutine()
    {
        while (true)
        {
            Resource nextTarget = FindNextAliveOre();
            if (nextTarget == null)
            {
                isMining = false;
                SetMoveAnimation(false);
                yield return null;
                continue;
            }

            isMining = true;
            currentTargetOre = nextTarget;

            yield return MoveToOre(nextTarget);
            yield return FaceOre(nextTarget);
            yield return PlayMiningSequence(nextTarget);

            SetMoveAnimation(false);
            yield return new WaitForSeconds(retargetDelay);
        }
    }

    private Resource FindNextAliveOre()
    {
        if (targetOreList == null || targetOreList.Count == 0)
        {
            currentTargetOre = null;
            currentTargetIndex = -1;
            return null;
        }

        for (int step = 0; step < targetOreList.Count; ++step)
        {
            int candidateIndex = (currentTargetIndex + 1 + step) % targetOreList.Count;
            Resource candidate = targetOreList[candidateIndex];

            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            currentTargetIndex = candidateIndex;
            return candidate;
        }

        currentTargetOre = null;
        currentTargetIndex = -1;
        return null;
    }

    private IEnumerator MoveToOre(Resource targetOre)
    {
        SetMoveAnimation(true);

        while (targetOre != null && targetOre.IsAlive)
        {
            Vector3 destination = targetOre.transform.position + targetVisitOffset;
            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;

            if (toDestination.sqrMagnitude <= ArrivalDistance * ArrivalDistance)
            {
                transform.position = destination;
                SetMoveAnimation(false);
                break;
            }

            Vector3 moveDirection = toDestination.normalized;
            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                status.moveSpeed * Time.deltaTime);

            if (moveDirection.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    status.rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        SetMoveAnimation(false);
    }

    private IEnumerator FaceOre(Resource targetOre)
    {
        SetMoveAnimation(false);

        while (targetOre != null && targetOre.IsAlive)
        {
            Vector3 lookDirection = targetOre.transform.position - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                yield break;
            }

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);
            float angle = Quaternion.Angle(transform.rotation, targetRotation);

            if (angle <= FacingAngleThreshold)
            {
                transform.rotation = targetRotation;
                yield break;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                status.rotationSpeed * Time.deltaTime);

            yield return null;
        }
    }

    private IEnumerator PlayMiningSequence(Resource targetOre)
    {
        SetMoveAnimation(false);

        for (int i = 0; i < MiningTriggerCount; ++i)
        {
            if (Animator != null)
            {
                Animator.SetTrigger(PickingHash);
            }

            yield return new WaitForSeconds(MiningTriggerInterval);
        }

        float remainingConsumeDelay = Mathf.Max(0f, MiningConsumeDelay - (MiningTriggerInterval * MiningTriggerCount));
        if (remainingConsumeDelay > 0f)
        {
            yield return new WaitForSeconds(remainingConsumeDelay);
        }

        if (targetOre != null && targetOre.IsAlive)
        {
            targetOre.ConsumeResource();
            TryTransferPortableOreToFactory();
        }
    }

    private void ResolveFactory()
    {
        if (targetFactory == null)
        {
            targetFactory = FindObjectOfType<PObj_Factory>();
        }
    }

    private void TryTransferPortableOreToFactory()
    {
        ResolveFactory();

        if (portableOre == null || targetFactory == null)
        {
            return;
        }

        targetFactory.TryAddExternalInputResource(
            portableOre,
            portableOreTransferDuration,
            portableOreJumpPower,
            portableOreJumpCount);
    }

    private void SetMoveAnimation(bool isMoving)
    {
        if (Animator != null)
        {
            Animator.SetBool(MoveHash, isMoving);
        }
    }
}
