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
    private float _clockTimescale;
    private double _durationInMinutes;

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
            var igame = global::Main.game;
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
            var dayScreen = GetDayScreen();
            if (dayScreen == null)
            {
                _inGame = false;
                return;
            }

            var day = dayScreen.day;
            if (day == null)
            {
                _inGame = false;
                return;
            }

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
            if (_borderLoaded)
            {
                _clockTimescale = (float)border!.localClockTimescale;
            }
        }
        catch (Exception e)
        {
            _inGame = false;
            Plugin.Log.LogError("Error in CacheGameState: " + e.ToString());
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

        ImGui.Text("当前时间流速: " + _clockTimescale.ToString("F1") + "x");

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
    }

    private void SetClockTimescale(float scale)
    {
        _clockTimescale = scale;
        MainThreadDispatcher.Enqueue(() =>
        {
            var border = GetDayScreen()?.border;
            if (border != null) border.localClockTimescale = scale;
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
}
