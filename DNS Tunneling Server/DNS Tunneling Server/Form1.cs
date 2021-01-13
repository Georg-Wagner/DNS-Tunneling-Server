using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DNS_Tunneling_Server
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        Thread udpthread = new Thread(udpserver);

        public static Form1 _Form1;

        private void Form1_Load(object sender, EventArgs e)
        {
            _Form1 = this;
            CheckForIllegalCrossThreadCalls = false;
            udpthread.Start();

        }

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

                    host = Encoding.ASCII.GetString(HexStringToByteArray(hostHex)).Split(':')[0];

                    Int32.TryParse(System.Text.Encoding.ASCII.GetString(HexStringToByteArray(tmpOut[tmpOut.Length - 1])).Split(':')[1], out port);
                    string dataHex = "";
                    for (int i = 0; i < tmpOut.Length - 1; i++)
                    {
                        dataHex += tmpOut[i];
                    }
                    byte[] dataBytesToSend = HexStringToByteArray(dataHex);
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
                        OpenStreams.TryRemove(streamID, out _ );
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
                    string[] splittedbase64AnswerData = Split(base64AnswerData, 230).ToArray();
                    messageID_streamID.TryAdd(messageID, streamID);
                    ClientResponseDataQueue.TryAdd(messageID, splittedbase64AnswerData);
                    Console.WriteLine($"Added to SendQueue Size: {dataBytesRecived.Length}, streamID: {streamID} messageID: {messageID}, {host}:{port}");
                b1:;
                });
                thrd.Start();
            }
        }
        static IEnumerable<string> Split(string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }
        public static byte[] HexStringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
     
        public static void udpserver()

        {
            Thread.CurrentThread.IsBackground = true;
            CheckForIllegalCrossThreadCalls = false;
            UdpClient listener = new UdpClient(53);


            while (true)

            {
                try

                {
                    //Empfange alle Eingehende Verbindungen auf 53 Port
                    IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, 53);
                    byte[] ReciveBytes = listener.Receive(ref groupEP);
                    string domain = getDomain(ReciveBytes);

                    string recivedMessage;
                    string messageID;
                    string streamID;
                    int messageNum;
                    int messagesCount;
                    
                    PharseDomain(domain, out streamID, out messageID, out messageNum, out messagesCount, out recivedMessage);
                    if (!string.IsNullOrEmpty(recivedMessage))
                    {
                        string[] tmpOut = new string[] { };
                        string[] tmpin = new string[] { };
                        //Wenn das der erste Teil von message ist, wird neue messageID zu ClientRequestHexQueue hinzugefügt
                        if (!ClientRequestHexQueue.TryGetValue(messageID, out tmpOut))
                        {
                            tmpOut = new string[messagesCount];
                            ClientRequestHexQueue.TryAdd(messageID, tmpOut);

                        }
                        AddToClientRequestHexQueue(streamID, messageID, messageNum, messagesCount, recivedMessage);

                    }
                    string strmessage = "null";
                    string responseData = "null";
                    //Es wird der erster Antwort von dem Web-Server aus der Schlange genommen und für versand als DNS TXT Record vorbereitet
                    if (!ClientResponseDataQueue.IsEmpty)
                    {
                        string msgIdTmp = ClientResponseDataQueue.First().Key;
                        string strmIdTmp = "";
                        messageID_streamID.TryGetValue(msgIdTmp, out strmIdTmp);
                        string[] splittedResponseData = ClientResponseDataQueue.First().Value;
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
                                while (ClientResponseDataQueue.TryRemove(msgIdTmp, out _) == false) ;
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


        public static string GetStringData(string domain)
        {
            return domain.Split((char)6)[0].Remove((char)4);
        }



        static void PharseDomain(string request, out string streamID, out string messageID, out int messageNum, out int messagesCount, out string message)
        {
            StringBuilder sb = new StringBuilder(request.Length);
            // Die Subdomains im DNS Protokoll werden nicht mit Punkte sondern mit unterschiedliche bytes geteilt die als Metadata dienen. 
            // Da es für unsere zwecke nur störend ist, werden sie alle auf Punkt geändert.
            foreach (char c in request)
            {
                if ((int)c > 127)
                { 
                    sb.Append('.');
                    continue;
                }

                if ((int)c < 32)
                {  
                    sb.Append('.');
                    continue;
                }
                if (c == ',')
                {
                    sb.Append('.');
                    continue;
                }
                if (c == '"')
                {
                    continue;
                }

                sb.Append(c);
            }
            request = sb.ToString();
            string domain = $"{request.Split('.')[request.Split('.').Length - 2]}.{request.Split('.')[request.Split('.').Length - 1]}";
            if (request.Split('.')[request.Split('.').Length - 4].Contains('-'))
            {
                messageID = request.Split('.')[request.Split('.').Length - 4].Split('-')[0];

                streamID = request.Split('.')[request.Split('.').Length - 4].Split('-')[1];
            }
            else
            {
                messageID = request.Split('.')[request.Split('.').Length - 4];

                streamID = "";
            }
         
            if (request.Split('.')[request.Split('.').Length - 3].Contains('-'))
            {
                messageNum = int.Parse(request.Split('.')[request.Split('.').Length - 3].Split('-')[0]);
            }
            else
            {
                messageNum = int.Parse(request.Split('.')[request.Split('.').Length - 3]);
            }
            try
            {
                messagesCount = int.Parse(request.Split('.')[request.Split('.').Length - 3].Split('-')[1]);
            }
            catch (Exception)
            {

                messagesCount = 0;
            }
            string data = "";
            message = null;
            if (messagesCount != 0)
            {
                data = request.Replace($"{messageID}-{streamID}.{messageNum}-{messagesCount}.{domain}", "");

                sb = new StringBuilder(data.Length);
                foreach (char c in data)
                {
                    if ((int)c < 48)
                    {
                        continue;
                    }
                    if ((int)c > 58 && (int)c < 64)
                    {
                        continue;
                    }
                    if ((int)c > 91 && (int)c < 96)
                    {
                        continue;
                    }
                    if ((int)c > 122)
                    {
                        continue;
                    }
                    sb.Append(c);
                }
                message = sb.ToString();
            }



        }
        static string getDomain(byte[] request)
        {
            int i = 13;
            string domain = "";

            while (!(request[i] == 0x00))
            {
                domain += (char)request[i];
                i++;
            }



            return domain;
        }

    }

}
