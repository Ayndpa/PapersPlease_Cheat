#nullable enable
using System;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using play.day;
using play.day.booth;
using play.day.border;
using play.screen;
using data;

namespace PapersPlease_Cheat;

/// <summary>
/// 作弊菜单 MonoBehaviour，挂在场景中，使用 OnGUI 渲染覆盖层。
/// </summary>
public class CheatManager : MonoBehaviour
{
    private bool _menuOpen;
    private Rect _windowRect = new(20, 20, 320, 520);
    private Vector2 _scrollPos;
    private string _statusText = "";
    private float _statusTimer;

    // --- 缓存 ---
    private HostUnity? _hostUnity;
    private Game? _game;

    // --- 用户输入 ---
    private string _moneyInput = "9999";
    private string _dayDurationInput = "";

    public CheatManager(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        // F1 切换菜单
        if (UnityEngine.Input.GetKeyDown(KeyCode.F1))
            _menuOpen = !_menuOpen;
    }

    private void ShowStatus(string msg)
    {
        _statusText = msg;
        _statusTimer = 3f;
    }

    // ===================== 游戏状态访问 =====================

    private HostUnity? GetHostUnity()
    {
        if (_hostUnity == null)
            _hostUnity = FindObjectOfType<HostUnity>();
        return _hostUnity;
    }

    private Game? GetGame()
    {
        var host = GetHostUnity();
        if (host == null) return null;
        if (_game == null)
        {
            var igame = host.game;
            if (igame != null)
                _game = igame.TryCast<Game>();
        }
        return _game;
    }

    private DayScreen? GetDayScreen()
    {
        var game = GetGame();
        if (game == null) return null;
        var gs = game.gameScreen;
        if (gs == null) return null;
        return gs.TryCast<DayScreen>();
    }

    private Day? GetDay() => GetDayScreen()?.day;
    private Booth? GetBooth() => GetDayScreen()?.booth;
    private Border? GetBorder() => GetDayScreen()?.border;

    // ===================== GUI =====================

    private void OnGUI()
    {
        if (!_menuOpen) return;

        _windowRect.width = 320;
        _windowRect.height = Mathf.Min(520, Screen.height - 40);
        GUI.Window(9999, _windowRect, (GUI.WindowFunction)DrawWindow, "Papers Please Cheat  [F1]");
    }

