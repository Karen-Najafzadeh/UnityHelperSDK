using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace UnityHelperSDK.HelperUtilities
{
    /// <summary>
    /// NavigationHelper provides a set of utilities for working with Unity's NavMesh system.
    /// It includes pathfinding, movement control, spatial queries, and dynamic obstacle management.
    /// </summary>

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
                float progress = 1f - (navAgent.remainingDistance / GetPathLength(navAgent.path));
                onProgress?.Invoke(progress);
                await Task.Yield();
            }
        }

        #endregion

        #region Spatial Queries
        
        /// <summary>
        /// Find nearest point on NavMesh
        /// </summary>
        public static Vector3 GetNearestPoint(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out var hit, 100f, NavMesh.AllAreas))
                return hit.position;
            return position;
        }

        /// <summary>
        /// Check if a point is on the NavMesh
        /// </summary>
        public static bool IsOnNavMesh(Vector3 position, float tolerance = 0.1f)
        {
            if (NavMesh.SamplePosition(position, out var hit, tolerance, NavMesh.AllAreas))
                return true;
            return false;
        }

        /// <summary>
        /// Find all NavMesh areas within a radius
        /// </summary>
        public static NavMeshHit[] FindAreasInRadius(Vector3 center, float radius)
        {
            var hits = new List<NavMeshHit>();
            const int SampleCount = 8;
            
            for (int i = 0; i < SampleCount; i++)
            {
                float angle = i * (2 * Mathf.PI / SampleCount);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 point = center + direction * radius;
                
                if (NavMesh.SamplePosition(point, out var hit, radius, NavMesh.AllAreas))
                    hits.Add(hit);
            }
            
            return hits.ToArray();
        }

        #endregion

        #region Dynamic Obstacles

        /// <summary>
        /// Add a dynamic obstacle to the NavMesh
        /// </summary>
        public static NavMeshObstacle AddDynamicObstacle(GameObject obj, Vector3 size)
        {
            var obstacle = obj.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.size = size;
            return obstacle;
        }

        /// <summary>
        /// Enable/disable NavMesh carving for an obstacle
        /// </summary>
        public static void SetObstacleCarving(NavMeshObstacle obstacle, bool enabled)
        {
            if (obstacle != null)
            {
                obstacle.carving = enabled;
                if (enabled)
                    obstacle.carveOnlyStationary = false;
            }
        }    /// <summary>
        /// Update NavMesh to include new area
        /// </summary>
        public static void UpdateNavMeshArea(Bounds bounds)
        {
            var data = new NavMeshData();
            var sources = new List<NavMeshBuildSource>();
            var instance = NavMesh.AddNavMeshData(data);
            NavMeshBuilder.UpdateNavMeshData(
                data,
                NavMesh.GetSettingsByID(0),
                sources,
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
            if (path == null)
                return new Vector3[0];
            return path.corners;
        }

        /// <summary>
        /// Calculate total path length
        /// </summary>
        public static float GetPathLength(NavMeshPath path)
        {
            if (path == null || path.corners.Length < 2)
                return 0f;

            float length = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
                length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            return length;
        }

        /// <summary>
        /// Draw the current path in the scene view
        /// </summary>
        public static void VisualizePath(NavMeshPath path)
        {
            if (!_showDebugPath || path == null)
                return;

            for (int i = 0; i < path.corners.Length - 1; i++)
                Debug.DrawLine(path.corners[i], path.corners[i + 1], _pathColor, _pathUpdateInterval);
        }

        #endregion
    }
}