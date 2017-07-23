using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace VimHelper
{
    class SocketServer
    {
        public static SemaphoreSlim ReleaseForExit = new SemaphoreSlim(0, 1);

        public static void Run()
        {
            Task.Run(async () => {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 12345));
                socket.Listen(5);        
                
                App.Log($"Listening at port {((IPEndPoint)socket.LocalEndPoint).Port}");

                // accept incoming connection(s)        
                socket.BeginAccept(AcceptCallback, socket);

                await ReleaseForExit.WaitAsync();
            }).GetAwaiter().GetResult();
        }

        private async static void AcceptCallback(IAsyncResult ar)
        {
            Socket socket = null;

            try {
                // finish accepting the connection
                Socket listenSocket = (Socket)ar.AsyncState;

                socket = listenSocket.EndAccept(ar);

                // accept another incoming connection
                listenSocket.BeginAccept(AcceptCallback, listenSocket);

                using(var stream = new NetworkStream(socket))
                using(var reader = new StreamReader(stream, Encoding.UTF8))
                while (0 == ReleaseForExit.CurrentCount) {
                    var text = await reader.ReadLineAsync();

                    var pieces = text.Split(',');
                    
                    var ws = App.Project.AdhocWorkspace();
                    var symbol = App.Project.FindSymbolAtOffset(ws, pieces[0], Int32.Parse(pieces[1]));

                    App.Log($"{symbol.AssemblyName} :: {symbol.SymbolName} :: {symbol.TypeName}");
                }
            } catch (Exception x) {
                App.Log($"Error while processing connection: {x}");
            } finally {                                
                socket?.Shutdown(SocketShutdown.Both);
                socket?.Close();                
            }
        }        
    }
}