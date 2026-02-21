using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BasicUI
{
    public class BasicUIManager : MonoBehaviour
    {
        public static BasicUIManager Instance { get; private set; }

        private static readonly BindingFlags ALL =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public KeyCode hudToggleKey = KeyCode.F9;
        public KeyCode nextCharKey = KeyCode.RightBracket;
        public KeyCode prevCharKey = KeyCode.LeftBracket;

        private bool _hudVisible = true;
        private int _selectedCharIndex = 0;
        private float _sessionTimer = 0f;
        private bool _initialized = false;

        private List<BasicCharacterTracker> _trackers = new List<BasicCharacterTracker>();

        private static readonly string[] ALL_CHARACTER_NAMES = new string[]
        {
            "Hera.F", "Hera.T", "Garret.C", "Garret.H",
            "Marina.F", "Marina.T", "Renee.F", "Renee.T",
            "Riley", "Avery", "Aurora", "Emmy", "Atlas",
            "Vertex", "Astera", "Jasper", "Sasha", "Victor"
        };

        private string _notification = "";
        private float _notifyTimer = 0f;
        private int _totalClimaxesAllTime = 0;

        private Texture2D _texBarBg;
        private Texture2D _texWhite;
        private bool _texturesBuilt;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            try
            {
                hudToggleKey = (KeyCode)PlayerPrefs.GetInt("BasicUI_HudToggle", (int)KeyCode.F9);
                nextCharKey  = (KeyCode)PlayerPrefs.GetInt("BasicUI_NextChar", (int)KeyCode.RightBracket);
                prevCharKey  = (KeyCode)PlayerPrefs.GetInt("BasicUI_PrevChar", (int)KeyCode.LeftBracket);
                StartCoroutine(DelayedInit(3f));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[BasicUI] Start exception: " + e);
            }
        }

        private IEnumerator DelayedInit(float delay)
        {
            yield return new WaitForSeconds(delay);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try { ScanCharacters(); }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[BasicUI] Scan attempt " + attempt + ": " + e);
                }

                if (_trackers.Count > 0)
                    break;

                yield return new WaitForSeconds(3f);
            }

            _initialized = true;
            ShowNotification("Basic UI loaded  —  F9 HUD  |  Chars: " + _trackers.Count);
        }

        void Update()
        {
            if (Input.GetKeyDown(hudToggleKey))
            {
                _hudVisible = !_hudVisible;
                ShowNotification(_hudVisible ? "HUD ON" : "HUD OFF");
            }

            if (_trackers.Count > 0)
            {
                if (Input.GetKeyDown(nextCharKey))
                {
                    _selectedCharIndex = (_selectedCharIndex + 1) % _trackers.Count;
                    ShowNotification("Selected: " + _trackers[_selectedCharIndex].DisplayName);
                }
                if (Input.GetKeyDown(prevCharKey))
                {
                    _selectedCharIndex = (_selectedCharIndex - 1 + _trackers.Count) % _trackers.Count;
                    ShowNotification("Selected: " + _trackers[_selectedCharIndex].DisplayName);
                }
            }

            _sessionTimer += Time.deltaTime;
            if (_notifyTimer > 0) _notifyTimer -= Time.deltaTime;

            if (!_initialized) return;

            if (Time.frameCount % 120 == 0)
            {
                try { ScanCharacters(); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[BasicUI] Rescan: " + e.Message); }
            }

            var snapshot = new List<BasicCharacterTracker>(_trackers);
            foreach (var t in snapshot)
            {
                try { t.Poll(); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[BasicUI] Poll " + t.RawName + ": " + e.Message); }
            }

            for (int i = _trackers.Count - 1; i >= 0; i--)
            {
                if (_trackers[i].IsNpcDestroyed && !_trackers[i].IsInScene)
                {
                    _totalClimaxesAllTime += _trackers[i].ClimaxCount;
                    ShowNotification(_trackers[i].DisplayName + " left the scene");
                    _trackers.RemoveAt(i);
                }
            }

            if (_selectedCharIndex >= _trackers.Count)
                _selectedCharIndex = Mathf.Max(0, _trackers.Count - 1);
        }

        private void ScanCharacters()
        {
            for (int i = _trackers.Count - 1; i >= 0; i--)
            {
                var t = _trackers[i];
                if (t.IsNpcDestroyed || !t.IsInScene)
                {
                    if (t.IsInScene) t.MarkDespawned();
                    _totalClimaxesAllTime += t.ClimaxCount;
                    _trackers.RemoveAt(i);
                }
            }

            var currentNames = new HashSet<string>(_trackers.Select(t => t.RawName));

            if (!TryScanViaACM(currentNames))
                TryScanDirectScene(currentNames);

            if (_selectedCharIndex >= _trackers.Count)
                _selectedCharIndex = Mathf.Max(0, _trackers.Count - 1);
        }

        private Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName) return t;
                    }
                }
                catch { }
            }
            return null;
        }

        private bool TryScanViaACM(HashSet<string> currentNames)
        {
            try
            {
                Type acmType = FindTypeByName("ActiveCharacterManager");
                if (acmType == null) return false;

                PropertyInfo instanceProp = acmType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (instanceProp == null) return false;

                object acmInstance = instanceProp.GetValue(null);
                if (acmInstance == null) return false;

                MethodInfo findMethod = acmType.GetMethod("FindCharacterInScene", ALL);
                if (findMethod == null) return false;

                foreach (var name in ALL_CHARACTER_NAMES)
                {
                    try
                    {
                        object npc = findMethod.Invoke(acmInstance, new object[] { name });
                        if (npc != null && !currentNames.Contains(name))
                        {
                            Component npcComp = npc as Component;
                            if (npcComp == null)
                            {
                                GameObject go = npc as GameObject;
                                if (go != null) npcComp = go.GetComponent<MonoBehaviour>();
                            }

                            if (npcComp != null)
                            {
                                _trackers.Add(new BasicCharacterTracker(name, npcComp));
                                currentNames.Add(name);
                                ShowNotification(name + " entered the scene");
                            }
                        }
                        else if (npc == null && currentNames.Contains(name))
                        {
                            var old = _trackers.FirstOrDefault(t => t.RawName == name);
                            if (old != null)
                            {
                                _totalClimaxesAllTime += old.ClimaxCount;
                                ShowNotification(old.DisplayName + " left the scene");
                            }
                            _trackers.RemoveAll(t => t.RawName == name);
                            currentNames.Remove(name);
                        }
                    }
                    catch { }
                }
                return true;
            }
            catch { return false; }
        }

        private void TryScanDirectScene(HashSet<string> currentNames)
        {
            try
            {
                Type npcType = FindTypeByName("NPCController");
                if (npcType == null) return;

                var sceneNPCs = GameObject.FindObjectsOfType(npcType);
                var liveNames = new HashSet<string>();

                foreach (var obj in sceneNPCs)
                {
                    var comp = obj as Component;
                    if (comp == null) continue;

                    string charName = null;
                    var nameField = npcType.GetField("characterName", ALL);
                    if (nameField != null)
                    {
                        var val = nameField.GetValue(comp);
                        if (val != null) charName = val.ToString();
                    }

                    if (string.IsNullOrEmpty(charName))
                        charName = comp.gameObject.name;
                    if (charName.Contains("(Clone)"))
                        charName = charName.Replace("(Clone)", "").Trim();

                    liveNames.Add(charName);

                    if (!currentNames.Contains(charName))
                    {
                        _trackers.Add(new BasicCharacterTracker(charName, comp));
                        currentNames.Add(charName);
                        ShowNotification(charName + " entered the scene");
                    }
                }

                for (int i = _trackers.Count - 1; i >= 0; i--)
                {
                    if (!liveNames.Contains(_trackers[i].RawName))
                    {
                        _totalClimaxesAllTime += _trackers[i].ClimaxCount;
                        ShowNotification(_trackers[i].DisplayName + " left the scene");
                        _trackers.RemoveAt(i);
                    }
                }
            }
            catch { }
        }

        private void ShowNotification(string msg)
        {
            _notification = msg;
            _notifyTimer = 3f;
        }

        private void BuildTextures()
        {
            if (_texturesBuilt) return;
            _texturesBuilt = true;

            _texBarBg = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            _texWhite = MakeTex(2, 2, Color.white);
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pix);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        void OnGUI()
        {
            BuildTextures();

            if (_notifyTimer > 0)
            {
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(_notifyTimer));
                GUI.Box(new Rect(Screen.width / 2f - 180, 10, 360, 24), _notification);
                GUI.color = old;
            }

            GUI.Label(new Rect(10, Screen.height - 20, 300, 18),
                "[F9] HUD  |  [/] Cycle Chars");

            if (_hudVisible) DrawHUD();
        }

        private void DrawHUD()
        {
            if (_trackers.Count == 0)
            {
                GUI.Box(new Rect(Screen.width - 230, 8, 220, 28), "No characters in scene");
                return;
            }

            float panelW = 300;
            float cardH = 76;
            float headerH = 22;
            float footerH = 22;
            float totalH = headerH + (_trackers.Count * cardH) + footerH + 6;
            float x = Screen.width - panelW - 10;
            float y = 8;

            GUI.Box(new Rect(x, y, panelW, totalH), "");

            GUI.Label(new Rect(x + 8, y + 2, panelW - 16, headerH), "BASIC HUD");
            y += headerH;

            for (int i = 0; i < _trackers.Count; i++)
            {
                var t = _trackers[i];
                bool selected = i == _selectedCharIndex;
                float cy = y + i * cardH;

                string prefix = selected ? "> " : "  ";
                string nameLabel = prefix + t.DisplayName;

                string status;
                if (t.IsClimaxing) status = "CLIMAX!";
                else if (t.IsCloseToClimax) status = "Close";
                else if (t.PleasureDelta > 0.05f) status = "Active";
                else status = "Idle";

                GUI.Label(new Rect(x + 8, cy, panelW - 80, 18), nameLabel);
                GUI.Label(new Rect(x + panelW - 70, cy, 62, 18), status);

                float arousalPct = t.PleasureCap > 0 ? Mathf.Clamp01(t.PleasureLevel / t.PleasureCap) : 0f;
                DrawSimpleBar(new Rect(x + 10, cy + 18, panelW - 65, 10), arousalPct,
                    Color.Lerp(Color.cyan, Color.red, arousalPct));
                GUI.Label(new Rect(x + panelW - 52, cy + 16, 44, 14),
                    (arousalPct * 100f).ToString("F0") + "%");

                float climaxPct = t.ClimaxThreshold > 0 ? Mathf.Clamp01(t.PleasureLevel / t.ClimaxThreshold) : 0f;
                Color climaxCol = t.IsClimaxing ? Color.red : (t.IsCloseToClimax ? Color.yellow : Color.Lerp(Color.green, Color.red, climaxPct));
                DrawSimpleBar(new Rect(x + 10, cy + 32, panelW - 65, 8), climaxPct, climaxCol);
                string climaxBarLabel = t.IsClimaxing ? "MAX" : (climaxPct * 100f).ToString("F0") + "%";
                GUI.Label(new Rect(x + panelW - 52, cy + 29, 44, 14), climaxBarLabel);

                string infoLine = "Climaxes: " + t.ClimaxCount;
                if (t.IsPenetrated) infoLine += "  |  Penetrated";
                GUI.Label(new Rect(x + 10, cy + 46, panelW - 20, 18), infoLine);
            }

            float fy = y + _trackers.Count * cardH + 2;
            int totalCl = _totalClimaxesAllTime + _trackers.Sum(t => t.ClimaxCount);
            int m = (int)(_sessionTimer / 60f);
            int s = (int)(_sessionTimer % 60f);
            GUI.Label(new Rect(x + 8, fy, panelW - 16, 20),
                string.Format("Time: {0}:{1:D2}   Total: {2}", m, s, totalCl));
        }

        private void DrawSimpleBar(Rect rect, float pct, Color col)
        {
            GUI.DrawTexture(rect, _texBarBg);

            if (pct > 0.001f)
            {
                var old = GUI.color;
                GUI.color = col;
                GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1,
                    (rect.width - 2) * Mathf.Clamp01(pct), rect.height - 2), _texWhite);
                GUI.color = old;
            }
        }
    }
}
