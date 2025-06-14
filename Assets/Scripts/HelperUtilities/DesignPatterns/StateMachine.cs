using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHelperSDK.DesignPatterns
{
    /// <summary>
    /// A generic state machine implementation that handles state transitions and updates
    /// </summary>
    public class StateMachine<TState, TContext> where TState : IState<TContext>
    {
        private Dictionary<Type, TState> _states = new Dictionary<Type, TState>();
        private TState _currentState;
        private TContext _context;
        
        public TState CurrentState => _currentState;

        public StateMachine(TContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Add a state to the state machine
        /// </summary>
        public void AddState<T>(T state) where T : TState
        {
            var type = typeof(T);
            if (_states.ContainsKey(type))
            {
                Debug.LogWarning($"State {type.Name} already exists in state machine");
                return;
            }
            
            _states[type] = state;
        }

        /// <summary>
        /// Change to a new state
        /// </summary>
        public void ChangeState<T>() where T : TState
        {
            var type = typeof(T);
            if (!_states.ContainsKey(type))
            {
                Debug.LogError($"State {type.Name} not found in state machine");
                return;
            }

            _currentState?.OnExit(_context);
            _currentState = _states[type];
            _currentState.OnEnter(_context);
        }

        /// <summary>
        /// Update the current state
        /// </summary>
        public void Update()
        {
            _currentState?.OnUpdate(_context);
        }
    }

    /// <summary>
    /// Interface for states to be used with the state machine
    /// </summary>
    public interface IState<TContext>
    {
        void OnEnter(TContext context);
        void OnUpdate(TContext context);
        void OnExit(TContext context);
    }

    /// <summary>
    /// Example usage:
    /// 
    /// public class GameState : IState<GameManager>
    /// {
    ///     public void OnEnter(GameManager context) { }
    ///     public void OnUpdate(GameManager context) { }
    ///     public void OnExit(GameManager context) { }
    /// }
    /// 
    /// var stateMachine = new StateMachine<IState<GameManager>, GameManager>(gameManager);
    /// stateMachine.AddState(new GameState());
    /// stateMachine.ChangeState<GameState>();
    /// </summary>
}
