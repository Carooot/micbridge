using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace MicBridge
{
    internal sealed class CaptureDemand
    {
        public bool Requested {get; private set;}
        public bool Paused {get; private set;}
        public bool Active {get {return Requested && !Paused;}}
        public void Request(bool active) {Requested=active;}
        public void Pause(bool pause) {Paused=pause;}
        public void Reset() {Requested=false;}
    }
    internal sealed class Preferences
    {
        public string Device="";
        public bool Background=true;
        public string Encode() {return "background="+(Background?"1":"0")+"\ndevice="+Convert.ToBase64String(Encoding.UTF8.GetBytes(Device))+"\n";}
        public static Preferences Decode(string text)
        {
            var p=new Preferences();if(text.Length>4096) return p;
            foreach(string row in text.Split('\n'))
            {
                int eq=row.IndexOf('=');if(eq<0) continue;string name=row.Substring(0,eq),value=row.Substring(eq+1).Trim();
                if(name=="background") p.Background=value!="0";
                if(name=="device") try {p.Device=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(value));}catch(FormatException) {}catch(DecoderFallbackException) {}
            }
            return p;
        }
    }
    internal static class Wire
    {
        public static readonly byte[] Hello=Encoding.ASCII.GetBytes("MICBRIDGE3\n");
        public static readonly byte[] Start={254,255}, Stop={0,0}, Ping={255,255};
        public static byte[] Audio(byte[] pcm)
        {
            if(pcm.Length==0 || pcm.Length>1920 || pcm.Length%2!=0) throw new InvalidDataException("无效的音频帧。");
            var frame=new byte[pcm.Length+2];frame[0]=(byte)pcm.Length;frame[1]=(byte)(pcm.Length>>8);
            Buffer.BlockCopy(pcm,0,frame,2,pcm.Length);return frame;
        }
        public static string ReadLine(Stream stream)
        {
            using(var bytes=new MemoryStream())
            {
                while(bytes.Length<256)
                {
                    int b=stream.ReadByte();if(b<0) throw new IOException("Mac 已断开连接。");
                    if(b==10) return new UTF8Encoding(false,true).GetString(bytes.ToArray());
                    bytes.WriteByte((byte)b);
                }
            }
            throw new IOException("Mac 返回了过长的响应。");
        }
        public static int Peak(byte[] bytes)
        {
            if(bytes.Length%2!=0) throw new InvalidDataException("麦克风数据不完整。");
            int peak=0;for(int i=0;i<bytes.Length;i+=2) peak=Math.Max(peak,Math.Abs((int)(short)(bytes[i]|bytes[i+1]<<8)));
            return peak;
        }
    }
    // Ending capture discards queued speech before STOP; the worker can hold at most one extra 20 ms frame.
    internal sealed class AudioOutbox
    {
        readonly object gate=new object();readonly Queue<byte[]> frames=new Queue<byte[]>();
        bool capturing,closed;
        public void Begin()
        {lock(gate) {if(closed) throw new IOException("连接已关闭。");capturing=true;frames.Clear();frames.Enqueue(Wire.Start);Monitor.Pulse(gate);}}
        public void End()
        {lock(gate) {if(closed) return;capturing=false;frames.Clear();frames.Enqueue(Wire.Stop);Monitor.Pulse(gate);}}
        public void Add(byte[] pcm)
        {
            var frame=Wire.Audio(pcm);
            lock(gate) {if(closed || !capturing) return;if(frames.Count>=10) throw new IOException("网络跟不上，已停止传输。请重新连接。");frames.Enqueue(frame);Monitor.Pulse(gate);}
        }
        public byte[] Take(int milliseconds=1000)
        {lock(gate) {if(!closed && frames.Count==0) Monitor.Wait(gate,milliseconds);if(closed) return null;return frames.Count==0?Wire.Ping:frames.Dequeue();}}
        public void Close() {lock(gate) {closed=true;capturing=false;frames.Clear();Monitor.PulseAll(gate);}}
    }
}
