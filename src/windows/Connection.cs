using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MicBridge
{
    internal sealed class Connection : IDisposable
    {
        readonly TcpClient socket;readonly Action<string> failed;readonly Action<bool> requested;int closed;
        public readonly AudioOutbox Outbox=new AudioOutbox();
        Connection(TcpClient socket,Action<string> failed,Action<bool> requested) {this.socket=socket;this.failed=failed;this.requested=requested;}
        public static Connection Open(Action<string> failed,Action<bool> requested)
        {
            var socket=new TcpClient {NoDelay=true,SendTimeout=2000,ReceiveTimeout=5000,SendBufferSize=4096};
            try
            {
                var task=socket.ConnectAsync("127.0.0.1",18768);
                if(!task.Wait(4000)) throw new IOException("连接超时，请检查 UU 端口直连 18768 是否开启。");
                task.GetAwaiter().GetResult();var stream=socket.GetStream();stream.Write(Wire.Hello,0,Wire.Hello.Length);
                string reply=Wire.ReadLine(stream);
                if(reply!="OK") throw new IOException(reply.StartsWith("ERROR ")?reply.Substring(6):"两端版本不同，请一起升级。");
                return new Connection(socket,failed,requested);
            }
            catch(AggregateException e) {socket.Close();throw new IOException("连接失败，请检查 Mac 接收器和 UU 端口直连。",e.InnerException);}
            catch(SocketException e) {socket.Close();throw new IOException("无法连接 Mac，请检查 UU 端口直连 18768。",e);}
            catch {socket.Close();throw;}
        }
        public void Run() {Task.Run((Action)Write);Task.Run((Action)Read);}
        void Write()
        {
            try
            {
                var stream=socket.GetStream();var ping=Stopwatch.StartNew();byte[] frame;
                while((frame=Outbox.Take())!=null)
                {stream.Write(frame,0,frame.Length);if(ping.ElapsedMilliseconds>=1000) {stream.Write(Wire.Ping,0,2);ping.Restart();}}
            }
            catch(Exception) {Fail("连接中断，麦克风已关闭。检查 UU 后重新连接。");}
        }
        void Read()
        {
            try
            {
                var stream=socket.GetStream();
                while(Volatile.Read(ref closed)==0)
                {
                    string reply=Wire.ReadLine(stream);
                    if(reply.StartsWith("ERROR ")) {Fail(reply.Substring(6));return;}
                    if(reply=="CAPTURE 1" || reply=="CAPTURE 0") {requested(reply=="CAPTURE 1");continue;}
                    if(reply!="PONG" && reply!="STARTED" && reply!="STOPPED") throw new IOException("未知的接收端响应。");
                }
            }
            catch(Exception) {Fail("Mac 已断开或暂时无响应，麦克风已关闭。请重新连接。");}
        }
        void Fail(string message)
        {if(Interlocked.Exchange(ref closed,1)!=0) return;Outbox.Close();socket.Close();failed(message);}
        public void Dispose() {if(Interlocked.Exchange(ref closed,1)==0) {Outbox.Close();socket.Close();}}
    }
}
