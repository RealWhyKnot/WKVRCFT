using System.Runtime.InteropServices;

namespace VRCFaceTracking.Core.OSC;

public enum OscValueType : byte
{
    Null = 0,
    Int = 1,
    Float = 2,
    Bool = 3,
    String = 4,
    ArrayBegin = 5,
    ArrayEnd = 6,
}

[StructLayout(LayoutKind.Sequential)]
public struct OscValue
{
    public OscValueType Type;
    [MarshalAs(UnmanagedType.I4)]
    public int IntValue;
    [MarshalAs(UnmanagedType.R4)]
    public float FloatValue;
    [MarshalAs(UnmanagedType.I1)]
    public bool BoolValue;
    [MarshalAs(UnmanagedType.LPUTF8Str)]
    public string StringValue;

    public object Value
    {
        get => Type switch
        {
            OscValueType.Int => IntValue,
            OscValueType.Float => FloatValue,
            OscValueType.Bool => BoolValue,
            OscValueType.String => StringValue,
            _ => null!
        };
        set
        {
            switch (Type)
            {
                case OscValueType.Int:
                    IntValue = (int)value;
                    break;
                case OscValueType.Float:
                    FloatValue = (float)value;
                    break;
                case OscValueType.Bool:
                    BoolValue = (bool)value;
                    break;
                case OscValueType.String:
                    StringValue = (string)value;
                    break;
            }
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct OscMessageMeta
{
    [MarshalAs(UnmanagedType.LPUTF8Str)]
    public string Address;

    [MarshalAs(UnmanagedType.U4)]
    public int ValueLength;

    [MarshalAs(UnmanagedType.SysUInt)]
    public IntPtr Value;
}

public static class fti_osc
{
    private const string DllName = "fti_osc";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr parse_osc(byte[] buffer, int bufferLength, ref int byteIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int create_osc_message([MarshalAs(UnmanagedType.LPArray, SizeConst = 4096)] byte[] buf, ref OscMessageMeta osc_template);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int create_osc_bundle(
        [MarshalAs(UnmanagedType.LPArray, SizeConst = 4096)] byte[] buf,
        [MarshalAs(UnmanagedType.LPArray)] OscMessageMeta[] messages,
        int len,
        ref int messageIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_osc_message(IntPtr oscMessage);
}
