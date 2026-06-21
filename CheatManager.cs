#nullable enable
using System;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using ImGuiNET;
using play.day;
using play.day.booth;
using play.day.border;
using play.screen;
using data;
using play;
using SNVector2 = System.Numerics.Vector2;
using SNVector4 = System.Numerics.Vector4;

namespace PapersPlease_Cheat;

/// <summary>
/// 作弊菜单，使用 DearImGuiInjection (ImGuiNET) 渲染 UI。
/// 游戏状态在 Update() 主线程中缓存，ImGui 渲染回调读取缓存；
/// 所有写操作通过 UnityMainThreadDispatcher 派发到主线程。
/// </summary>
public class CheatManager : MonoBehaviour
{
    private bool _menuOpen = true;
    private bool _firstUpdate = true;
    private string _lastScreenType = "";
    private bool _lastMainGameNull = true;

    // --- 状态文本 ---
    private string _statusText = "";
    private float _statusTimer;

    // --- 用户输入（byte[] 用于 ImGui InputText） ---
    private byte[] _moneyBuf = new byte[32];
    private byte[] _durationBuf = new byte[32];
    private byte[] _citationsBuf = new byte[32];
    private byte[] _savingsBuf = new byte[32];

    // --- 缓存（主线程写入，渲染线程读取） ---
    private bool _inGame;
    private int _dayId;
    private int _savings;
    private int _bribeMoney;
    private int _numProcessedPaid;
    private int _numProcessedUnpaid;
    private int _numMadeTravelers;
    private int _numDetains;
    private int _numCitations;
    private int _penaltyCost;
    private bool _timeIsUp;
    private bool _boothLoaded;
    private bool _borderLoaded;
    private float _userSliderHour = 6.0f;
    private bool _isDraggingSlider;
    private double _durationInMinutes;

    // --- 工作棚升级状态 ---
    private bool _unlockSpacebarHotkey;
    private bool _unlockTabHotkey;
    private bool _unlockDoubleClick;
    private bool _unlockRulebookTabs;

    // --- 调试辅助缓存 ---
    private HostUnity? _hostUnity;
    private bool _dbHostUnityFound = false;
    private bool _dbHostUnityGameNull = true;

    private bool _dbMainGameNull = true;
    private string _dbMainGameType = "None";
    private bool _dbCastGameSuccess = false;
    private bool _dbGameScreenNull = true;
    private string _dbGameScreenType = "None";
    private bool _dbCastDayScreenSuccess = false;
    private bool _dbDayNull = true;
    private string _lastDebugStateMsg = "";

    public CheatManager(IntPtr ptr) : base(ptr) { }

    private void OnEnable()
    {
        DearImGuiInjection.DearImGuiInjection.Render += OnImGuiRender;
    }

    private void OnDisable()
    {
        DearImGuiInjection.DearImGuiInjection.Render -= OnImGuiRender;
    }

    private void Update()
    {
        // 状态计时器
        if (_statusTimer > 0)
        {
            _statusTimer -= Time.deltaTime;
            if (_statusTimer <= 0) _statusText = "";
        }

        if (_firstUpdate)
        {
            _firstUpdate = false;
            SetImGuiCursorVisible(_menuOpen);
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            _menuOpen = !_menuOpen;
            SetImGuiCursorVisible(_menuOpen);
        }

        // 在主线程缓存游戏状态
        CacheGameState();
    }

    private void ShowStatus(string msg)
    {
        _statusText = msg;
        _statusTimer = 3f;
    }

    // ===================== byte[] <-> string 辅助 =====================

    private static void StringToBuf(string s, byte[] buf)
    {
        var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
        var len = global::System.Math.Min(bytes.Length, buf.Length - 1);
        global::System.Array.Clear(buf, 0, buf.Length);
        global::System.Array.Copy(bytes, buf, len);
    }

    private static string BufToString(byte[] buf)
    {
        int end = global::System.Array.IndexOf(buf, (byte)0);
        if (end < 0) end = buf.Length;
        return global::System.Text.Encoding.UTF8.GetString(buf, 0, end);
    }

