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
    class Stream
    {
        public static ConcurrentDictionary<string, string[]> ClientRequestHexQueue = new ConcurrentDictionary<string, string[]>();
        public static ConcurrentDictionary<string, string[]> ClientResponseDataQueue = new ConcurrentDictionary<string, string[]>();
        public static ConcurrentDictionary<string, TcpClient> OpenStreams = new ConcurrentDictionary<string, TcpClient>();
        public static ConcurrentDictionary<string, string> messageID_streamID = new ConcurrentDictionary<string, string>();








        public static void AddToClientRequestHexQueue(string streamID, string messageID, int messageNum, int messagesCount, string recivedMessage)
        {
            string[] tmpOut = new string[] { };
            string[] tmpin = new string[] { };
            ClientRequestHexQueue.TryGetValue(messageID, out tmpOut);
            tmpin = new string[tmpOut.Length];
            tmpOut.CopyTo(tmpin, 0);
            tmpin[messageNum] = recivedMessage;
            ClientRequestHexQueue.TryUpdate(messageID, tmpin, tmpOut);
            //Wenn dieser Antwort vom Tunneling-Server der letzte Teil für dieses messageID ist....
            if (messagesCount - 1 == messageNum)
            {
                var thrd = new Thread(async () =>
                {

                a1:
                    try
                    {
                        // Es wird überprüft ob tatsächlich alle Teile angekommen sind

                        while (ClientRequestHexQueue.TryGetValue(messageID, out tmpOut) == false) ;
                        while (tmpOut.All(x => string.IsNullOrEmpty(x)))
                        {
                            while (ClientRequestHexQueue.TryGetValue(messageID, out tmpOut) == false) ;
                        }
                    }
                    catch (Exception)
                    {

                        goto a1;
                    }
                    string host = "";
                    Int32 port;
                    string hostHex = tmpOut[tmpOut.Length - 1];

                    host = Encoding.ASCII.GetString(Utils.HexStringToByteArray(hostHex)).Split(':')[0];

                    Int32.TryParse(System.Text.Encoding.ASCII.GetString(Utils.HexStringToByteArray(tmpOut[tmpOut.Length - 1])).Split(':')[1], out port);
                    string dataHex = "";
                    for (int i = 0; i < tmpOut.Length - 1; i++)
                    {
                        dataHex += tmpOut[i];
                    }
                    byte[] dataBytesToSend = Utils.HexStringToByteArray(dataHex);
                    Console.WriteLine($"Recived from Client Size: {dataBytesToSend.Length}, streamID: {streamID} messageID: {messageID}, {host}:{port}");
                    byte[] dataBytesRecived = new byte[8192];
                    TcpClient tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(host, port);
                    NetworkStream upstream = tcpClient.GetStream();
                    //Überprüfe ob der stream schon existiert
                    if (!OpenStreams.ContainsKey(streamID))
                    {
                        Console.WriteLine($"Connected to: {host}:{port} , streamID: {streamID} messageID: {messageID}");
                        OpenStreams.TryAdd(streamID, tcpClient);

                    }
                    else
                    {

                        OpenStreams.TryGetValue(streamID, out tcpClient);

                        Console.WriteLine($"Stream to: {host}:{port} opened, streamID: {streamID} messageID: {messageID}");
                        upstream = tcpClient.GetStream();

                    }


                    try
                    {
                        await upstream.WriteAsync(dataBytesToSend, 0, dataBytesToSend.Length);
                    }
                    catch (Exception)
                    {
                        OpenStreams.TryRemove(streamID, out _);
                        Console.WriteLine($"Stream to: {host}:{port} Closed, streamID: {streamID} messageID: {messageID}");
                        goto b1;
                    }

                    Console.WriteLine($"Data sent to: {host}:{port} Size: {dataBytesToSend.Length} streamID: {streamID} messageID: {messageID}");
                    Console.WriteLine($"Parsing Response from: {host}:{port} streamID: {streamID} messageID: {messageID}");

                    int countOfBytesRecived = upstream.Read(dataBytesRecived);
                    dataBytesRecived = dataBytesRecived.Take(countOfBytesRecived).ToArray();

                    Console.WriteLine($"Recived from Server Size: {dataBytesRecived.Length}, streamID: {streamID} messageID: {messageID}, {host}:{port}");
                    //Antwort von dem Web-Server wird in Base64 Konvertiert
                    string base64AnswerData = Convert.ToBase64String(dataBytesRecived);
                    //Da TXT record maximal 255 Zeichen lang sein darf, wird Antwort von dem Web-Server auf 230 Zeichen lange strings aufgeteilt. Restliche Kapazität wird für Meta Daten benutzt
                    string[] splittedbase64AnswerData = Utils.Split(base64AnswerData, 230).ToArray();
                    messageID_streamID.TryAdd(messageID, streamID);
                    ClientResponseDataQueue.TryAdd(messageID, splittedbase64AnswerData);
                    Console.WriteLine($"Added to SendQueue Size: {dataBytesRecived.Length}, streamID: {streamID} messageID: {messageID}, {host}:{port}");
                b1:;
                });
                thrd.Start();
            }
        }
    }
}
