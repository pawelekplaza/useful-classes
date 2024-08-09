namespace Utils;

using System.Reactive.Concurrency;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public interface IHighPrecisionTimer
{
    double ElapsedMilliseconds { get; }
    double ElapsedSeconds { get; }
    
    void Reset();
    void Start();
}

public partial class HighPrecisionTimer : IHighPrecisionTimer
{
    [LibraryImport("Kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryPerformanceCounter(out long lpPerformanceCount);

    [LibraryImport("Kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryPerformanceFrequency(out long lpFrequency);

    private readonly EventLoopScheduler _scheduler = new();
    private readonly long _frequency;
    private readonly Action _action;
    private readonly double _intervalMilliseconds;
    private readonly CancellationToken _cancellationToken;
    
    private long _start;

    public static IHighPrecisionTimer Create(Action action, double intervalMilliseconds, CancellationToken cancellationToken = default)
    {
        return new HighPrecisionTimer(action, intervalMilliseconds, cancellationToken);
    }

    private HighPrecisionTimer(Action action, double intervalMilliseconds, CancellationToken cancellationToken = default)
    {
        if (!QueryPerformanceFrequency(out _frequency))
        {
            throw new InvalidOperationException("High-resolution performance counter not supported.");
        }

        _action = action ?? throw new ArgumentNullException(nameof(action));
        _intervalMilliseconds = intervalMilliseconds;
        _cancellationToken = cancellationToken;

        Reset();
    }

    public void Reset()
    {
        QueryPerformanceCounter(out _start);
    }

    public double ElapsedMilliseconds
    {
        get
        {
            QueryPerformanceCounter(out long now);
            return (now - _start) * 1000.0 / _frequency;
        }
    }

    public double ElapsedSeconds
    {
        get
        {
            QueryPerformanceCounter(out long now);
            return (now - _start) * 1.0 / _frequency;
        }
    }

    public void Start()
    {
        Reset();

        Task.Run(() =>
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (ElapsedMilliseconds >= _intervalMilliseconds - 0.001)
                {
                    Reset();
                    _scheduler.Schedule(_action);
                }
            }
        }, _cancellationToken);
    }
}