    // ===================== 游戏状态缓存（主线程） =====================

    private Game? GetGame()
    {
        try
        {
            IGame? igame = global::Main.game;
            _dbMainGameNull = igame == null;

            if (igame == null)
            {
                if (_hostUnity == null)
                {
                    _hostUnity = CustomFindObjectOfType<HostUnity>();
                }

                _dbHostUnityFound = _hostUnity != null;
                if (_hostUnity != null)
                {
                    igame = _hostUnity.game;
                    _dbHostUnityGameNull = igame == null;
                }
                else
                {
                    _dbHostUnityGameNull = true;
                }
            }
            else
            {
                _dbHostUnityFound = false;
                _dbHostUnityGameNull = true;
            }

            if (igame == null) return null;
            return igame.TryCast<Game>();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Error getting Game instance: " + e.ToString());
            return null;
        }
    }

    private DayScreen? GetDayScreen()
    {
        var game = GetGame();
        if (game == null) return null;
        var gs = game.gameScreen;
        if (gs == null) return null;
        return gs.TryCast<DayScreen>();
    }

    private void CacheGameState()
    {
        try
        {
            IGame? igame = global::Main.game;
            _dbMainGameNull = igame == null;

            if (igame == null)
            {
                if (_hostUnity == null)
                {
                    _hostUnity = CustomFindObjectOfType<HostUnity>();
                }

                _dbHostUnityFound = _hostUnity != null;
                if (_hostUnity != null)
                {
                    igame = _hostUnity.game;
                    _dbHostUnityGameNull = igame == null;
                }
                else
                {
                    _dbHostUnityGameNull = true;
                }
            }
            else
            {
                _dbHostUnityFound = false;
                _dbHostUnityGameNull = true;
            }

            if (igame == null)
            {
                _dbMainGameType = "None";
                _dbCastGameSuccess = false;
                _dbGameScreenNull = true;
                _dbGameScreenType = "None";
                _dbCastDayScreenSuccess = false;
                _dbDayNull = true;

                LogDebugState();

                if (!_lastMainGameNull)
                {
                    _lastMainGameNull = true;
                    Plugin.Log.LogInfo("Game instance is null");
                }
                _inGame = false;
                return;
            }

            if (_lastMainGameNull)
            {
                _lastMainGameNull = false;
                Plugin.Log.LogInfo("Game instance is not null");
            }

            _dbMainGameType = igame.TryCast<Il2CppSystem.Object>()?.GetIl2CppType()?.FullName ?? "Unknown";

            var game = igame.TryCast<Game>();
            _dbCastGameSuccess = game != null;
            if (game == null)
            {
                _dbGameScreenNull = true;
                _dbGameScreenType = "None";
                _dbCastDayScreenSuccess = false;
                _dbDayNull = true;

                LogDebugState();

                _inGame = false;
                return;
            }

            var gs = game.gameScreen;
            _dbGameScreenNull = gs == null;
            if (gs == null)
            {
                _dbGameScreenType = "None";
                _dbCastDayScreenSuccess = false;
                _dbDayNull = true;

                LogDebugState();

                if (_lastScreenType != "null")
                {
                    _lastScreenType = "null";
                    Plugin.Log.LogInfo("gameScreen is null");
                }
                _inGame = false;
                return;
            }

            string currentScreenType = gs.GetIl2CppType().FullName;
            _dbGameScreenType = currentScreenType;
            if (_lastScreenType != currentScreenType)
            {
                _lastScreenType = currentScreenType;
                Plugin.Log.LogInfo("gameScreen type changed to: " + currentScreenType);
            }

            var dayScreen = gs.TryCast<DayScreen>();
            _dbCastDayScreenSuccess = dayScreen != null;
            if (dayScreen == null)
            {
                _dbDayNull = true;

                LogDebugState();

                _inGame = false;
                return;
            }

            var day = dayScreen.day;
            _dbDayNull = day == null;
            if (day == null)
            {
                LogDebugState();

                _inGame = false;
                return;
            }

            LogDebugState();

            bool wasInGame = _inGame;
            int prevDayId = _dayId;

            _inGame = true;
            _dayId = day.id;
            _bribeMoney = day.bribeMoney;
            _numProcessedPaid = day.numProcessedTravelersPaid;
            _numProcessedUnpaid = day.numProcessedTravelersUnpaid;
            _numMadeTravelers = day.numMadeTravelers;
            _numDetains = day.numDetains;
            _durationInMinutes = day.durationInMinutes;

            try { _numCitations = day.get_numCitations(); } catch { }
            try { _penaltyCost = day.get_penaltyCost(); } catch { }

            _savings = 0;
            try
            {
                var storyStateObj = day.get_storyState();
                if (storyStateObj != null && storyStateObj.facts != null)
                {
                    _savings = storyStateObj.facts.has("savings") ? storyStateObj.facts.getValueInt("savings", null) : 0;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Error fetching savings: " + ex.Message);
            }

            var booth = dayScreen.booth;
            _boothLoaded = booth != null;
            if (_boothLoaded)
            {
                try { _timeIsUp = booth!.get_timeIsUp(); } catch { }
            }

            var border = dayScreen.border;
            _borderLoaded = border != null;

            // 缓存工作棚升级状态
            var storyState = day.get_storyState();
            if (storyState != null)
            {
                _unlockSpacebarHotkey = storyState.hasUpgrade(Upgrade.INSPECT_HOTKEY);
                _unlockTabHotkey = storyState.hasUpgrade(Upgrade.STAMPBAR_HOTKEY);
                _unlockDoubleClick = storyState.hasUpgrade(Upgrade.INSPECT_DOUBLECLICK);
                _unlockRulebookTabs = storyState.hasUpgrade(Upgrade.RULEBOOK_TABS);
            }

            // 当进入新关卡或跨越新的一天时，确保将时间流速和全局流速重置为默认值 1.0，防止受之前设置的影响
            if (!wasInGame || prevDayId != _dayId)
            {
                StringToBuf(_bribeMoney.ToString(), _moneyBuf);
                StringToBuf(_durationInMinutes.ToString("F0"), _durationBuf);
                StringToBuf(_numCitations.ToString(), _citationsBuf);
                StringToBuf(_savings.ToString(), _savingsBuf);

                MainThreadDispatcher.Enqueue(() =>
                {
                    try { global::app.Clock.globalSpeed = 1.0; } catch { }
                    try { if (border != null) border.localClockTimescale = 1.0; } catch { }
                });
            }
        }
        catch (Exception e)
        {
            _inGame = false;
            Plugin.Log.LogError("Error in CacheGameState: " + e.ToString());
        }
    }

    private void LogDebugState()
    {
        string debugStateMsg = $"MainGameNull: {_dbMainGameNull}, HostUnityFound: {_dbHostUnityFound}, HostUnityGameNull: {_dbHostUnityGameNull}, MainGameType: {_dbMainGameType}, CastGame: {_dbCastGameSuccess}, GameScreenNull: {_dbGameScreenNull}, GameScreenType: {_dbGameScreenType}, CastDayScreen: {_dbCastDayScreenSuccess}, DayNull: {_dbDayNull}";
        if (_lastDebugStateMsg != debugStateMsg)
        {
            _lastDebugStateMsg = debugStateMsg;
            Plugin.Log.LogInfo("[CheatManager State Change] " + debugStateMsg);
        }
    }

    // ===================== ImGui 渲染 =====================

    private void OnImGuiRender()
    {
        // 主菜单栏
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Papers Please Cheat"))
            {
                if (ImGui.MenuItem("显示/隐藏作弊窗口", "F1"))
                {
                    _menuOpen = !_menuOpen;
                    SetImGuiCursorVisible(_menuOpen);
                }
                ImGui.EndMenu();
            }
            ImGui.EndMainMenuBar();
        }

        if (!_menuOpen)
        {
            SetImGuiCursorVisible(false);
            return;
        }

        ImGui.SetNextWindowSize(new SNVector2(340, 520), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Papers Please Cheat  [F1]", ref _menuOpen))
        {
            if (!_inGame)
            {
                ImGui.Text("未在游戏中...");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("【系统调试信息 / Debug Info】");
                ImGui.Text($"Main.game is null: {_dbMainGameNull}");
                ImGui.Text($"HostUnity found: {_dbHostUnityFound}");
                ImGui.Text($"HostUnity.game is null: {_dbHostUnityGameNull}");
                ImGui.Text($"Resolved Game Type: {_dbMainGameType}");
                ImGui.Text($"Cast to Game success: {_dbCastGameSuccess}");
                ImGui.Text($"gameScreen is null: {_dbGameScreenNull}");
                ImGui.Text($"gameScreen Type: {_dbGameScreenType}");
                ImGui.Text($"Cast to DayScreen success: {_dbCastDayScreenSuccess}");
                ImGui.Text($"day is null: {_dbDayNull}");
                ImGui.Separator();
                DrawStatusText();
                ImGui.End();
                return;
            }

            // ===== 当日信息 =====
            if (ImGui.CollapsingHeader("当日信息", ImGuiTreeNodeFlags.DefaultOpen))
                DrawInfoPanel();

            ImGui.Spacing();

            // ===== 作弊功能 =====
            if (ImGui.CollapsingHeader("作弊功能", ImGuiTreeNodeFlags.DefaultOpen))
                DrawCheatButtons();

            ImGui.Spacing();

            // ===== 时间控制 =====
            if (ImGui.CollapsingHeader("时间控制", ImGuiTreeNodeFlags.DefaultOpen))
                DrawTimeControls();

            ImGui.Spacing();

            // ===== 工作棚升级 =====
            if (ImGui.CollapsingHeader("工作棚升级", ImGuiTreeNodeFlags.DefaultOpen))
                DrawBoothUpgrades();

            ImGui.Spacing();

            // ===== 快捷操作 =====
            if (ImGui.CollapsingHeader("快捷操作", ImGuiTreeNodeFlags.DefaultOpen))
                DrawQuickActions();

            DrawStatusText();
        }
        ImGui.End();
    }

    private void DrawStatusText()
    {
        if (!string.IsNullOrEmpty(_statusText))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new SNVector4(0, 1, 1, 1)));
            ImGui.Text(_statusText);
            ImGui.PopStyleColor();
        }
    }

    private void DrawInfoPanel()
    {
        ImGui.Text("天数: " + _dayId);
        ImGui.Text("当前存款: " + _savings);
        ImGui.Text("贿赂收入: " + _bribeMoney);
        ImGui.Text("处理旅客(付费): " + _numProcessedPaid);
        ImGui.Text("处理旅客(未付): " + _numProcessedUnpaid);
        ImGui.Text("已产生旅客: " + _numMadeTravelers);
        ImGui.Text("拘留数: " + _numDetains);
        ImGui.Text("罚单数: " + _numCitations + "  罚款: " + _penaltyCost);

        if (_boothLoaded)
            ImGui.Text("时间: " + (_timeIsUp ? "已结束" : "进行中"));
    }

    private void DrawCheatButtons()
    {
        // -- 修改存款 (长期储蓄) --
        ImGui.PushItemWidth(80);
        ImGui.Text("修改存款:");
        ImGui.SameLine();
        ImGui.InputText("##savings", _savingsBuf, (uint)_savingsBuf.Length);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGui.Button("设置##savings"))
        {
            if (int.TryParse(BufToString(_savingsBuf), out int val))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var day = GetDayScreen()?.day;
                        var storyState = day?.get_storyState();
                        if (storyState != null && storyState.facts != null)
                        {
                            storyState.facts.setValueInt("savings", val);
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError("Error setting savings: " + ex.Message);
                    }
                });
                _savings = val;
                ShowStatus("存款已修改为 " + val);
            }
        }

        // -- 修改贿赂收入 --
        ImGui.PushItemWidth(80);
        ImGui.Text("贿赂收入:");
        ImGui.SameLine();
        ImGui.InputText("##money", _moneyBuf, (uint)_moneyBuf.Length);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGui.Button("设置##money"))
        {
            if (int.TryParse(BufToString(_moneyBuf), out int val))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    var day = GetDayScreen()?.day;
                    if (day != null) day.bribeMoney = val;
                });
                _bribeMoney = val;
                ShowStatus("贿赂收入已设为 " + val);
            }
        }

        // -- 修改罚单数 --
        ImGui.PushItemWidth(80);
        ImGui.Text("罚单数量:");
        ImGui.SameLine();
        ImGui.InputText("##citations", _citationsBuf, (uint)_citationsBuf.Length);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGui.Button("设置##citations"))
        {
            if (int.TryParse(BufToString(_citationsBuf), out int val) && val >= 0)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var day = GetDayScreen()?.day;
                        if (day != null && day.citations != null)
                        {
                            int currentCount = day.citations.length;
                            if (val < currentCount)
                            {
                                for (int i = 0; i < currentCount - val; i++)
                                {
                                    day.citations.pop();
                                }
                            }
                            else if (val > currentCount)
                            {
                                for (int i = 0; i < val - currentCount; i++)
                                {
                                    day.addCitation("作弊罚单");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError("Error setting citations: " + ex.Message);
                    }
                });
                _numCitations = val;
                ShowStatus("罚单数量已设为 " + val);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("清空##citations"))
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    var day = GetDayScreen()?.day;
                    if (day != null && day.citations != null)
                    {
                        day.citations.spliceVoid(0, day.citations.length);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("Error clearing citations: " + ex.Message);
                }
            });
            _numCitations = 0;
            StringToBuf("0", _citationsBuf);
            ShowStatus("已清空当日罚单");
        }

        // -- 设置天数时长 --
        ImGui.PushItemWidth(80);
        ImGui.Text("时长(分):");
        ImGui.SameLine();
        // 首次填充时长缓冲区
        if (BufToString(_durationBuf).Length == 0 && _durationInMinutes > 0)
            StringToBuf(_durationInMinutes.ToString("F0"), _durationBuf);
        ImGui.InputText("##duration", _durationBuf, (uint)_durationBuf.Length);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGui.Button("设置##duration"))
        {
            if (double.TryParse(BufToString(_durationBuf), out double val))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    var day = GetDayScreen()?.day;
                    if (day != null) day.durationInMinutes = val;
                });
                _durationInMinutes = val;
                ShowStatus("当天时长已设为 " + val + " 分钟");
            }
        }

        // -- 减少等待队列 --
        if (ImGui.Button("减少等待队列人数"))
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    var day = GetDayScreen()?.day;
                    if (day != null)
                    {
                        day.waitingLineLength = 0;
                        ShowStatus("等待队列已清空");
                    }
                }
                catch (Exception e) { ShowStatus("操作失败: " + e.Message); }
            });
        }
    }

    private void DrawTimeControls()
    {
        if (!_boothLoaded)
        {
            ImGui.TextDisabled("(检查亭未加载)");
            return;
        }

        try
        {
            var dayScreen = GetDayScreen();
            var booth = dayScreen?.booth;
            var boothClock = booth?.boothClock;
            var border = dayScreen?.border;

            if (booth != null && booth.consoleEnt != null && _durationInMinutes > 0)
            {
                double currentHour = booth.consoleEnt.get_hour();
                if (currentHour < 6.0) currentHour = 6.0;
                if (currentHour > 18.0) currentHour = 18.0;

                int displayHour = (int)currentHour;
                int displayMinute = (int)((currentHour - displayHour) * 60.0);

                ImGui.Text("当前游戏时间: " + $"{displayHour:D2}:{displayMinute:D2}");

                // 诊断信息
                ImGui.Text($"[DEBUG] boothClock.time: {boothClock?.time ?? -1f:F2}");
                ImGui.Text($"[DEBUG] boothClock.get_time(): {boothClock?.get_time() ?? -1f:F2}");
                if (border != null && border.localClock != null)
                {
                    ImGui.Text($"[DEBUG] borderClock.time: {border.localClock.time:F2}");
                    ImGui.Text($"[DEBUG] borderClock.get_time(): {border.localClock.get_time():F2}");
                }
                ImGui.Text($"[DEBUG] consoleEnt.get_hour(): {booth.consoleEnt.get_hour():F2}");
                if (booth.consoleEnt.consoleClock != null)
                {
                    ImGui.Text($"[DEBUG] consoleClock.hour: {booth.consoleEnt.consoleClock.hour:F2}");
                }

                // 如果用户没有在拖动滑块，则让滑块跟随游戏时间自动更新
                if (!_isDraggingSlider)
                {
                    _userSliderHour = (float)currentHour;
                }

                if (ImGui.SliderFloat("滑动调整时间", ref _userSliderHour, 6.0f, 18.0f, " "))
                {
                    double targetHour = _userSliderHour;
                    int targetH = (int)targetHour;
                    int targetM = (int)((targetHour - targetH) * 60.0);

                    MainThreadDispatcher.Enqueue(() =>
                    {
                        try
                        {
                            if (booth.consoleEnt != null)
                            {
                                booth.consoleEnt.set_hour(targetHour);
                            }

                            // 同时也更新 boothClock 和 border.localClock（如果有逻辑在使用它们）
                            if (boothClock != null && _durationInMinutes > 0)
                            {
                                double totalSeconds = _durationInMinutes * 60.0;
                                double newRatio = (targetHour - 6.0) / 12.0;
                                double newTime = newRatio * totalSeconds;
                                boothClock.setTime(newTime);
                                if (border != null && border.localClock != null)
                                {
                                    border.localClock.setTime(newTime);
                                }
                            }
                            ShowStatus("时间已调整为 " + $"{targetH:D2}:{targetM:D2}");
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.LogError("Error setting time: " + e.Message);
                        }
                    });
                }

                // 更新拖动状态
                _isDraggingSlider = ImGui.IsItemActive();

                // 快捷设定按钮
                ImGui.Text("快捷设定:");
                if (ImGui.Button("设为早上 (08:00)"))
                {
                    SetGameTime(8.0);
                }
                ImGui.SameLine();
                if (ImGui.Button("设为中午 (12:00)"))
                {
                    SetGameTime(12.0);
                }
                ImGui.SameLine();
                if (ImGui.Button("设为下午 (15:00)"))
                {
                    SetGameTime(15.0);
                }
                ImGui.SameLine();
                if (ImGui.Button("设为下班前 (17:50)"))
                {
                    SetGameTime(17.833); // 17 + 50/60
                }
            }
            else
            {
                ImGui.TextDisabled("(时钟不可用)");
            }
        }
        catch (Exception e)
        {
            ImGui.TextDisabled("(时间控制加载失败)");
            Plugin.Log.LogError("Error in DrawTimeControls: " + e.Message);
        }
    }

    private void SetGameTime(double targetHour)
    {
        MainThreadDispatcher.Enqueue(() =>
        {
            try
            {
                var dayScreen = GetDayScreen();
                var booth = dayScreen?.booth;
                if (booth != null && booth.consoleEnt != null)
                {
                    booth.consoleEnt.set_hour(targetHour);
                }

                var boothClock = booth?.boothClock;
                if (boothClock != null && _durationInMinutes > 0)
                {
                    double totalSeconds = _durationInMinutes * 60.0;
                    double newRatio = (targetHour - 6.0) / 12.0;
                    double newTime = newRatio * totalSeconds;

                    boothClock.setTime(newTime);
                    var border = dayScreen?.border;
                    if (border != null && border.localClock != null)
                    {
                        border.localClock.setTime(newTime);
                    }
                }

                int h = (int)targetHour;
                int m = (int)((targetHour - h) * 60.0);
                ShowStatus("时间已设为 " + $"{h:D2}:{m:D2}");
            }
            catch (Exception e)
            {
                ShowStatus("设定失败: " + e.Message);
            }
        });
    }

    private void DrawQuickActions()
    {
        if (_boothLoaded)
        {
            if (ImGui.Button("自动盖章：全部批准"))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        GetDayScreen()?.booth?.debugStampAll(StampApprovalKind.APPROVED);
                        ShowStatus("已自动盖章：全部批准");
                    }
                    catch (Exception e) { ShowStatus("盖章失败: " + e.Message); }
                });
            }

            if (ImGui.Button("自动盖章：全部拒绝"))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        GetDayScreen()?.booth?.debugStampAll(StampApprovalKind.DENIED);
                        ShowStatus("已自动盖章：全部拒绝");
                    }
                    catch (Exception e) { ShowStatus("盖章失败: " + e.Message); }
                });
            }

            if (ImGui.Button("呼叫下一位旅客"))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        GetDayScreen()?.booth?.acceptNextTraveler();
                        ShowStatus("已呼叫下一位旅客");
                    }
                    catch (Exception e) { ShowStatus("呼叫失败: " + e.Message); }
                });
            }

            if (ImGui.Button("打开/关闭检查界面"))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var inspectUi = GetDayScreen()?.booth?.inspectUi;
                        if (inspectUi != null)
                        {
                            bool isOpen = inspectUi.get_open();
                            inspectUi.set_open(!isOpen);
                            ShowStatus(isOpen ? "检查界面已关闭" : "检查界面已打开");
                        }
                    }
                    catch (Exception e) { ShowStatus("操作失败: " + e.Message); }
                });
            }
        }

        if (_borderLoaded)
        {
            if (ImGui.Button("启用狙击"))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    try { GetDayScreen()?.border?.set_snipingEnabled(true); ShowStatus("狙击已启用"); }
                    catch (Exception e) { ShowStatus("操作失败: " + e.Message); }
                });
            }
            ImGui.SameLine();
            if (ImGui.Button("禁用狙击"))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    try { GetDayScreen()?.border?.set_snipingEnabled(false); ShowStatus("狙击已禁用"); }
                    catch (Exception e) { ShowStatus("操作失败: " + e.Message); }
                });
            }
        }
    }

    private void DrawBoothUpgrades()
    {
        var dayScreen = GetDayScreen();
        if (dayScreen == null || dayScreen.day == null)
        {
            ImGui.TextDisabled("(游戏或存档未加载)");
            return;
        }

        bool spaceVal = _unlockSpacebarHotkey;
        if (ImGui.Checkbox("空格键快捷键 (开启/关闭对比模式)", ref spaceVal))
        {
            ToggleUpgrade(Upgrade.INSPECT_HOTKEY, spaceVal, "空格键快捷键");
        }

        bool tabVal = _unlockTabHotkey;
        if (ImGui.Checkbox("Tab/Enter 快捷键 (弹出/收回盖章面板)", ref tabVal))
        {
            ToggleUpgrade(Upgrade.STAMPBAR_HOTKEY, tabVal, "Tab/Enter 快捷键");
        }

        bool doubleClickVal = _unlockDoubleClick;
        if (ImGui.Checkbox("鼠标双击快捷键 (自动关联对比信息)", ref doubleClickVal))
        {
            ToggleUpgrade(Upgrade.INSPECT_DOUBLECLICK, doubleClickVal, "双击快捷关联");
        }

        bool rulebookVal = _unlockRulebookTabs;
        if (ImGui.Checkbox("规则书标签 (Rulebook Tabs 快速翻页)", ref rulebookVal))
        {
            ToggleUpgrade(Upgrade.RULEBOOK_TABS, rulebookVal, "规则书标签");
        }

        ImGui.Spacing();
        if (ImGui.Button("解锁全部升级##all_upgrades"))
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    var storyState = GetDayScreen()?.day?.get_storyState();
                    if (storyState != null)
                    {
                        storyState.giveUpgrade(Upgrade.INSPECT_HOTKEY);
                        storyState.giveUpgrade(Upgrade.STAMPBAR_HOTKEY);
                        storyState.giveUpgrade(Upgrade.INSPECT_DOUBLECLICK);
                        storyState.giveUpgrade(Upgrade.RULEBOOK_TABS);
                        ShowStatus("已解锁全部工作棚升级");
                    }
                }
                catch (Exception e) { ShowStatus("解锁失败: " + e.Message); }
            });
        }

        ImGui.SameLine();
        if (ImGui.Button("输出 Fact 调试日志##debug_facts"))
        {
            LogAllFacts();
            ShowStatus("Fact 状态已输出至 BepInEx 控制台/日志");
        }
    }

    private void ToggleUpgrade(Upgrade upgrade, bool enable, string displayName)
    {
        MainThreadDispatcher.Enqueue(() =>
        {
            try
            {
                var storyState = GetDayScreen()?.day?.get_storyState();
                if (storyState != null)
                {
                    if (enable)
                    {
                        storyState.giveUpgrade(upgrade);
                        ShowStatus("已解锁: " + displayName);
                    }
                    else
                    {
                        string tag = upgrade.getTag();
                        // 尝试以不同的键名格式移除该事实，以锁回升级
                        storyState.facts.facts.remove(tag);
                        storyState.facts.facts.remove("upgrade:" + tag);
                        storyState.facts.facts.remove("upgrade." + tag);
                        storyState.facts.facts.remove("upgrade_" + tag);
                        ShowStatus("已锁定: " + displayName);
                    }
                }
            }
            catch (Exception e)
            {
                ShowStatus("操作失败: " + e.Message);
            }
        });
    }

    private void LogAllFacts()
    {
        try
        {
            var storyState = GetDayScreen()?.day?.get_storyState();
            if (storyState != null && storyState.facts != null && storyState.facts.facts != null)
            {
                var stringMap = storyState.facts.facts;
                var keysIter = new haxe.ds._StringMap.StringMapKeyIterator(stringMap);
                Plugin.Log.LogInfo("=== PapersPlease Cheat: StoryState Facts Dump ===");
                while (keysIter.hasNext())
                {
                    string key = keysIter.next();
                    Plugin.Log.LogInfo("Fact Key: " + key);
                }
                Plugin.Log.LogInfo("=================================================");
            }
            else
            {
                Plugin.Log.LogWarning("StoryState or Facts is null.");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Error dumping facts: " + e.ToString());
        }
    }

    private static void SetImGuiCursorVisible(bool visible)
    {
        try
        {
            var type = typeof(DearImGuiInjection.DearImGuiInjection);
            
            // Set IsCursorVisible property
            var prop = type.GetProperty("IsCursorVisible", global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Static);
            if (prop != null)
            {
                prop.SetValue(null, visible);
            }
            
            // Call UpdateCursorVisibility method
            var method = type.GetMethod("UpdateCursorVisibility", global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
            }
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    private static T? CustomFindObjectOfType<T>() where T : MonoBehaviour
    {
        try
        {
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                foreach (var go in rootObjects)
                {
                    if (go == null) continue;
                    var comp = FindComponentInHierarchy<T>(go.transform);
                    if (comp != null) return comp;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("Error in CustomFindObjectOfType: " + ex.Message);
        }
        return null;
    }

    private static T? FindComponentInHierarchy<T>(Transform trans) where T : MonoBehaviour
    {
        if (trans == null) return null;

        var comp = trans.GetComponent<T>();
        if (comp != null) return comp;

        int childCount = trans.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var child = trans.GetChild(i);
            var result = FindComponentInHierarchy<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
