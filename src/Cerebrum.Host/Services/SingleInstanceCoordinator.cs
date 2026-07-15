using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Cerebrum.Host.Services;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;

    private SingleInstanceCoordinator(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public bool IsPrimary => _ownsMutex;

    public static SingleInstanceCoordinator Acquire()
    {
        var identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var identityHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
        var name = $"Local\\Cerebrum.Host.{Process.GetCurrentProcess().SessionId}.{identityHash}";
        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        return new(mutex, createdNew);
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The process is already exiting and no longer owns the object.
            }
        }

        _mutex.Dispose();
    }
}
