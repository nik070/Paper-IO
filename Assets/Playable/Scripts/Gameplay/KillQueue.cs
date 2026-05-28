using System.Collections.Generic;
using UnityEngine;
using Core;
using Mechanics;

public class KillQueue : MonoBehaviour
{
    [Header("Queue Settings")]
    [Tooltip("The main player that the first enemy in the queue will follow.")]
    public Transform playerTarget;

    [Tooltip("The distance each follower should keep from the one in front of it.")]
    public float followDistance = 1.5f;

    [Tooltip("How fast the followers move to catch up.")]
    public float followSpeed = 10f;

    [Tooltip("The new scale for the enemy once it joins the queue (e.g. 0.5 is half size).")]
    public float scaleDownMultiplier = 0.5f;

    [Tooltip("Should followers rotate to look at their target?")]
    public bool lookAtTarget = true;

    [Tooltip("An extra rotation offset to apply (useful if the model faces sideways natively).")]
    public Vector3 rotationOffset = Vector3.zero;

    [Tooltip("Continuous spin applied every frame (in degrees per second).")]
    public Vector3 spinSpeed = Vector3.zero;

    // List to keep track of all enemies in the queue
    private List<Transform> followers = new List<Transform>();

    [Header("Auto Find Settings")]
    [Tooltip("If true, it will automatically search for the player GameObject by name if playerTarget is not set.")]
    public bool autoFindPlayer = true;
    [Tooltip("The name of the player GameObject to find in the scene.")]
    public string playerNameToFind = "Player";

    /// <summary>
    /// Call this if the queue is managed externally and you need to assign the spawned player.
    /// </summary>
    public void SetPlayerTarget(Transform spawnedPlayer)
    {
        playerTarget = spawnedPlayer;
    }

    private void OnEnable()
    {
        GameEvents.OnCharacterDied += HandleCharacterDied;
    }

    private void OnDisable()
    {
        GameEvents.OnCharacterDied -= HandleCharacterDied;
    }

    private void HandleCharacterDied(DeathInfo info)
    {
        if (playerTarget == null) return;

        // Check if the player killed this enemy
        if (info.Killer != null && info.Killer.transform == playerTarget)
        {
            // The original enemy is going to be destroyed entirely.
            // To keep it as a follower, we instantiate a clone of its visual model.
            if (info.Victim.skinRoot != null)
            {
                GameObject pet = Instantiate(info.Victim.skinRoot, info.Victim.transform.position, info.Victim.transform.rotation);

                pet.SetActive(true);
                AddToQueue(pet.transform);
            }
        }
    }

    private void Update()
    {
        // Dynamically find the player if we don't have one assigned
        if (playerTarget == null && autoFindPlayer)
        {
            GameObject playerObj = GameObject.Find(playerNameToFind);
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
            }
        }

        if (followers.Count == 0 || playerTarget == null) return;

        for (int i = 0; i < followers.Count; i++)
        {
            Transform follower = followers[i];

            // Clean up list if a follower was destroyed
            if (follower == null)
            {
                followers.RemoveAt(i);
                i--;
                continue;
            }

            // The first follower follows the player; others follow the one in front of them in the list
            Transform targetToFollow = (i == 0) ? playerTarget : followers[i - 1];

            // Vector pointing from follower to target
            Vector3 directionToTarget = targetToFollow.position - follower.position;
            float distanceToTarget = directionToTarget.magnitude;

            // Only move if we are further than the desired follow distance
            if (distanceToTarget > followDistance)
            {
                // The position we want to reach (just behind the target)
                Vector3 desiredPosition = targetToFollow.position - (directionToTarget.normalized * followDistance);

                // Smoothly move towards the desired position
                follower.position = Vector3.Lerp(follower.position, desiredPosition, Time.deltaTime * followSpeed);
            }

            // Always calculate and apply rotation to ensure smoothness, even when not moving.
            // Flatten the direction (y = 0) to prevent the follower from flipping downwards on slopes or Y differences.
            Vector3 lookDirection = directionToTarget;
            lookDirection.y = 0;

            if (lookAtTarget && lookDirection.sqrMagnitude > 0.001f)
            {
                // Isolate the Y-axis rotation (yaw) to guarantee the model never pitches/tumbles when turning 180 degrees.
                float targetYaw = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;

                // Extract current yaw by removing the rotation offset
                Quaternion currentHeading = follower.rotation * Quaternion.Inverse(Quaternion.Euler(rotationOffset));
                Vector3 currentForward = currentHeading * Vector3.forward;
                float currentYaw = Mathf.Atan2(currentForward.x, currentForward.z) * Mathf.Rad2Deg;

                // Interpolate only the yaw angle smoothly
                float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * followSpeed);

                // Construct the final rotation: pure yaw + fixed offset
                follower.rotation = Quaternion.Euler(0, newYaw, 0) * Quaternion.Euler(rotationOffset);
            }
            else if (rotationOffset != Vector3.zero && spinSpeed == Vector3.zero)
            {
                follower.localRotation = Quaternion.Euler(rotationOffset);
            }

            // Apply continuous spinning if configured
            if (spinSpeed != Vector3.zero)
            {
                follower.Rotate(spinSpeed * Time.deltaTime, Space.Self);
            }
        }
    }

    /// <summary>
    /// Call this when an enemy dies to add them to the player's follow queue.
    /// </summary>
    public void AddToQueue(Transform enemyTransform)
    {
        if (enemyTransform != null && !followers.Contains(enemyTransform))
        {
            // Add to our list
            followers.Add(enemyTransform);

            // Scale them down
            enemyTransform.localScale *= scaleDownMultiplier;

            // Note: You will likely want to disable the enemy's AI, physics, or colliders here
            // so they don't keep trying to attack or block the player. 
            // For example:
            // var collider = enemyTransform.GetComponent<Collider>();
            // if (collider != null) collider.enabled = false;
        }
    }

    /// <summary>
    /// Removes an enemy from the queue (e.g., if you want to destroy them later).
    /// </summary>
    public void RemoveFromQueue(Transform enemyTransform)
    {
        if (followers.Contains(enemyTransform))
        {
            followers.Remove(enemyTransform);
        }
    }
}
