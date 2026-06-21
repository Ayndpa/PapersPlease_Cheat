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

    // --- 缓存（主线程写入，渲染线程读取） ---
    private bool _inGame;
    private int _dayId;
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
    private float _clockTimescale = 1.0f;
    private double _durationInMinutes;

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

        // 应用时间流速微调 (对于检查亭时钟 boothClock)
        ApplyBoothClockTimescale();
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

            var booth = dayScreen.booth;
            _boothLoaded = booth != null;
            if (_boothLoaded)
            {
                try { _timeIsUp = booth!.get_timeIsUp(); } catch { }
            }

            var border = dayScreen.border;
            _borderLoaded = border != null;

            // 当进入新关卡或跨越新的一天时，重新应用当前流速设置，保证倍速能在重新加载后延续
            if ((!wasInGame || prevDayId != _dayId) && !Mathf.Approximately(_clockTimescale, 1.0f))
            {
                SetClockTimescale(_clockTimescale);
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
        // -- 修改金钱 --
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
        if (!_borderLoaded)
        {
            ImGui.TextDisabled("(边境未加载)");
            return;
        }

        ImGui.Text("当前时间流速: " + _clockTimescale.ToString("F2") + "x");

        // 减速预设
        ImGui.Text("时间减速预设:");
        if (ImGui.Button("极慢 (0.1x)"))
        {
            SetClockTimescale(0.1f);
            ShowStatus("时间减速至 0.1x");
        }
        ImGui.SameLine();
        if (ImGui.Button("较慢 (0.2x)"))
        {
            SetClockTimescale(0.2f);
            ShowStatus("时间减速至 0.2x");
        }
        ImGui.SameLine();
        if (ImGui.Button("半速 (0.5x)"))
        {
            SetClockTimescale(0.5f);
            ShowStatus("时间减速至 0.5x");
        }
        ImGui.SameLine();
        if (ImGui.Button("微慢 (0.8x)"))
        {
            SetClockTimescale(0.8f);
            ShowStatus("时间减速至 0.8x");
        }

        // 常规/加速预设
        ImGui.Text("时间常规/加速预设:");
        if (ImGui.Button("暂停 (0x)"))
        {
            SetClockTimescale(0);
            ShowStatus("时间已暂停");
        }
        ImGui.SameLine();
        if (ImGui.Button("正常 (1x)"))
        {
            SetClockTimescale(1);
            ShowStatus("时间恢复正常");
        }
        ImGui.SameLine();
        if (ImGui.Button("加速 (2x)"))
        {
            SetClockTimescale(2);
            ShowStatus("时间 2x 加速");
        }
        ImGui.SameLine();
        if (ImGui.Button("加速 (3x)"))
        {
            SetClockTimescale(3);
            ShowStatus("时间 3x 加速");
        }

        if (ImGui.Button("5x"))
        {
            SetClockTimescale(5);
            ShowStatus("时间 5x 加速");
        }
        ImGui.SameLine();
        if (ImGui.Button("10x"))
        {
            SetClockTimescale(10);
            ShowStatus("时间 10x 加速");
        }
        ImGui.SameLine();
        if (ImGui.Button("50x"))
        {
            SetClockTimescale(50);
            ShowStatus("时间 50x 加速");
        }

        // 滑动条精细调节
        ImGui.Spacing();
        ImGui.Text("精细调节:");
        float val = _clockTimescale;
        if (ImGui.SliderFloat("流速倍率", ref val, 0.0f, 10.0f, "%.2fx"))
        {
            SetClockTimescale(val);
        }
    }

    private void SetClockTimescale(float scale)
    {
        _clockTimescale = scale;
        MainThreadDispatcher.Enqueue(() =>
        {
            // 确保恢复全局时钟速度为默认的 1.0，避免干扰输入与其它引擎时钟
            try
            {
                global::app.Clock.globalSpeed = 1.0;
            }
            catch { }

            try
            {
                var border = GetDayScreen()?.border;
                if (border != null) border.localClockTimescale = scale; // 仅调节边境时钟流速
            }
            catch { }
        });
    }

    private void ApplyBoothClockTimescale()
    {
        if (!_inGame) return;
        if (Mathf.Approximately(_clockTimescale, 1.0f)) return;

        try
        {
            var dayScreen = GetDayScreen();
            var booth = dayScreen?.booth;
            if (booth != null)
            {
                var boothClock = booth.boothClock;
                if (boothClock != null)
                {
                    double dt = boothClock.dt;
                    // 只有当 dt > 0 时才进行补偿，防止游戏暂停时无限累加或倒退
                    if (dt > 0)
                    {
                        boothClock.time += (_clockTimescale - 1.0) * dt;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Error in ApplyBoothClockTimescale: " + e.Message);
        }
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
