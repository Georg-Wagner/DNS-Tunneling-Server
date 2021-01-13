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
    class DNSServer
    {
        public static void Start()

        {
            Thread.CurrentThread.IsBackground = true;

            UdpClient listener = new UdpClient(53);
            Console.WriteLine("Server Started...");

            while (true)

            {
                try

                {
                    //Empfange alle Eingehende Verbindungen auf 53 Port
                    IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, 53);
                    byte[] ReciveBytes = listener.Receive(ref groupEP);
                    string domain = Utils.getDomain(ReciveBytes);

                    string recivedMessage;
                    string messageID;
                    string streamID;
                    int messageNum;
                    int messagesCount;

                    Utils.PharseDomain(domain, out streamID, out messageID, out messageNum, out messagesCount, out recivedMessage);
                    if (!string.IsNullOrEmpty(recivedMessage))
                    {
                        string[] tmpOut = new string[] { };
                        string[] tmpin = new string[] { };
                        //Wenn das der erste Teil von message ist, wird neue messageID zu ClientRequestHexQueue hinzugefügt
                        if (!Stream.ClientRequestHexQueue.TryGetValue(messageID, out tmpOut))
                        {
                            tmpOut = new string[messagesCount];
                            Stream.ClientRequestHexQueue.TryAdd(messageID, tmpOut);

                        }
                        Stream.AddToClientRequestHexQueue(streamID, messageID, messageNum, messagesCount, recivedMessage);

                    }
                    string strmessage = "null";
                    string responseData = "null";
                    //Es wird der erster Antwort von dem Web-Server aus der Schlange genommen und für versand als DNS TXT Record vorbereitet
                    if (!Stream.ClientResponseDataQueue.IsEmpty)
                    {
                        string msgIdTmp = Stream.ClientResponseDataQueue.First().Key;
                        string strmIdTmp = "";
                        Stream.messageID_streamID.TryGetValue(msgIdTmp, out strmIdTmp);
                        string[] splittedResponseData = Stream.ClientResponseDataQueue.First().Value;
                        for (int i = 0; i < splittedResponseData.Length; i++)
                        {
                            if (!String.IsNullOrEmpty(splittedResponseData[i]))
                            {
                                responseData = splittedResponseData[i];

                                strmessage = $"[{msgIdTmp}-{strmIdTmp}.{i}-{splittedResponseData.Length}]{responseData}";
                                splittedResponseData[i] = null;
                                goto buildMessage;
                            }
                            if (i == splittedResponseData.Length - 1)
                            {
                                while (Stream.ClientResponseDataQueue.TryRemove(msgIdTmp, out _) == false) ;
                            }

                        }

                    }
                buildMessage:

                    byte[] SendBytes = ReciveBytes;
                    //offsets wurden mithilfe von Wireschark herausgefunden
                    byte[] flag1 = { 0x81, 0x80 };

                    byte[] flag2 = { 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };

                    byte[] txtRecordOffset = { 0xC0, 0x0C, 0x00, 0x10, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, Convert.ToByte(strmessage.Length + 1), Convert.ToByte(strmessage.Length) };

                    flag1.CopyTo(SendBytes, 2);

                    flag2.CopyTo(SendBytes, 6);

                    byte[] dot = { 0x02 };



                    byte[] replyMessage = new byte[strmessage.Length];

                    for (int b = 0; b < strmessage.Length; b++)

                    {

                        replyMessage[b] = Convert.ToByte(strmessage[b]);

                    }

                    if (SendBytes[SendBytes.Length - 1] == 0x00)
                    {
                        SendBytes = SendBytes.ToList().GetRange(0, SendBytes.Length - 11).ToArray();
                    }
                    SendBytes = SendBytes.Concat(txtRecordOffset).Concat(replyMessage).ToArray();

                    //Antwort an Tunneling-Client
                    listener.Send(SendBytes, SendBytes.Length, groupEP);

                }

                catch (Exception e)
                {

                }

            }

        }
    }
}
