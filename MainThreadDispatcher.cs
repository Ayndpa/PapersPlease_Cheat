#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PapersPlease_Cheat;

/// <summary>
/// 轻量级主线程调度器，用于从 ImGui 渲染线程安全地调用 Unity API。
/// 在 Plugin.Load() 中通过 ClassInjector 注册并创建实例。
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new();
    private static MainThreadDispatcher? _instance;

    public MainThreadDispatcher(IntPtr ptr) : base(ptr) { }

    private void Awake()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        _instance = null;
        lock (_executionQueue)
        {
            _executionQueue.Clear();
        }
    }

    private void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                try { _executionQueue.Dequeue().Invoke(); }
                catch (Exception e) { Debug.LogError(e.ToString()); }
            }
        }
    }

    /// <summary>
    /// 将 Action 派发到 Unity 主线程执行。
    /// </summary>
    public static void Enqueue(Action action)
    {
        if (_instance == null) return;
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
}
