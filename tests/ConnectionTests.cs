using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace MicBridge
{
    static class ConnectionTests
    {
        static void Expect(bool condition) {if(!condition) throw new Exception("automatic connection test failed");}
        static void Send(Stream stream,string text) {var bytes=Encoding.ASCII.GetBytes(text+"\n");stream.Write(bytes,0,bytes.Length);}
        static void Frame(Stream stream,int expected)
        {
            for(int i=0;i<10;i++) {int lo=stream.ReadByte(),hi=stream.ReadByte();Expect(lo>=0 && hi>=0);int value=lo|(hi<<8);
                if(value==65535) {Send(stream,"PONG");continue;}Expect(value==expected);return;}
            throw new Exception("expected capture control frame");
        }
        public static void Run()
        {
            var listener=new TcpListener(IPAddress.Loopback,18768);listener.Start();
            using(var changed=new AutoResetEvent(false)) using(var failed=new AutoResetEvent(false))
            {
                Connection connection=null;bool requested=false;
                try
                {
                    var server=Task.Run(()=>{using(var peer=listener.AcceptTcpClient()) {peer.ReceiveTimeout=4000;var stream=peer.GetStream();
                        Expect(Wire.ReadLine(stream)=="MICBRIDGE3");Send(stream,"OK");Send(stream,"CAPTURE 0");
                        Expect(changed.WaitOne(4000) && !requested);Send(stream,"CAPTURE 1");
                        Expect(changed.WaitOne(4000) && requested);Frame(stream,65534);Send(stream,"STARTED");Send(stream,"CAPTURE 0");
                        Expect(changed.WaitOne(4000) && !requested);Frame(stream,0);Send(stream,"STOPPED");Send(stream,"INVALID");}});
                    connection=Connection.Open(message=>failed.Set(),active=>{
                        if(active) connection.Outbox.Begin();else if(requested) connection.Outbox.End();requested=active;changed.Set();});
                    connection.Run();Expect(server.Wait(12000));Expect(failed.WaitOne(4000));
                    Expect(connection.Outbox.Take(0)==null);
                    Console.WriteLine("PASS: real TCP capture notifications, start/stop frames and invalid-response shutdown");
                }
                finally {if(connection!=null) connection.Dispose();listener.Stop();}
            }
        }
    }
}
