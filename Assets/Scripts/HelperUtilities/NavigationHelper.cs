using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A comprehensive navigation helper that handles pathfinding, movement,
/// and spatial queries using Unity's NavMesh system.
/// 
/// Features:
/// - Path finding and generation
/// - Smooth movement control
/// - Dynamic obstacle avoidance
/// - Path visualization
/// - NavMesh runtime updates
/// - Spatial queries
/// </summary>
public static class NavigationHelper
{
    // Navigation settings
    private static float _defaultStoppingDistance = 0.1f;
    private static float _defaultMovementSpeed = 5f;
    private static float _pathUpdateInterval = 0.5f;

    // Path visualization
    private static bool _showDebugPath = false;
    private static Color _pathColor = Color.yellow;
    private static float _pathWidth = 0.2f;

    #region Path Finding

    /// <summary>
    /// Find a path between two points
    /// </summary>
    public static bool FindPath(Vector3 start, Vector3 end, out NavMeshPath path)
    {
        path = new NavMeshPath();
        return NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path);
    }

    /// <summary>
    /// Move an agent along a path smoothly
    /// </summary>
    public static async Task MoveAlongPathAsync(GameObject agent, Vector3 destination, 
        float speed = -1, float stoppingDistance = -1, Action<float> onProgress = null)
    {
        var navAgent = agent.GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = agent.AddComponent<NavMeshAgent>();
        }

        if (speed > 0) navAgent.speed = speed;
        if (stoppingDistance > 0) navAgent.stoppingDistance = stoppingDistance;

        navAgent.SetDestination(destination);

        while (!navAgent.pathStatus.Equals(NavMeshPathStatus.PathComplete) || 
               navAgent.remainingDistance > navAgent.stoppingDistance)
        {
            float progress = 1f - (navAgent.remainingDistance / navAgent.path.length);
            onProgress?.Invoke(progress);
            await Task.Yield();
        }
    }

    #endregion

    #region Spatial Queries

    /// <summary>
    /// Find nearest point on NavMesh
    /// </summary>
    public static Vector3 GetNearestNavigablePoint(Vector3 point, float maxDistance = 100f)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, maxDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return point;
    }

    /// <summary>
    /// Check if point is on NavMesh
    /// </summary>
    public static bool IsPointNavigable(Vector3 point)
    {
        NavMeshHit hit;
        return NavMesh.SamplePosition(point, out hit, 0.1f, NavMesh.AllAreas);
    }

    /// <summary>
    /// Find random point in radius on NavMesh
    /// </summary>
    public static Vector3 GetRandomNavigablePoint(Vector3 center, float radius)
    {
        for (int i = 0; i < 30; i++)  // Try 30 times
        {
            Vector3 randomPoint = center + UnityEngine.Random.insideUnitSphere * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, radius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        return center;  // Fallback to center if no point found
    }

    #endregion

    #region Path Visualization

    /// <summary>
    /// Draw the current path in the scene view
    /// </summary>
    public static void VisualizePath(NavMeshPath path)
    {
        if (!_showDebugPath || path == null) return;

        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Debug.DrawLine(path.corners[i], path.corners[i + 1], _pathColor, _pathUpdateInterval);
        }
    }

    #endregion

    #region NavMesh Modification

    /// <summary>
    /// Add a new obstacle to the NavMesh at runtime
    /// </summary>
    public static NavMeshObstacle AddDynamicObstacle(GameObject obj, Vector3 size)
    {
        var obstacle = obj.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = obj.AddComponent<NavMeshObstacle>();
        }

        obstacle.size = size;
        obstacle.carving = true;
        return obstacle;
    }

    /// <summary>
    /// Update NavMesh to include new area
    /// </summary>
    public static void UpdateNavMeshArea(Bounds bounds)
    {
        NavMeshData data = NavMesh.AddNavMeshData(new NavMeshData());
        NavMeshBuilder.UpdateNavMeshData(
            data,
            NavMesh.GetSettingsByID(0),
            new NavMeshBuildSource[] { },
            bounds
        );
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get the corners of a path
    /// </summary>
    public static Vector3[] GetPathCorners(NavMeshPath path)
    {
        if (path == null) return new Vector3[0];
        return path.corners;
    }

    /// <summary>
    /// Calculate total path length
    /// </summary>
    public static float CalculatePathLength(NavMeshPath path)
    {
        if (path == null || path.corners.Length < 2) return 0f;

        float length = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        }
        return length;
    }

    #endregion
}
