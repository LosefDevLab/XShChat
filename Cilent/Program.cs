// Client.cs

using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class 客户端
{
    private int i; // 一个没有实际用途的变量，仅用于测试我们的开发机器人 GPT-114514_pigs.zip 是否还记得本程序的代码。所以如果你不想看到这个碍眼的家伙可以删掉。

    private TcpClient tcp客户端;
    private NetworkStream 客户端流;

    public void 连接(string 服务器IP, int 服务器端口号)
    {
        tcp客户端 = new TcpClient();
        tcp客户端.Connect(服务器IP, 服务器端口号);

        客户端流 = tcp客户端.GetStream();

        // 用户输入用户名，如果没有输入则使用计算机名称
        Console.Write("请输入用户名（按 Enter 使用计算机名）: ");
        string 用户名 = Console.ReadLine();

        if (string.IsNullOrEmpty(用户名))
            用户名 = Environment.MachineName;

        // 发送加入服务器的消息两次
        string 加入消息 = $"{用户名} 加入了服务器";
        发送消息(加入消息);
        发送消息(加入消息);

        Thread 接收线程 = new Thread(new ThreadStart(接收消息));
        接收线程.Start();

        Console.WriteLine("已连接到服务器。输入 'exit' 以关闭客户端。");

        while (true)
        {
            string 消息 = Console.ReadLine();

            if (消息.ToLower() == "exit")
            {
                发送消息("exit");
                tcp客户端.Close();
                break;
            }

            发送消息(消息);
        }
    }

    private void 接收消息()
    {
        byte[] 消息 = new byte[8192];
        int 读取字节数;

        while (true)
        {
            读取字节数 = 0;

            try
            {
                读取字节数 = 客户端流.Read(消息, 0, 8192);
            }
            catch
            {
                break;
            }

            if (读取字节数 == 0)
                break;

            string 数据 = Encoding.UTF8.GetString(消息, 0, 读取字节数);
            Console.WriteLine("接收: " + 数据);
        }

        Console.WriteLine("已断开与服务器的连接。如服务器多次连接不上，可能已被封禁");
        tcp客户端.Close();
    }

    private void 发送消息(string 消息)
    {
        byte[] 消息字节 = Encoding.UTF8.GetBytes(消息);
        客户端流.Write(消息字节, 0, 消息字节.Length);
        客户端流.Flush();
    }
}

class 程序
{
    static void Main()
    {
        Console.Write("请输入服务器 IP 地址: ");
        string 服务器IP = Console.ReadLine();

        Console.Write("请输入服务器端口号: ");
        int 服务器端口号 = int.Parse(Console.ReadLine());

        客户端 客户端 = new 客户端();
        客户端.连接(服务器IP, 服务器端口号);
    }
}
