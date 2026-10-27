using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using BackendTray.Contracts;

namespace BackendTray.Manager;

/// <summary>
/// 每個登入工作階段只允許一個管理介面（服務本身由 SCM 保證唯一）。
/// 也負責接收「顯示視窗」與安裝程式送來的「結束」通知。
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\BackendTray.Manager";
    private const string ShowEventName = @"Local\BackendTray.Manager.Show";
    private const int AsfwAny = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private EventWaitHandle? _exitEvent;
    private RegisteredWaitHandle? _showWait;
    private RegisteredWaitHandle? _exitWait;

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
    }

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        if (createdNew)
            return new SingleInstance(mutex);

        mutex.Dispose();
        return null;
    }

    public static void ActivateExisting()
    {
        // 讓已在執行的實例可以把視窗帶到最前面，否則 Windows 只會閃爍工作列按鈕。
        AllowSetForegroundWindow(AsfwAny);
        if (EventWaitHandle.TryOpenExisting(ShowEventName, out var showEvent))
        {
            using (showEvent)
                showEvent.Set();
        }
    }

    /// <param name="onShow">另一個實例要求顯示視窗（在執行緒集區上呼叫）。</param>
    /// <param name="onExit">安裝程式要求結束（在執行緒集區上呼叫）。</param>
    public void Listen(Action onShow, Action onExit)
    {
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => onShow(), null, Timeout.Infinite, executeOnlyOnce: false);

        _exitEvent = CreateExitEvent();
        if (_exitEvent is not null)
            _exitWait = ThreadPool.RegisterWaitForSingleObject(_exitEvent, (_, _) => onExit(), null, Timeout.Infinite, executeOnlyOnce: true);
    }

    public void Dispose()
    {
        _showWait?.Unregister(null);
        _exitWait?.Unregister(null);
        _showEvent.Dispose();
        _exitEvent?.Dispose();
        _mutex.Dispose();
    }

    /// <summary>
    /// 所有工作階段共用的 Global 事件，讓不同管理員的管理介面都能收到。
    /// 非系統管理員（Debug 組態）無法建立 Global 物件，此時就不接收結束通知。
    /// </summary>
    private static EventWaitHandle? CreateExitEvent()
    {
        var security = new EventWaitHandleSecurity();
        security.AddAccessRule(new EventWaitHandleAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), EventWaitHandleRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new EventWaitHandleAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), EventWaitHandleRights.FullControl, AccessControlType.Allow));

        try
        {
            return EventWaitHandleAcl.Create(false, EventResetMode.ManualReset, ManagementConstants.ManagerExitEventName, out _, security);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException or IOException)
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
