using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace DNS_Tunneling_Server
{
    // done, still just to conect mit Gui (Lucas)
    class localIP
    {
        public void getNetworkInterface()
        {


            String strHostName = string.Empty;
            // Getting Ip address of local machine
            // First get the host name of local machine. 
            strHostName = Dns.GetHostName();
            Console.WriteLine("Local Machine's Host Name: " + strHostName);
            // Then using host name, get the IP address list.. 
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
            IPAddress[] addr = ipEntry.AddressList;

            for (int i = 0; i < addr.Length; i++)
            {
                Console.WriteLine("IP Address {0}: {1} ", i, addr[i].ToString());
            }

        }
    }
}
  