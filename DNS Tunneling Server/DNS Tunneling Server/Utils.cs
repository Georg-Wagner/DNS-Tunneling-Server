using System;
using System.Collections.Generic;
using System.Text;

namespace DNS_Tunneling_Server
{
    class Utils
    {
        public static string GetStringData(string domain)
        {
            return domain.Split((char)6)[0].Remove((char)4);
        }
        public static IEnumerable<string> Split(string str, int maxChunkSize)
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
        public static void PharseDomain(string request, out string streamID, out string messageID, out int messageNum, out int messagesCount, out string message)
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
        public static string getDomain(byte[] request)
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
