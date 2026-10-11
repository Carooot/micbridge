using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
namespace MicBridge
{
    static class CoreTests
    {
        static int checks;
        static void Assert(bool ok) {checks++;if(!ok) throw new Exception("assertion failed at "+checks);}
        static void Throws(Action action) {try {action();}catch {checks++;return;}throw new Exception("expected rejection");}
        static void Main()
        {
            var demand=new CaptureDemand();Assert(!demand.Active);
            demand.Request(true);Assert(demand.Active);demand.Request(true);Assert(demand.Active);
            demand.Pause(true);Assert(!demand.Active && demand.Requested);
            demand.Request(false);demand.Pause(false);Assert(!demand.Active);
            demand.Request(true);demand.Pause(true);demand.Reset();demand.Pause(false);Assert(!demand.Active);
            demand.Request(true);demand.Reset();Assert(!demand.Active);
            var preferences=new Preferences {Device="USB 麦克风 = 测试",Background=false};
            var restored=Preferences.Decode(preferences.Encode());Assert(restored.Device==preferences.Device && !restored.Background);
            Assert(Preferences.Decode("device=not base64\nbackground=1").Device=="");
            Assert(Preferences.Decode(new string('x',4097)).Background);
            Assert(Encoding.ASCII.GetString(Wire.Hello)=="MICBRIDGE3\n");
            Assert(Wire.ReadLine(new MemoryStream(Encoding.UTF8.GetBytes("OK\nrest")))=="OK");
            Assert(Wire.ReadLine(new MemoryStream(Encoding.UTF8.GetBytes("ERROR 设备不可用\n")))=="ERROR 设备不可用");
            Throws(()=>Wire.ReadLine(new MemoryStream(new byte[256])));
            Throws(()=>Wire.ReadLine(new MemoryStream(new byte[0])));
            Assert(Wire.Peak(new byte[]{0,0x80,0xff,0x7f})==32768);
            Throws(()=>Wire.Peak(new byte[]{1}));
            var audio=Wire.Audio(new byte[]{1,2});Assert(audio.Length==4 && audio[0]==2 && audio[1]==0 && audio[3]==2);
            Throws(()=>Wire.Audio(new byte[0]));Throws(()=>Wire.Audio(new byte[1]));Throws(()=>Wire.Audio(new byte[1922]));
            var queue=new AudioOutbox();queue.Add(new byte[]{1,2});Assert(queue.Take(0)==Wire.Ping);
            queue.Begin();Assert(queue.Take(0)==Wire.Start);queue.Add(new byte[]{1,2});Assert(queue.Take(0)[0]==2);
            queue.Add(new byte[]{1,2});queue.Add(new byte[]{3,4});queue.End();Assert(queue.Take(0)==Wire.Stop);Assert(queue.Take(0)==Wire.Ping);
            queue.Add(new byte[]{5,6});Assert(queue.Take(0)==Wire.Ping);
            queue.Begin();queue.End();queue.Begin();Assert(queue.Take(0)==Wire.Start);Assert(queue.Take(0)==Wire.Ping);
            for(int i=0;i<10;i++) queue.Add(new byte[]{1,2});Throws(()=>queue.Add(new byte[]{1,2}));
            queue.End();Assert(queue.Take(0)==Wire.Stop);
            var blocked=Task.Run(()=>queue.Take());queue.Close();Assert(blocked.Wait(1500));Assert(blocked.Result==null);
            queue.End();Throws(()=>queue.Begin());
            if(Environment.OSVersion.Platform==PlatformID.Win32NT) ConnectionTests.Run();
            Console.WriteLine("PASS: "+checks+" checks, including capture demand, saved settings, idle silence, backlog discard, bounds and worker shutdown");
        }
    }
}
