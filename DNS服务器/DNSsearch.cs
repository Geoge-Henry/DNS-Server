using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace DNS服务器
{
    public enum QueryType
    {
        A = 1,
        NS = 2,
        CNAME = 5,
        PTR = 12,
        HINFO = 13,
        MX = 15,
        AXFR = 252,
        ANY = 255
    }
    public class Query
    {
        public string QueryName { get; set; }
        public QueryType QueryType { get; set; }
        public Int16 QueryClass { get; set; }

        public Query()
        {
        }

        public Query(Func<int, byte[]> read)
        {

            var name = new StringBuilder();
            var length = read(1)[0];
            //以下while过程为读取查询问题的查询名
            while (length != 0)
            {
                for (var i = 0; i < length; i++)
                {
                    name.Append((char)read(1)[0]);
                }

                length = read(1)[0];
                if (length != 0)
                    name.Append(".");
            }
            QueryName = name.ToString();

            QueryType = (QueryType)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(read(2), 0));//读取查询类型
            QueryClass = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(read(2), 0));//读取查询类

        }
        public virtual byte[] ToBytes()
        {
            var list = new List<byte>();


            var a = QueryName.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < a.Length; i++)
            {
                list.Add((byte)a[i].Length);
                for (var j = 0; j < a[i].Length; j++)
                    list.Add((byte)a[i][j]);
            }
            list.Add(0);


            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int16)QueryType)));
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(QueryClass)));

            return list.ToArray();

        }
    }
    public class ResouceRecord : Query
    {
        public Int16 Point { get; set; }
        public Int32 TTL { get; set; }
        public byte[] Datas { get; set; }

        public ResouceRecord()
            : base()
        {
            var bytes = new byte[] { 0xc0, 0x0c };
            //一般响应报文中，资源部分的域名都是指针C00C(1100000000001100)，刚好指向请求部分的域名
            Point = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(bytes, 0));//获得域名
        }

        public ResouceRecord(Func<int, byte[]> read)
            : base()
        {

            TTL = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(read(4), 0));//获得生存时间
            var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(read(2), 0));//获得资源数据长度
            Datas = read(length);//读取资源数据

        }
        public override byte[] ToBytes()
        {
            var list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Point)));
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int16)QueryType)));
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(QueryClass)));
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(TTL)));
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int16)Datas.Length)));
            list.AddRange(Datas);

            return list.ToArray();
        }
    }
    public class DNS
    {
        public Int16 标志 { get; set; }
        public int QR { get; set; }     //0表示查询报文 1表示响应报文
        public int opcode { get; set; } //0表示标准查询,1表示反向查询,2表示服务器状态请求
        public int AA { get; set; }  //授权回答
        public int TC { get; set; } //表示可截断的
        public int RD { get; set; } //表示期望递归 
        public int RA { get; set; } //表示可用递归
        public int rcode { get; set; } //0表示没有错误,3表示名字错误

        public List<Query> Querys { get; set; }  //问题数
        public List<ResouceRecord> ResouceRecords { get; set; }  //资源记录数
        public Int16 授权资源记录数 { get; set; }
        public Int16 额外资源记录数 { get; set; }


        public byte[] ToBytes()
        {
            var list = new List<byte>();
            var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(标志));//将短值由主机字节顺序转换为网络字节顺序。
            list.AddRange(bytes);
            var b = new byte();
            b = b.SetBits(QR, 0, 1)
                .SetBits(opcode, 1, 4)
                .SetBits(AA, 5, 1)
                .SetBits(TC, 6, 1);

            b = b.SetBits(RD, 7, 1);
            list.Add(b);
            b = new byte();
            b = b.SetBits(RA, 0, 1)
                .SetBits(0, 1, 3)
                .SetBits(rcode, 4, 4);
            list.Add(b);

            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int16)Querys.Count)));//addrange表示添加到末尾
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int16)ResouceRecords.Count)));
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(授权资源记录数)));
            list.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(额外资源记录数)));

            foreach (var q in Querys)
            {
                list.AddRange(q.ToBytes());
            }
            foreach (var r in ResouceRecords)
            {
                list.AddRange(r.ToBytes());
            }

            return list.ToArray();

        }

        private int index;
        private byte[] package;
        private byte ReadByte()
        {
            return package[index++];
        }
        private byte[] ReadBytes(int count = 1)
        {
            var bytes = new byte[count];
            for (var i = 0; i < count; i++)
                bytes[i] = ReadByte();
            return bytes;
        }




        public DNS(byte[] buffer, int length)
        {
            package = new byte[length];
            for (var i = 0; i < length; i++)
                package[i] = buffer[i];

            标志 = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(ReadBytes(2), 0));
            //将短值由网络字节顺序转换为主机字节顺序,0表示开始位置。

            //读取前两个字节(DNS报文的标识部分)
            var b1 = ReadByte();
            var b2 = ReadByte();

            QR = b1.GetBits(0, 1);//0为开始位置，1为长度
            opcode = b1.GetBits(1, 4);
            AA = b1.GetBits(5, 1);
            TC = b1.GetBits(6, 1);
            RD = b1.GetBits(7, 1);

            RA = b2.GetBits(0, 1);
            rcode = b2.GetBits(4, 4);

            var queryCount = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(ReadBytes(2), 0));
            var rrCount = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(ReadBytes(2), 0));

            授权资源记录数 = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(ReadBytes(2), 0));
            额外资源记录数 = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(ReadBytes(2), 0));

            Querys = new List<Query>();
            for (var i = 0; i < queryCount; i++)
            {
                Querys.Add(new Query(ReadBytes));
            }

            for (var i = 0; i < rrCount; i++)
            {
                ResouceRecords.Add(new ResouceRecord(ReadBytes));
            }

        }


    }



    public static class Extension
    {
        public static int GetBits(this byte b, int start, int length)
        {
            var temp = b >> (8 - start - length);
            var mask = 0;
            for (var i = 0; i < length; i++)
            {
                mask = (mask << 1) + 1;
            }

            return temp & mask;

        }
        public static byte SetBits(this byte b, int data, int start, int length)
        {
            var temp = b;

            var mask = 0xFF;
            for (var i = 0; i < length; i++)
            {
                mask = mask - (0x01 << (7 - (start + i)));
            }
            temp = (byte)(temp & mask);

            mask = ((byte)data).GetBits(8 - length, length);
            mask = mask << (7 - start);

            return (byte)(temp | mask);


        }
        public static string ToBinaryString(this byte b)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < 8; i++)
            {
                if (i % 4 == 0)
                    sb.Append(" ");
                var bit = b.GetBits(i, 1);
                sb.Append(bit);
            }
            return sb.ToString();
        }
        public static string BinaryString(this int b)
        {
            var sb = new StringBuilder();
            for (var i = 15; i >= 0; i--)
                sb.Append((b >> i) & 0x01);
            return sb.ToString();
        }
        public static string ToHexString(this IEnumerable<byte> bytes)
        {
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(string.Format("{0:x2}({1}) ", b, (char)b));
            }
            return sb.ToString();
        }
        /// <summary>
        /// 日志文件保存到d:\donata\log文件夹,每天保存一个文件
        /// </summary>
        /// <param name="log"></param>

        public static void Log(this string log)
        {
            try
            {
                var logfie = "d:\\dns.txt";


                if (!File.Exists(logfie))
                {

                    File.Create(logfie).Close();
                }
                using (var sw = File.AppendText(logfie))
                {
                    sw.WriteLine(log);
                }
            }
            catch
            {

            }
        }
    }
}
