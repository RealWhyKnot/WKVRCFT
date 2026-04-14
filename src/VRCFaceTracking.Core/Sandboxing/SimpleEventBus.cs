using VRCFaceTracking.Core.Sandboxing.IPC;

namespace VRCFaceTracking.Core.Sandboxing;

public class SimpleEventBus
{
    private Queue<IpcPacket> _packetQueue = new();
    public bool BypassQueue = false;
    public int Count => _packetQueue.Count;

    public void Push<T>(T packet) where T : IpcPacket
    {
        _packetQueue.Enqueue(packet);
    }

    public T Pop<T>() where T : IpcPacket
    {
        return (T)_packetQueue.Dequeue();
    }

    public T Peek<T>() where T : IpcPacket
    {
        return (T)_packetQueue.Peek();
    }
}
