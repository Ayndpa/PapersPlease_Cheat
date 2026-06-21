using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace PapersPlease_Cheat;

[BepInDependency(DearImGuiInjection.Metadata.GUID)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log = null!;

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // 在 IL2CPP 中注册自定义 MonoBehaviour 类型
        ClassInjector.RegisterTypeInIl2Cpp<MainThreadDispatcher>();
        ClassInjector.RegisterTypeInIl2Cpp<CheatManager>();

        // 创建 GameObject 并挂载组件
        var go = new GameObject("PapersPlease_Cheat");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<MainThreadDispatcher>();
        go.AddComponent<CheatManager>();

        Log.LogInfo("CheatManager registered. Press F1 in-game to toggle menu.");
    }
}
