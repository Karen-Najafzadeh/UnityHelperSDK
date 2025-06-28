using System;
using UnityEngine;

namespace UnityHelperSDK.DesignPatterns
{
    /// <summary>
    /// Interface defining a state's behavior
    /// </summary>
    public interface IStatePattern<TContext>
    {
        void Enter(TContext context);
        void HandleInput(TContext context);
        void Update(TContext context);
        void Exit(TContext context);
    }

    /// <summary>
    /// Generic state pattern implementation that handles state behavior and transitions
    /// </summary>
    public abstract class StatePatternBase<TState, TContext> where TState : IStatePattern<TContext>
    {
        protected TState _currentState;
        protected TContext _context;

        public StatePatternBase(TContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Change to a new state
        /// </summary>
        public void ChangeState(TState newState)
        {
            if (_currentState != null && _currentState.Equals(newState))
            {
                Debug.LogWarning("Attempting to change to the same state");
                return;
            }

            try 
            {
                _currentState?.Exit(_context);
                _currentState = newState;
                _currentState?.Enter(_context);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error changing state: {e}");
            }
        }

        /// <summary>
        /// Update the current state
        /// </summary>
        public void Update()
        {
            _currentState?.HandleInput(_context);
            _currentState?.Update(_context);
        }

        /// <summary>
        /// Get the current state
        /// </summary>
        public TState GetCurrentState()
        {
            return _currentState;
        }
    }

    /// <summary>
    /// Example usage:
    /// 
    /// public class PlayerState : IStatePattern<Player>
    /// {
    ///     public virtual void Enter(Player context) { }
    ///     public virtual void HandleInput(Player context) { }
    ///     public virtual void Update(Player context) { }
    ///     public virtual void Exit(Player context) { }
    /// }
    /// 
    /// public class IdleState : PlayerState { }
    /// public class RunningState : PlayerState { }
    /// 
    /// public class PlayerStateManager : StatePatternBase<PlayerState, Player>
    /// {
    ///     public PlayerStateManager(Player player) : base(player) 
    ///     {
    ///         ChangeState(new IdleState());
    ///     }
    /// }
    /// 
    /// var stateManager = new PlayerStateManager(player);
    /// stateManager.ChangeState(new RunningState());
    /// </summary>
}
