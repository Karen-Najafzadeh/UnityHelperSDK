using System;
using UnityHelperSDK.TutorialUtilities;


    public class CompositeTrigger : ITrigger
    {
        private readonly ITrigger[] _triggers;
        private readonly CompositeTriggerLogic _logic;
        private int _firedCount;
        public event Action Fired;

        public CompositeTrigger(ITrigger[] triggers, CompositeTriggerLogic logic)
        {
            _triggers = triggers;
            _logic = logic;
        }

        public void Initialize()
        {
            foreach (var trig in _triggers)
            {
                trig.Fired += OnSubTriggerFired;
                trig.Initialize();
            }
        }

        public void TearDown()
        {
            foreach (var trig in _triggers)
            {
                trig.Fired -= OnSubTriggerFired;
                trig.TearDown();
            }
        }

        private void OnSubTriggerFired()
        {
            switch (_logic)
            {
                case CompositeTriggerLogic.And:
                    _firedCount++;
                    if (_firedCount >= _triggers.Length)
                        Fired?.Invoke();
                    break;
                case CompositeTriggerLogic.Or:
                    Fired?.Invoke();
                    break;
                case CompositeTriggerLogic.Not:
                    // Only fire if none of the subtriggers fire
                    break;
            }
        }
    }
