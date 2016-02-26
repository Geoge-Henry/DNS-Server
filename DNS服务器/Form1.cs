using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Controls;
using System.IO;
//using Tools;

//运行程序,把网络协议中的DNS改为127.0.0.1,如果ping www.csdn.net,如果给出的地址是127.0.0.1,说明有效果
//目前只处理 A 类型的请求,其他一律转发给公共DNS服务器.
namespace DNS服务器
{
    public partial class Form1 : Form
    {
        Boolean show = false;
        private Socket server;
        private TaskFactory tf = new TaskFactory();//实例化任务线程tf
        //   private Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();        //key为string型，value为byte[]型


        public Form1()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.Fixed3D;

            server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //使用地址族ipv4、套接字类型数据报和UDP协议初始化 Socket 类的新实例
            //(其中，addressFamily 参数指定 Socket 使用的寻址方案，socketType 参数指定 Socket 的类型，
            //protocolType 参数指定 Socket 使用的协议。)
            server.Bind(new IPEndPoint(IPAddress.Any, 53));  //使 Socket 与一个本地终结点53号端口相关联
            //var DNSServer = new IPEndPoint(IPAddress.Parse("202.106.0.20"), 53);
            var DNSServer = new IPEndPoint(IPAddress.Parse("114.114.114.114"), 53);
            //公共DNS服务器IP

            tf.StartNew(() => //开启一个新任务(线程)并执行
            {
                while (true)
                {
                    try
                    {
                        var client = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                        //指定主机上一个可用ip和0端口，侦听所有网络接口的客户端活动
                        var buff = new byte[512];
                        int read = server.ReceiveFrom(buff, ref client);
                        //一直等待从绑定的 Socket 套接字接收数据，将数据存入接收缓冲区,并返回成功读取的字节数
                        tf.StartNew(
                           () =>
                           {
                               var dns = new DNS(buff, read);

                               //只处理A 类请求  
                               if (dns.QR == 0 && dns.opcode == 0 && dns.Querys.Count == 1 && dns.Querys[0].QueryType == QueryType.A)
                               {
                                   var queryName = dns.Querys[0].QueryName;//要查询的域名
                                   if (queryName == "MVCDemo.com" || queryName == "mymvclogindemo.top") //重定向csdn
                                   {
                                       //   var subname = queryName.Substring(0, queryName.Length - ".donata.cn".Length);
                                       var ip = new byte[4] { 127, 0, 0, 1 }; //返回IP:127.0.0.1
                                       if(queryName == "MVCDemo.com")
                                           ip = new byte[4] { 127, 0, 0, 2 }; 
                                       //DisplayString(string.Format("{0}==>{1}", queryName, new IPAddress(ip)));

                                       dns.QR = 1;
                                       dns.RA = 1;
                                       dns.RD = 1;
                                       dns.ResouceRecords = new List<ResouceRecord>{
                                            new ResouceRecord{
                                                 Datas=ip,//要返回的资源数据
                                                 TTL=100,
                                                 QueryClass=1,//通常为1，指Internet数据。
                                                 QueryType=QueryType.A//A标识ipv4地址
                                            }
                                            };
                                       server.SendTo(dns.ToBytes(), client);
                                       return;
                                   }
                               }


                               tf.StartNew(
                                   () =>
                                   {

                                       try
                                       {
                                           var proxy = new UdpClient();
                                           proxy.Client.ReceiveTimeout = 5000;

                                           proxy.Connect(DNSServer);
                                           proxy.Send(buff, read);

                                           var bytes = proxy.Receive(ref DNSServer);

                                           //bytes.ToHexString().Log();

                                           server.SendTo(bytes, client);
                                       }
                                       catch
                                       {
                                       }

                                   });
                           });

                    }
                    catch
                    {
                    }

                }
            });

        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        //处理图标最大化
        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            notifyIcon1.Visible = false;
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
            this.Show();
            this.Focus();
        }

        //处理图标最小化
        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            show = !show;
            if (!show)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                notifyIcon1.Visible = true;
            }
        }



    }
}