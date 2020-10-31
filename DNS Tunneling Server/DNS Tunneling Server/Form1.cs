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
        public static void AddToClientRequestHexQueue(string messageID, int messageNum, int messagesCount, string recivedMessage)
        {
            string[] tmpOut = new string[] { };
            string[] tmpin = new string[] { };
            ClientRequestHexQueue.TryGetValue(messageID, out tmpOut);
            tmpin = new string[tmpOut.Length];
            tmpOut.CopyTo(tmpin, 0);
            tmpin[messageNum] = recivedMessage;
            ClientRequestHexQueue.TryUpdate(messageID, tmpin, tmpOut);
            if (messagesCount - 1 == messageNum)
            {
                var thrd = new Thread(async () =>
                {

                a1:
                    try
                    {
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

                    host = System.Text.Encoding.ASCII.GetString(HexStringToByteArray(hostHex)).Split(':')[0];

                    Int32.TryParse(System.Text.Encoding.ASCII.GetString(HexStringToByteArray(tmpOut[tmpOut.Length - 1])).Split(':')[1], out port);
                    string dataHex = "";
                    for (int i = 0; i < tmpOut.Length - 1; i++)
                    {
                        dataHex += tmpOut[i];
                    }
                    byte[] dataBytesToSend = HexStringToByteArray(dataHex);
                    byte[] dataBytesRecived = new byte[] { };
                    TcpClient tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(host, port);
                    NetworkStream upstream = tcpClient.GetStream();
                    // upstream.ReadTimeout = 3000;

                    // 独立线程，完成自己的任务后消失
                    await upstream.WriteAsync(dataBytesToSend, 0, dataBytesToSend.Length);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        upstream.CopyTo(ms);
                        dataBytesRecived = ms.ToArray();
                    }
                    string base64AnswerData = Convert.ToBase64String(dataBytesRecived);
                    string[] splittedbase64AnswerData = Split(base64AnswerData, 230).ToArray();
                    ClientResponseDataQueue.TryAdd(messageID, splittedbase64AnswerData);

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
        //public UdpClient listener;
        public static void udpserver()

        {
            Thread.CurrentThread.IsBackground = true;
            CheckForIllegalCrossThreadCalls = false;
            UdpClient listener = new UdpClient(53);


            while (true)

            {



                try

                {

                    IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, 53);
                    byte[] ReciveBytes = listener.Receive(ref groupEP);
                    string domain = getDomain(ReciveBytes);

                    //byte[] byteip = SubDomToIPAddr(domain);
                    string recivedMessage;
                    string messageID;
                    int messageNum;
                    int messagesCount;
                    PharseDomain(domain, out messageID, out messageNum, out messagesCount, out recivedMessage);
                    if (!string.IsNullOrEmpty(recivedMessage))
                    {
                        string[] tmpOut = new string[] { };
                        string[] tmpin = new string[] { };
                        if (!ClientRequestHexQueue.TryGetValue(messageID, out tmpOut))
                        {
                            tmpOut = new string[messagesCount];
                            ClientRequestHexQueue.TryAdd(messageID, tmpOut);

                        }
                        AddToClientRequestHexQueue(messageID, messageNum, messagesCount, recivedMessage);

                    }
                    string strmessage1 = "null";
                    string responseData = "null";
                    if (!ClientResponseDataQueue.IsEmpty)
                    {
                        string msgIdTmp = ClientResponseDataQueue.First().Key;
                        string[] splittedResponseData = ClientResponseDataQueue.First().Value;
                        for (int i = 0; i < splittedResponseData.Length; i++)
                        {
                            if (!String.IsNullOrEmpty(splittedResponseData[i]))
                            {
                                responseData = splittedResponseData[i];

                                strmessage1 = $"[{msgIdTmp}.{i}-{splittedResponseData.Length}]{responseData}";
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
                    //{_Form1.richTextBox2.Text}";

                    byte[] SendBytes = ReciveBytes;

                    byte[] flag1 = { 0x81, 0x80 };

                    byte[] flag2 = { 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };

                    // byte[] ttl = { 0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x04 };  //A record

                    byte[] ttl = { 0xC0, 0x0C, 0x00, 0x10, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, Convert.ToByte(strmessage1.Length + 1), Convert.ToByte(strmessage1.Length) };  //txt record

                    flag1.CopyTo(SendBytes, 2);

                    flag2.CopyTo(SendBytes, 6);

                    byte[] dot = { 0x02 };



                    byte[] replyMessage = new byte[strmessage1.Length];

                    for (int b = 0; b < strmessage1.Length; b++)

                    {

                        replyMessage[b] = Convert.ToByte(strmessage1[b]);

                    }

                    if (SendBytes[SendBytes.Length - 1] == 0x00)
                    {
                        SendBytes = SendBytes.ToList().GetRange(0, SendBytes.Length - 11).ToArray();
                    }
                    SendBytes = SendBytes.Concat(ttl).Concat(replyMessage).ToArray();
                    _Form1.richTextBox1.Text += recivedMessage + "\n";

                    listener.Send(SendBytes, SendBytes.Length, groupEP);

                }

                catch (Exception e)
                {
                    // MessageBox.Show(e.ToString());
                    //  _Form1.richTextBox1.Text = Convert.ToString(e);
                }

            }

        }


        public static string GetStringData(string domain)
        {
            return domain.Split((char)6)[0].Remove((char)4);
        }

        static string ReturnCleanASCII(string s)
        {
            s = s.Split((char)6)[0];
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if ((int)c > 127) // you probably don't want 127 either
                    continue;
                if ((int)c < 32)  // I bet you don't want control characters 
                    continue;
                if (c == ',')
                    continue;
                if (c == '"')
                    continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        static void PharseDomain(string request, out string messageID, out int messageNum, out int messagesCount, out string message)
        {
            StringBuilder sb = new StringBuilder(request.Length);
            foreach (char c in request)
            {
                if ((int)c > 127)
                { // you probably don't want 127 either
                    sb.Append('.');
                    continue;
                }

                if ((int)c < 32)
                {   // I bet you don't want control characters 
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
            request = sb.ToString().Remove(0, 1);
            string domain = $"{request.Split('.')[request.Split('.').Length - 2]}.{request.Split('.')[request.Split('.').Length - 1]}";
            messageID = request.Split('.')[request.Split('.').Length - 4];
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
                data = request.Replace($"{messageID}.{messageNum}-{messagesCount}.{domain}", "");

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
            int i = 12;
            string domain = "";

            while (!(request[i] == 0x00))
            {
                domain += (char)request[i];
                i++;
            }



            return domain;
        }
        static byte[] SubDomToIPAddr(string domain)
        {
            byte[] byteip = new byte[4];
            for (int b = 0; b < 4; b++)
            {
                //byteip[b] = Convert.ToByte(ipaddress.Split('.')[b]);
            }
            return byteip;
        }

    }

}