    private void DrawWindow(int id)
    {
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        // --- 状态计时器 ---
        if (_statusTimer > 0)
        {
            _statusTimer -= Time.deltaTime;
            if (_statusTimer <= 0) _statusText = "";
        }

        var day = GetDay();
        var booth = GetBooth();
        var border = GetBorder();

        if (day == null)
        {
            GUILayout.Label("未在游戏中..."); // 未在游戏中...
            ShowStatusIfAny();
            GUILayout.EndScrollView();
            GUI.DragWindow();
            return;
        }

        // ===== 信息面板 =====
        GUILayout.Label("═══════ 当日信息 ═══════"); // ═══════ 当日信息 ═══════
        DrawInfoPanel(day, booth);

        GUILayout.Space(8);

        // ===== 作弊功能 =====
        GUILayout.Label("═══════ 作弊功能 ═══════"); // ═══════ 作弊功能 ═══════
        DrawCheatButtons(day, booth);

        GUILayout.Space(8);

        // ===== 时间控制 =====
        GUILayout.Label("═══════ 时间控制 ═══════"); // ═══════ 时间控制 ═══════
        DrawTimeControls(border);

        GUILayout.Space(8);

        // ===== 快捷操作 =====
        GUILayout.Label("═══════ 快捷操作 ═══════"); // ═══════ 快捷操作 ═══════
        DrawQuickActions(booth, border);

        ShowStatusIfAny();

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private void ShowStatusIfAny()
    {
        if (!string.IsNullOrEmpty(_statusText))
        {
            GUI.color = Color.cyan;
            GUILayout.Label(_statusText);
            GUI.color = Color.white;
        }
    }

    private void DrawInfoPanel(Day day, Booth? booth)
    {
        GUILayout.Label("  天数: " + day.id);             // 天数
        GUILayout.Label("  贿赂收入: " + day.bribeMoney); // 贿赂收入
        GUILayout.Label("  处理旅客(付费): " + day.numProcessedTravelersPaid);  // 处理旅客(付费)
        GUILayout.Label("  处理旅客(未付): " + day.numProcessedTravelersUnpaid); // 处理旅客(未付)
        GUILayout.Label("  已产生旅客: " + day.numMadeTravelers); // 已产生旅客
        GUILayout.Label("  拘留数: " + day.numDetains); // 拘留数

        try
        {
            int numCitations = day.get_numCitations();
            int penaltyCost = day.get_penaltyCost();
            GUILayout.Label("  罚单数: " + numCitations + "  罚款: " + penaltyCost); // 罚单数  罚款
        }
        catch { }

        if (booth != null)
        {
            try
            {
                bool timeUp = booth.get_timeIsUp();
                GUILayout.Label("  时间: " + (timeUp ? "已结束" : "进行中")); // 时间: 已结束/进行中
            }
            catch { }
        }
    }

    private void DrawCheatButtons(Day day, Booth? booth)
    {
        // -- 修改金钱 --
        GUILayout.BeginHorizontal();
        GUILayout.Label("  金钱:", GUILayout.Width(50)); // 金钱
        _moneyInput = GUILayout.TextField(_moneyInput, GUILayout.Width(80));
        if (GUILayout.Button("设置", GUILayout.Width(60))) // 设置
        {
            if (int.TryParse(_moneyInput, out int val))
            {
                day.bribeMoney = val;
                ShowStatus("贿赂收入已设为 " + val); // 贿赂收入已设为
            }
        }
        GUILayout.EndHorizontal();

        // -- 设置天数时长 --
        GUILayout.BeginHorizontal();
        GUILayout.Label("  时长(分):", GUILayout.Width(70)); // 时长(分)
        if (string.IsNullOrEmpty(_dayDurationInput))
            _dayDurationInput = day.durationInMinutes.ToString("F0");
        _dayDurationInput = GUILayout.TextField(_dayDurationInput, GUILayout.Width(60));
        if (GUILayout.Button("设置", GUILayout.Width(60))) // 设置
        {
            if (double.TryParse(_dayDurationInput, out double val))
            {
                day.durationInMinutes = val;
                ShowStatus("当天时长已设为 " + val + " 分钟"); // 当天时长已设为 X 分钟
            }
        }
        GUILayout.EndHorizontal();

        // -- 减少等待队列 --
        if (GUILayout.Button("  减少等待队列人数")) // 减少等待队列人数
        {
            try
            {
                day.waitingLineLength = 0;
                ShowStatus("等待队列已清空"); // 等待队列已清空
            }
            catch (Exception e)
            {
                ShowStatus("操作失败: " + e.Message); // 操作失败
            }
        }
    }

    private void DrawTimeControls(Border? border)
    {
        if (border == null)
        {
            GUILayout.Label("  (边境未加载)"); // (边境未加载)
            return;
        }

        float curScale = (float)border.localClockTimescale;
        GUILayout.Label("  当前时间流速: " + curScale.ToString("F1") + "x"); // 当前时间流速

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("  暂停 (0x)"))  { border.localClockTimescale = 0;    ShowStatus("时间已暂停"); } // 暂停  时间已暂停
        if (GUILayout.Button("  正常 (1x)"))  { border.localClockTimescale = 1;    ShowStatus("时间恢复正常"); } // 正常  时间恢复正常
        if (GUILayout.Button("  加速 (3x)"))  { border.localClockTimescale = 3;    ShowStatus("时间 3x 加速"); } // 加速  时间 3x 加速
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("  5x"))         { border.localClockTimescale = 5;    ShowStatus("时间 5x 加速"); }
        if (GUILayout.Button("  10x"))        { border.localClockTimescale = 10;   ShowStatus("时间 10x 加速"); }
        if (GUILayout.Button("  50x"))        { border.localClockTimescale = 50;   ShowStatus("时间 50x 加速"); }
        GUILayout.EndHorizontal();
    }

    private void DrawQuickActions(Booth? booth, Border? border)
    {
        if (booth != null)
        {
            if (GUILayout.Button("  自动盖章：全部批准")) // 自动盖章：全部批准
            {
                try
                {
                    booth.debugStampAll(StampApprovalKind.APPROVED);
                    ShowStatus("已自动盖章：全部批准"); // 已自动盖章：全部批准
                }
                catch (Exception e) { ShowStatus("盖章失败: " + e.Message); } // 盖章失败
            }

            if (GUILayout.Button("  自动盖章：全部拒绝")) // 自动盖章：全部拒绝
            {
                try
                {
                    booth.debugStampAll(StampApprovalKind.DENIED);
                    ShowStatus("已自动盖章：全部拒绝"); // 已自动盖章：全部拒绝
                }
                catch (Exception e) { ShowStatus("盖章失败: " + e.Message); }
            }

            if (GUILayout.Button("  呼叫下一位旅客")) // 呼叫下一位旅客
            {
                try
                {
                    booth.acceptNextTraveler();
                    ShowStatus("已呼叫下一位旅客"); // 已呼叫下一位旅客
                }
                catch (Exception e) { ShowStatus("呼叫失败: " + e.Message); } // 呼叫失败
            }

            if (GUILayout.Button("  打开/关闭检查界面")) // 打开/关闭检查界面
            {
                try
                {
                    var inspectUi = booth.inspectUi;
                    if (inspectUi != null)
                    {
                        bool isOpen = inspectUi.get_open();
                        inspectUi.set_open(!isOpen);
                        ShowStatus(isOpen ? "检查界面已关闭" : "检查界面已打开"); // 检查界面已关闭/已打开
                    }
                }
                catch (Exception e) { ShowStatus("操作失败: " + e.Message); }
            }
        }

        if (border != null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("  启用狙击")) // 启用狙击
            {
                try { border.set_snipingEnabled(true); ShowStatus("狙击已启用"); } // 狙击已启用
                catch (Exception e) { ShowStatus("操作失败: " + e.Message); }
            }
            if (GUILayout.Button("  禁用狙击")) // 禁用狙击
            {
                try { border.set_snipingEnabled(false); ShowStatus("狙击已禁用"); } // 狙击已禁用
                catch (Exception e) { ShowStatus("操作失败: " + e.Message); }
            }
            GUILayout.EndHorizontal();
        }
    }
}
