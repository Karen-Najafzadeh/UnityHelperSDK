using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace UnityHelperSDK.HelperUtilities{


    /// <summary>
    /// A comprehensive coroutine and task management helper that provides advanced control
    /// over parallel execution, pooling, timing, and cancellation.
    /// 
    /// Features:
    /// - Coroutine management and pooling
    /// - Task integration and synchronization
    /// - Parallel execution
    /// - Sequence management with fluent API
    /// - Timing utilities with realtime support
    /// - Cancellation support
    /// - Callback handling
    /// - Error handling and logging
    /// </summary>
    public static class CoroutineHelper
    {
        private static readonly Dictionary<string, IEnumerator> _activeCoroutines = new Dictionary<string, IEnumerator>();
        private static readonly Dictionary<string, CancellationTokenSource> _cancellationSources = new Dictionary<string, CancellationTokenSource>();
        private static CoroutineRunner _runner;

        #region Initialization

        private static void EnsureInitialized()
        {
            if (_runner == null)
            {
                var go = new GameObject("CoroutineRunner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<CoroutineRunner>();
            }
        }

        #endregion

        #region Coroutine Management

        /// <summary>
        /// Start a coroutine with error handling and cancellation support
        /// </summary>
        public static void StartManagedCoroutine(
            string id,
            IEnumerator routine,
            Action<Exception> onError = null)
        {
            EnsureInitialized();

            if (_activeCoroutines.ContainsKey(id))
                StopCoroutineWithId(id);

            var cts = new CancellationTokenSource();
            _cancellationSources[id] = cts;
            
            var wrappedRoutine = WrapWithErrorHandler(routine, onError);
            _activeCoroutines[id] = wrappedRoutine;
            _runner.StartCoroutine(wrappedRoutine);
        }

        /// <summary>
        /// Stop a coroutine by ID and clean up resources
        /// </summary>
        public static void StopCoroutineWithId(string id)
        {
            if (_activeCoroutines.TryGetValue(id, out var routine))
            {
                _runner.StopCoroutine(routine);
                _activeCoroutines.Remove(id);
            }

            if (_cancellationSources.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _cancellationSources.Remove(id);
            }
        }

        /// <summary>
        /// Stop all active coroutines and clean up
        /// </summary>
        public static void StopAllCoroutines()
        {
            foreach (var id in _activeCoroutines.Keys.ToList())
                StopCoroutineWithId(id);

            _activeCoroutines.Clear();
            foreach (var cts in _cancellationSources.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _cancellationSources.Clear();
        }

        #endregion

        #region Task Integration

        /// <summary>
        /// Convert a Task to a Coroutine
        /// </summary>
        public static IEnumerator ToCoroutine(Task task)
        {
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
                throw task.Exception;
        }

        /// <summary>
        /// Convert a Task<T> to a Coroutine with result
        /// </summary>
        public static IEnumerator ToCoroutine<T>(Task<T> task, Action<T> onComplete)
        {
            yield return ToCoroutine(task);
            
            if (!task.IsFaulted && !task.IsCanceled)
                onComplete?.Invoke(task.Result);
        }

        #endregion

        #region Sequence Management

        /// <summary>
        /// Run multiple coroutines in sequence
        /// </summary>
        public static IEnumerator Sequence(params IEnumerator[] coroutines)
        {
            foreach (var routine in coroutines)
                yield return _runner.StartCoroutine(routine);
        }

        /// <summary>
        /// Run multiple coroutines in parallel
        /// </summary>
        public static IEnumerator Parallel(params IEnumerator[] coroutines)
        {
            var routines = new List<Coroutine>();
            foreach (var routine in coroutines)
                routines.Add(_runner.StartCoroutine(routine));

            foreach (var routine in routines)
                yield return routine;
        }

        #endregion

        #region Error Handling

        private static IEnumerator WrapWithErrorHandler(
            IEnumerator routine,
            Action<Exception> onError)
        {
            bool hasError = false;
            Exception error = null;
            bool isComplete = false;
            while (!isComplete)
            {
                object current = null;
                try
                {
                    if (!(isComplete = !routine.MoveNext()))
                        current = routine.Current;
                }
                catch (Exception ex)
                {
                    hasError = true;
                    error = ex;
                    isComplete = true;
                }

                if (!isComplete)
                    yield return current;
            }
            
            if (hasError && onError != null)
            {
                try
                {
                    onError(error);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// MonoBehaviour that runs coroutines
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        private void OnDestroy()
        {
            CoroutineHelper.StopAllCoroutines();
        }
    }
}