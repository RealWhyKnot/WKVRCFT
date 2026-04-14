using System.Collections.Concurrent;
using VRCFaceTracking.Core.OSC;

namespace VRCFaceTracking.Core.Services;

public static class ParameterSenderService
{
    private static readonly ConcurrentQueue<OscMessage> _sendQueue = new();
    private static OscSendService? _oscSendService;

    public static bool AllParametersRelevantStatic { get; set; } = true;

    public static void Initialize(OscSendService oscSendService)
    {
        _oscSendService = oscSendService;
    }

    public static void Enqueue(OscMessage message)
    {
        _sendQueue.Enqueue(message);
    }

    public static void Clear()
    {
        while (_sendQueue.TryDequeue(out _)) { }
    }

    public static async Task SendLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await UnifiedTracking.UpdateData(ct);

                var messages = new List<OscMessageMeta>();
                while (_sendQueue.TryDequeue(out var msg))
                {
                    messages.Add(msg._meta);
                }

                if (messages.Count > 0 && _oscSendService != null)
                {
                    _oscSendService.SendBundle(messages.ToArray());
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                LogService.AddEntry(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = "Error",
                    Source = "ParameterSender",
                    Message = "Error in send loop: " + ex.Message
                });
            }

            await Task.Delay(10, ct);
        }
    }
}
