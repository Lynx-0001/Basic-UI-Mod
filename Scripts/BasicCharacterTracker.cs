using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BasicUI
{
    public class BasicCharacterTracker
    {
        private static readonly BindingFlags ALL =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public string RawName { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsInScene { get; private set; } = true;

        public float PleasureLevel { get; private set; }
        public float PleasureCap { get; private set; } = 25f;
        public float PleasureDelta { get; private set; }

        public bool IsClimaxing { get; private set; }
        public bool IsCloseToClimax { get; private set; }
        public float ClimaxThreshold { get; private set; } = 100f;
        public int ClimaxCount { get; private set; }

        public float PenetratedValue { get; private set; }
        public bool IsPenetrated => PenetratedValue > 0.1f;

        private Component _npc;
        private Type _npcType;
        private Animator _animator;
        private object _climaxCtrl;
        private Type _climaxType;
        private bool _reflectionReady;
        private bool _wasClimaxing;

        private PropertyInfo _propPleasureLevel;
        private PropertyInfo _propPleasureCap;
        private PropertyInfo _propPleasureDelta;
        private PropertyInfo _propClimaxCtrl;

        private PropertyInfo _propClimaxing;
        private PropertyInfo _propCloseToClimax;
        private FieldInfo _fldClimaxThreshold;

        public BasicCharacterTracker(string rawName, Component npcController)
        {
            RawName = rawName;
            DisplayName = rawName.Contains(".") ? rawName.Substring(0, rawName.LastIndexOf('.')) : rawName;
            _npc = npcController;

            if (_npc != null)
            {
                if (_npc.GetType().Name != "NPCController")
                {
                    foreach (var comp in _npc.GetComponentsInChildren<Component>(true))
                    {
                        if (comp != null && comp.GetType().Name == "NPCController")
                        {
                            _npc = comp;
                            break;
                        }
                    }
                }

                _npcType = _npc.GetType();
                CacheReflection();
                CacheAnimator();
            }
        }

        private void CacheReflection()
        {
            try
            {
                _propPleasureLevel = _npcType.GetProperty("CurPleasureLevel", ALL);
                _propPleasureCap   = _npcType.GetProperty("CurPleasureCap", ALL);
                _propPleasureDelta = _npcType.GetProperty("CurPleasureDelta", ALL);
                _propClimaxCtrl    = _npcType.GetProperty("ClimaxController", ALL);

                if (_propClimaxCtrl != null)
                {
                    _climaxCtrl = _propClimaxCtrl.GetValue(_npc);
                    if (_climaxCtrl != null)
                    {
                        _climaxType = _climaxCtrl.GetType();
                        _propClimaxing     = _climaxType.GetProperty("Climaxing", ALL);
                        _propCloseToClimax = _climaxType.GetProperty("CloseToClimax", ALL);
                        _fldClimaxThreshold = _climaxType.GetField("climaxThreshold", ALL);
                    }
                }

                _reflectionReady = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[BasicUI] Reflection failed for " + RawName + ": " + e.Message);
                _reflectionReady = false;
            }
        }

        private void CacheAnimator()
        {
            try
            {
                if (_npc != null)
                    _animator = _npc.GetComponentInChildren<Animator>();
            }
            catch { }
        }

        public bool IsNpcDestroyed => _npc == null;

        public void MarkDespawned()
        {
            IsInScene = false;
        }

        public void Poll()
        {
            if (_npc == null)
            {
                if (IsInScene) MarkDespawned();
                return;
            }

            if (!_npc.gameObject.activeInHierarchy)
            {
                if (IsInScene) MarkDespawned();
                return;
            }

            if (!_reflectionReady || !IsInScene) return;

            PleasureLevel = ReadFloat(_propPleasureLevel, _npc);
            PleasureCap   = ReadFloat(_propPleasureCap, _npc);
            if (PleasureCap <= 0) PleasureCap = 25f;
            PleasureDelta = ReadFloat(_propPleasureDelta, _npc);

            if (_climaxCtrl != null)
            {
                if (_propClimaxCtrl != null)
                {
                    try
                    {
                        var cc = _propClimaxCtrl.GetValue(_npc);
                        if (cc != null && cc != _climaxCtrl)
                        {
                            _climaxCtrl = cc;
                            _climaxType = cc.GetType();
                            _propClimaxing     = _climaxType.GetProperty("Climaxing", ALL);
                            _propCloseToClimax = _climaxType.GetProperty("CloseToClimax", ALL);
                            _fldClimaxThreshold = _climaxType.GetField("climaxThreshold", ALL);
                        }
                    }
                    catch { }
                }

                IsClimaxing     = ReadBool(_propClimaxing, _climaxCtrl);
                IsCloseToClimax = ReadBool(_propCloseToClimax, _climaxCtrl);
                ClimaxThreshold = ReadFloat(_fldClimaxThreshold, _climaxCtrl, 100f);

                if (IsClimaxing && !_wasClimaxing)
                    ClimaxCount++;
                _wasClimaxing = IsClimaxing;
            }

            if (_animator != null)
            {
                try { PenetratedValue = _animator.GetFloat("Penetrated"); } catch { }
            }
        }

        private float ReadFloat(PropertyInfo prop, object target, float fallback = 0f)
        {
            if (prop == null || target == null) return fallback;
            try { return (float)prop.GetValue(target); } catch { return fallback; }
        }

        private float ReadFloat(FieldInfo field, object target, float fallback = 0f)
        {
            if (field == null || target == null) return fallback;
            try
            {
                var val = field.GetValue(target);
                if (val is float f) return f;
                if (val is double d) return (float)d;
                if (val is int i) return i;
                return fallback;
            }
            catch { return fallback; }
        }

        private bool ReadBool(PropertyInfo prop, object target, bool fallback = false)
        {
            if (prop == null || target == null) return fallback;
            try { return (bool)prop.GetValue(target); } catch { return fallback; }
        }
    }
}
