using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

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
    // Runner MonoBehaviour for coroutines
    private static CoroutineRunner _runner;
    
    // Active coroutines tracking
    private static readonly Dictionary<string, (IEnumerator Routine, Coroutine Handle)> _activeCoroutines 
        = new Dictionary<string, (IEnumerator, Coroutine)>();
    private static readonly Dictionary<string, CancellationTokenSource> _cancellationSources 
        = new Dictionary<string, CancellationTokenSource>();
    
    // Coroutine pools
    private static readonly Dictionary<string, Queue<IEnumerator>> _coroutinePools 
        = new Dictionary<string, Queue<IEnumerator>>();
    
    // Sequence tracking
    private static readonly Dictionary<string, CoroutineSequence> _sequences 
        = new Dictionary<string, CoroutineSequence>();
    
    #region Initialization
    
    /// <summary>
    /// Initialize the helper with automatic initialization on first use
    /// </summary>
    public static void Initialize()
    {
        if (_runner == null)
        {
            var go = new GameObject("CoroutineRunner");
            _runner = go.AddComponent<CoroutineRunner>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
    }
    
    private static void EnsureInitialized()
    {
        if (_runner == null)
            Initialize();
    }
    
    #endregion
    
    #region Coroutine Management
    
    /// <summary>
    /// Start a coroutine with ID and optional error handling
    /// </summary>
    public static Coroutine StartCoroutineWithId(
        string id, 
        IEnumerator routine,
        Action<Exception> onError = null)
    {
        EnsureInitialized();
        StopCoroutineWithId(id);
        
        var wrappedRoutine = WrapWithErrorHandler(routine, onError);
        var handle = _runner.StartCoroutine(wrappedRoutine);
        _activeCoroutines[id] = (wrappedRoutine, handle);
        return handle;
    }
    
    /// <summary>
    /// Stop a coroutine by ID and clean up resources
    /// </summary>
    public static void StopCoroutineWithId(string id)
    {
        if (_activeCoroutines.TryGetValue(id, out var routineInfo))
        {
            _runner.StopCoroutine(routineInfo.Handle);
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
        {
            StopCoroutineWithId(id);
        }
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
    /// Start a task as a coroutine with progress reporting
    /// </summary>
    public static Coroutine StartTaskAsCoroutine<TProgress>(
        string id, 
        Task task, 
        IProgress<TProgress> progress = null,
        Action onComplete = null,
        Action<Exception> onError = null)
    {
        return StartCoroutineWithId(id, TaskToCoroutine(task, progress, onComplete, onError));
    }
    
    /// <summary>
    /// Start a task as a coroutine with result and progress
    /// </summary>
    public static Coroutine StartTaskAsCoroutine<T, TProgress>(
        string id, 
        Task<T> task, 
        IProgress<TProgress> progress = null,
        Action<T> onComplete = null,
        Action<Exception> onError = null)
    {
        return StartCoroutineWithId(id, TaskToCoroutine(task, progress, onComplete, onError));
    }
    
    private static IEnumerator TaskToCoroutine<TProgress>(
        Task task, 
        IProgress<TProgress> progress,
        Action onComplete = null,
        Action<Exception> onError = null)
    {
        while (!task.IsCompleted)
        {
            if (task is Task<TProgress> progressTask && !progressTask.IsFaulted)
            {
                progress?.Report(((Task<TProgress>)task).Result);
            }
            yield return null;
        }
        
        if (task.IsFaulted && onError != null)
            onError(task.Exception.InnerException ?? task.Exception);
        else if (!task.IsCanceled && onComplete != null)
            onComplete();
    }
    
    private static IEnumerator TaskToCoroutine<T, TProgress>(
        Task<T> task, 
        IProgress<TProgress> progress,
        Action<T> onComplete = null,
        Action<Exception> onError = null)
    {
        while (!task.IsCompleted)
        {
            if (task is Task<TProgress> progressTask && !progressTask.IsFaulted)
            {
                progress?.Report(((Task<TProgress>)task).Result);
            }
            yield return null;
        }
        
        if (task.IsFaulted && onError != null)
            onError(task.Exception.InnerException ?? task.Exception);
        else if (!task.IsCanceled && onComplete != null)
            onComplete(task.Result);
    }
    
    #endregion
    
    #region Parallel Execution
    
    /// <summary>
    /// Execute multiple coroutines in parallel with completion tracking
    /// </summary>
    public static Coroutine ExecuteParallel(
        string id, 
        Action onAllComplete = null,
        params IEnumerator[] routines)
    {
        return StartCoroutineWithId(id, ParallelCoroutine(routines, onAllComplete));
    }
    
    private static IEnumerator ParallelCoroutine(
        IEnumerator[] routines,
        Action onAllComplete = null)
    {
        var running = new List<(IEnumerator Routine, object Current)>();
        foreach (var routine in routines)
        {
            if (routine.MoveNext())
                running.Add((routine, routine.Current));
        }
        
        while (running.Count > 0)
        {
            yield return null;
            
            for (int i = running.Count - 1; i >= 0; i--)
            {
                var (routine, current) = running[i];
                
                if (current is YieldInstruction || current is CustomYieldInstruction)
                {
                    yield return current;
                }
                
                if (!routine.MoveNext())
                    running.RemoveAt(i);
                else
                    running[i] = (routine, routine.Current);
            }
        }
        
        onAllComplete?.Invoke();
    }
    
    #endregion
    
    #region Sequence Management
    
    /// <summary>
    /// Create a new sequence with fluent API
    /// </summary>
    public static CoroutineSequence CreateSequence(string id)
    {
        var sequence = new CoroutineSequence();
        _sequences[id] = sequence;
        return sequence;
    }
    
    /// <summary>
    /// Get an existing sequence by ID
    /// </summary>
    public static CoroutineSequence GetSequence(string id)
    {
        return _sequences.TryGetValue(id, out var sequence) ? sequence : null;
    }
    
    /// <summary>
    /// Execute a sequence as a coroutine with error handling
    /// </summary>
    public static Coroutine ExecuteSequence(
        string id,
        Action onComplete = null,
        Action<Exception> onError = null)
    {
        if (!_sequences.TryGetValue(id, out var sequence))
            return null;
            
        return StartCoroutineWithId(
            id,
            sequence.Execute(onComplete),
            onError);
    }
    
    #endregion
    
    #region Timing Utilities
    
    /// <summary>
    /// Execute an action after a delay with realtime option
    /// </summary>
    public static Coroutine ExecuteAfterDelay(
        string id,
        float delay,
        Action action,
        bool useRealtime = false)
    {
        return StartCoroutineWithId(id, DelayedAction(delay, action, useRealtime));
    }
    
    /// <summary>
    /// Execute an action repeatedly with realtime option
    /// </summary>
    public static Coroutine ExecuteRepeating(
        string id,
        float interval,
        Action action,
        int repetitions = -1,
        bool useRealtime = false)
    {
        return StartCoroutineWithId(id, RepeatingAction(interval, action, repetitions, useRealtime));
    }
    
    private static IEnumerator DelayedAction(
        float delay,
        Action action,
        bool useRealtime = false)
    {
        if (useRealtime)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return new WaitForSeconds(delay);
            
        action();
    }
    
    private static IEnumerator RepeatingAction(
        float interval,
        Action action,
        int repetitions,
        bool useRealtime = false)
    {
        var wait = useRealtime 
            ? new WaitForSecondsRealtime(interval) 
            : new WaitForSeconds(interval);
            
        int count = 0;
        while (repetitions < 0 || count < repetitions)
        {
            yield return wait;
            action();
            count++;
        }
    }
    
    #endregion
    
    #region Coroutine Pooling
    
    /// <summary>
    /// Get a coroutine from pool or create new with validation
    /// </summary>
    public static IEnumerator GetPooledCoroutine(
        string poolId,
        Func<IEnumerator> factory)
    {
        if (string.IsNullOrEmpty(poolId))
            throw new ArgumentException("Pool ID cannot be null or empty", nameof(poolId));
            
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
            
        if (!_coroutinePools.TryGetValue(poolId, out var pool))
        {
            pool = new Queue<IEnumerator>();
            _coroutinePools[poolId] = pool;
        }
        
        return pool.Count > 0 ? pool.Dequeue() : factory();
    }
    
    /// <summary>
    /// Return a coroutine to its pool with validation
    /// </summary>
    public static void ReturnToPool(string poolId, IEnumerator routine)
    {
        if (string.IsNullOrEmpty(poolId))
            throw new ArgumentException("Pool ID cannot be null or empty", nameof(poolId));
            
        if (routine == null)
            throw new ArgumentNullException(nameof(routine));
            
        if (!_coroutinePools.TryGetValue(poolId, out var pool))
        {
            pool = new Queue<IEnumerator>();
            _coroutinePools[poolId] = pool;
        }
        
        pool.Enqueue(routine);
    }
    
    #endregion
    
    #region Error Handling
    
    private static IEnumerator WrapWithErrorHandler(
        IEnumerator routine,
        Action<Exception> onError)
    {
        bool hasError = false;
        Exception error = null;
        
        while (true)
        {
            try
            {
                if (!routine.MoveNext())
                    break;
                    
                yield return routine.Current;
            }
            catch (Exception ex)
            {
                hasError = true;
                error = ex;
                break;
            }
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
/// Class for building and executing sequences of coroutines
/// </summary>
public class CoroutineSequence
{
    private readonly List<IEnumerator> _steps = new List<IEnumerator>();
    private readonly Dictionary<string, object> _context = new Dictionary<string, object>();
    
    public CoroutineSequence Then(IEnumerator step)
    {
        _steps.Add(step);
        return this;
    }
    
    public CoroutineSequence Then(Action action)
    {
        return Then(ExecuteAction(action));
    }
    
    public CoroutineSequence ThenWait(float seconds, bool useRealtime = false)
    {
        return Then(WaitRoutine(seconds, useRealtime));
    }
    
    public CoroutineSequence ThenWaitUntil(Func<bool> condition)
    {
        return Then(WaitUntilRoutine(condition));
    }
    
    public void SetContextValue<T>(string key, T value)
    {
        _context[key] = value;
    }
    
    public T GetContextValue<T>(string key)
    {
        return _context.TryGetValue(key, out var value) ? (T)value : default;
    }
    
    internal IEnumerator Execute(Action onComplete = null)
    {
        foreach (var step in _steps)
        {
            yield return step;
        }
        
        onComplete?.Invoke();
    }
    
    private IEnumerator ExecuteAction(Action action)
    {
        action();
        yield return null;
    }
    
    private IEnumerator WaitRoutine(float seconds, bool useRealtime)
    {
        if (useRealtime)
            yield return new WaitForSecondsRealtime(seconds);
        else
            yield return new WaitForSeconds(seconds);
    }
    
    private IEnumerator WaitUntilRoutine(Func<bool> condition)
    {
        yield return new WaitUntil(condition);
    }
}

/// <summary>
/// MonoBehaviour for running coroutines with cleanup
/// </summary>
public class CoroutineRunner : MonoBehaviour
{
    private void OnDestroy()
    {
        CoroutineHelper.StopAllCoroutines();
    }
}
