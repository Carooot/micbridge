using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MicBridge
{
    internal sealed class Recorder : IDisposable
    {
        internal sealed class Device
        {public uint Id;public string Name;public override string ToString() {return Name;}}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct CAPS
        {public ushort manufacturer,product;public uint version;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string name;public uint formats;public ushort channels,reserved;}
        [DllImport("winmm.dll")] static extern uint waveInGetNumDevs();
        [DllImport("winmm.dll",CharSet=CharSet.Unicode,EntryPoint="waveInGetDevCapsW")] static extern uint waveInGetDevCaps(UIntPtr id,out CAPS caps,uint size);
        public static List<Device> Devices()
        {
            var devices=new List<Device> {new Device {Id=uint.MaxValue,Name="跟随 Windows 默认麦克风"}};
            for(uint i=0;i<waveInGetNumDevs();i++)
            {CAPS caps;if(waveInGetDevCaps(new UIntPtr(i),out caps,(uint)Marshal.SizeOf(typeof(CAPS)))==0) devices.Add(new Device {Id=i,Name=caps.name});}
            return devices;
        }
        [StructLayout(LayoutKind.Sequential, Pack=2)] struct FORMAT
        { public ushort tag, channels; public uint rate, average; public ushort align, bits, extra; }
        [StructLayout(LayoutKind.Sequential)] struct HEADER
        { public IntPtr data; public uint length, recorded; public UIntPtr user; public uint flags, loops; public IntPtr next; public UIntPtr reserved; }
        [DllImport("winmm.dll")] static extern uint waveInOpen(out IntPtr handle, uint device, ref FORMAT format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern uint waveInPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveInAddBuffer(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveInStart(IntPtr handle);
        [DllImport("winmm.dll")] static extern uint waveInReset(IntPtr handle);
        [DllImport("winmm.dll")] static extern uint waveInClose(IntPtr handle);
        readonly Dictionary<IntPtr,IntPtr> buffers = new Dictionary<IntPtr,IntPtr>();
        readonly uint headerSize = (uint)Marshal.SizeOf(typeof(HEADER));
        IntPtr handle;
        public Recorder(IntPtr window,uint device)
        {
            var format = new FORMAT { tag=1, channels=1, rate=48000, average=96000, align=2, bits=16 };
            Check(waveInOpen(out handle, device, ref format, window, IntPtr.Zero, 0x10000));
            try
            {
                for(int i=0; i<8; i++)
                {
                    IntPtr data = Marshal.AllocHGlobal(1920), header = Marshal.AllocHGlobal((int)headerSize);
                    Marshal.StructureToPtr(new HEADER { data=data, length=1920 }, header, false);
                    buffers.Add(header,data);
                    Check(waveInPrepareHeader(handle,header,headerSize));
                    Check(waveInAddBuffer(handle,header,headerSize));
                }
                Check(waveInStart(handle));
            }
            catch { Dispose(); throw; }
        }
        public byte[] Accept(IntPtr device, IntPtr pointer)
        {
            if(device != handle || handle == IntPtr.Zero || !buffers.ContainsKey(pointer)) return null;
            var h = (HEADER)Marshal.PtrToStructure(pointer,typeof(HEADER));
            if((h.flags & 1) == 0) return null;
            if(h.recorded > h.length || h.recorded % 2 != 0) throw new InvalidOperationException("麦克风返回了无效的数据。");
            var bytes = new byte[h.recorded]; Marshal.Copy(h.data,bytes,0,bytes.Length);
            Check(waveInAddBuffer(handle,pointer,headerSize));
            return bytes;
        }
        static void Check(uint result)
        {
            if(result != 0) throw new InvalidOperationException("打开或读取麦克风失败（"+result+"）。请检查 Windows 默认输入设备，并允许桌面应用访问麦克风。");
        }
        public void Dispose()
        {
            if(handle == IntPtr.Zero) return;
            waveInReset(handle);
            foreach(var p in buffers)
            { waveInUnprepareHeader(handle,p.Key,headerSize); Marshal.FreeHGlobal(p.Key); Marshal.FreeHGlobal(p.Value); }
            buffers.Clear(); waveInClose(handle); handle = IntPtr.Zero;
        }
    }
}
