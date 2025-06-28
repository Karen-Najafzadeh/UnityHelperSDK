using UnityEngine;

namespace UnityHelperSDK.DesignPatterns
{
    /// <summary>
    /// A generic interface for defining strategies
    /// </summary>
    public interface IStrategy<TContext, TResult>
    {
        TResult Execute(TContext context);
    }

    /// <summary>
    /// A generic context class that uses a strategy
    /// </summary>
    public class StrategyContext<TContext, TResult>
    {
        private IStrategy<TContext, TResult> _strategy;

        public StrategyContext(IStrategy<TContext, TResult> strategy)
        {
            _strategy = strategy;
        }

        public void SetStrategy(IStrategy<TContext, TResult> strategy)
        {
            _strategy = strategy;
        }

        public TResult ExecuteStrategy(TContext context)
        {
            if (_strategy == null)
            {
                Debug.LogError("No strategy set");
                return default;
            }
            return _strategy.Execute(context);
        }
    }

    /// <summary>
    /// Example usage:
    /// 
    /// public class MovementStrategy : IStrategy<Transform, Vector3>
    /// {
    ///     public Vector3 Execute(Transform context)
    ///     {
    ///         // Implement movement logic
    ///         return context.position;
    ///     }
    /// }
    /// 
    /// var context = new StrategyContext<Transform, Vector3>(new MovementStrategy());
    /// Vector3 result = context.ExecuteStrategy(transform);
    /// </summary>
}
