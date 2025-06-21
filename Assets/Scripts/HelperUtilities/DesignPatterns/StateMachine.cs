using System;
using System.Collections.Generic;
using UnityEngine;
using UnityHelperSDK.DesignPatterns;

namespace UnityHelperSDK.DesignPatterns
{
    /// <summary>
    /// Enum-based state machine with fluent state configuration.
    /// </summary>
    public class StateMachine<TContext, TStateEnum> where TStateEnum : Enum
    {
        private readonly Dictionary<TStateEnum, State> _states = new();
        private State _current;
        private readonly TContext _context;

        public TStateEnum CurrentState => _current != null ? _current.Key : default;

        public StateMachine(TContext context)
        {
            _context = context;
        }

        public StateBuilder DefineState(TStateEnum key)
        {
            if (_states.TryGetValue(key, out var existing))
            {
                Debug.LogWarning($"State '{key}' already defined");
                return new StateBuilder(existing);
            }

            var state = new State(key, _context, this);
            _states[key] = state;
            return new StateBuilder(state);
        }

        public void SetInitialState(TStateEnum key)
        {
            if (_states.TryGetValue(key, out var state))
                TransitionTo(state);
            else
                Debug.LogError($"State '{key}' not defined");
        }

        public void TransitionTo(TStateEnum key)
        {
            if (_states.TryGetValue(key, out var state))
                TransitionTo(state);
            else
                Debug.LogError($"State '{key}' not defined");
        }

        private void TransitionTo(State next)
        {
            _current?.OnExit();
            _current = next;
            _current.OnEnter();
        }

        public void Update()
        {
            _current?.OnUpdate();
        }

        /// <summary>
        /// Fluent builder for defining a state.
        /// </summary>
        public class StateBuilder
        {
            private readonly State _state;

            public StateBuilder(State state) => _state = state;

            public StateBuilder OnEnter(Action<TContext> action)
            {
                _state.EnterAction = action ?? (_ => { });
                return this;
            }

            public StateBuilder OnUpdate(Action<TContext> action)
            {
                _state.UpdateAction = action ?? (_ => { });
                return this;
            }

            public StateBuilder OnExit(Action<TContext> action)
            {
                _state.ExitAction = action ?? (_ => { });
                return this;
            }

            public StateMachine<TContext, TStateEnum> EndState()
            {
                return _state.Machine;
            }
        }

        public class State
        {
            public TStateEnum Key { get; }
            public Action<TContext> EnterAction = _ => { };
            public Action<TContext> UpdateAction = _ => { };
            public Action<TContext> ExitAction = _ => { };

            public StateMachine<TContext, TStateEnum> Machine { get; }
            private readonly TContext _context;

            public State(TStateEnum key, TContext context, StateMachine<TContext, TStateEnum> machine)
            {
                Key = key;
                _context = context;
                Machine = machine;
            }

            public void OnEnter() => EnterAction(_context);
            public void OnUpdate() => UpdateAction(_context);
            public void OnExit() => ExitAction(_context);
        }
    }
}


//Example usage
//public enum PlayerState { Idle, Run, Jump }

//var sm = new SimpleStateMachine<Player, PlayerState>(player);

//sm.DefineState(PlayerState.Idle)
//  .OnEnter(p => p.PlayIdle())
//  .OnUpdate(p => { if (p.StartRun) sm.TransitionTo(PlayerState.Run); })
//  .OnExit(p => p.StopIdle())
//  .EndState();

//sm.DefineState(PlayerState.Run)
//  .OnEnter(p => p.PlayRun())
//  .OnUpdate(p => { p.Move(); if (!p.IsMoving) sm.TransitionTo(PlayerState.Idle); })
//  .OnExit(p => p.StopRun())
//  .EndState();

//sm.SetInitialState(PlayerState.Idle);
