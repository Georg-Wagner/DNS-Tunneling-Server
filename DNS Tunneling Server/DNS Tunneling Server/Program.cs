using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DNS_Tunneling_Server
{
    static class Program
    {
        static Thread udpthread = new Thread(DNSServer.Start);
        static void Main()
        {
           
            udpthread.Start();
            Console.ReadKey();
        }
       
      
    }
}
