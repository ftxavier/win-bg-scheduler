using System.IO;
using System.IO.Pipes;

namespace WinBgScheduler.App.Services;

/// <summary>Keeps one running copy; a second launch asks the first one to show its window.</summary>
public sealed class SingleInstance : IDisposable
{
    private static readonly string PipeName = $"WinBgScheduler.Activate.{Environment.UserName}";
    private readonly CancellationTokenSource _cts = new();
    private Mutex? _mutex;

    public bool TryAcquire()
    {
        _mutex = new Mutex(true, @"Local\WinBgScheduler.SingleInstance", out var created);
        return created;
    }

    public static void SignalExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            client.WriteByte(1);
        }
        catch (Exception e) when (e is IOException or TimeoutException)
        {
            // The other instance is closing; nothing to do.
        }
    }

    public void Listen(Action onActivate)
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cts.Token);
                    server.ReadByte();
                    onActivate();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    // Broken connection; keep listening.
                }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        _mutex?.Dispose();
    }
}
