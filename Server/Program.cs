using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;

class 服务器
{
    private TcpListener tcp监听器;
    private List<客户端信息> 客户端列表 = new List<客户端信息>();
    private object 锁对象 = new object();
    private string 日志文件路径 = "日志.txt"; // 日志文件路径
    private string 搜索文件路径 = "搜索结果.txt"; // 搜索结果文件路径
    private string 封禁用户文件路径 = "封禁用户.txt"; // 封禁用户文件路径
    private HashSet<string> 封禁用户集合;

    public 服务器(int 端口)
    {
        tcp监听器 = new TcpListener(IPAddress.Any, 端口);

        // 创建日志文件，如果不存在的话
        if (!File.Exists(日志文件路径))
        {
            using (File.Create(日志文件路径)) { }
        }
        // 创建搜索结果文件，如果不存在的话
        if (!File.Exists(搜索文件路径))
        {
            using (File.Create(搜索文件路径)) { }
        }

        // 创建或读取封禁用户文件
        if (!File.Exists(封禁用户文件路径))
        {
            using (File.Create(封禁用户文件路径)) { }
        }
        封禁用户集合 = File.ReadAllLines(封禁用户文件路径).ToHashSet();
    }

    public void 启动()
    {
        记录日志("服务器已启动。");

        tcp监听器.Start();

        // 启动一个新线程来处理控制台输入
        Thread 控制台输入线程 = new Thread(new ThreadStart(读取控制台输入));
        控制台输入线程.Start();

        while (true)
        {
            TcpClient tcp客户端 = tcp监听器.AcceptTcpClient();

            // 用户连接到服务器的消息
            byte[] 连接消息字节 = new byte[8192];
            int 读取字节数 = tcp客户端.GetStream().Read(连接消息字节, 0, 8192);
            string 连接消息 = Encoding.UTF8.GetString(连接消息字节, 0, 读取字节数);

            // 创建客户端信息对象来保存客户端信息
            客户端信息 客户端信息 = new 客户端信息 { Tcp客户端 = tcp客户端, 连接消息 = 连接消息 };

            // 检查是否在封禁列表中
            if (封禁用户集合.Contains(客户端信息.用户名))
            {
                Console.WriteLine($"拒绝连接封禁用户 '{客户端信息.用户名}'。");
                tcp客户端.Close();
                continue;
            }

            // 检查是否有重复用户名
            if (!用户名可用(客户端信息.用户名))
            {
                发送消息(客户端信息, "该用户名已被使用，请选择其他用户名。");
                tcp客户端.Close();
                continue;
            }

            lock (锁对象)
            {
                客户端列表.Add(客户端信息);
            }

            // 广播新用户加入的消息
            广播消息($"{客户端信息.用户名} 加入了服务器");
            // 启动一个新线程来处理客户端通信
            Thread 客户端线程 = new Thread(new ParameterizedThreadStart(处理客户端通信));
            客户端线程.Start(客户端信息);
        }
    }

    private bool 用户名可用(string 用户名)
    {
        lock (锁对象)
        {
            return !客户端列表.Exists(c => c.用户名 == 用户名);
        }
    }

    private void 处理客户端通信(object 客户端信息Obj)
    {
        客户端信息 客户端信息 = (客户端信息)客户端信息Obj;
        TcpClient tcp客户端 = 客户端信息.Tcp客户端;
        NetworkStream 客户端流 = tcp客户端.GetStream();

        记录日志($"用户 '{客户端信息.用户名}' 已连接。");

        byte[] 消息字节 = new byte[8192];
        int 读取字节数;

        while (true)
        {
            读取字节数 = 0;

            try
            {
                读取字节数 = 客户端流.Read(消息字节, 0, 8192);
            }
            catch
            {
                break;
            }

            if (读取字节数 == 0)
                break;

            string 数据 = Encoding.UTF8.GetString(消息字节, 0, 读取字节数);

            // 广播消息给所有客户端
            广播消息($"{客户端信息.用户名}: {数据}");
        }

        Console.WriteLine("用户 '" + 客户端信息.用户名 + "' 断开了连接。");
        lock (锁对象)
        {
            客户端列表.Remove(客户端信息);
        }
        广播消息($"{客户端信息.用户名} 已离开聊天。");
        tcp客户端.Close();
    }

    private void 广播消息(string 消息)
    {
        byte[] 广播字节 = Encoding.UTF8.GetBytes(消息);

        lock (锁对象)
        {
            foreach (var 客户端 in 客户端列表)
            {
                NetworkStream 客户端流 = 客户端.Tcp客户端.GetStream();
                客户端流.Write(广播字节, 0, 广播字节.Length);
                客户端流.Flush();
            }
        }

        // 记录广播的消息到日志文件
        记录日志(消息);
    }

    private void 发送消息(客户端信息 客户端信息, string 消息)
    {
        byte[] 消息字节 = Encoding.UTF8.GetBytes(消息);
        客户端信息.Tcp客户端.GetStream().Write(消息字节, 0, 消息字节.Length);
        客户端信息.Tcp客户端.GetStream().Flush();
    }

    private void 封禁用户(string 目标用户名)
    {
        try
        {
            lock (锁对象)
            {
                if (!封禁用户集合.Contains(目标用户名))
                {
                    封禁用户集合.Add(目标用户名);

                    // 更新封禁用户文件
                    File.WriteAllLines(封禁用户文件路径, 封禁用户集合);

                    Console.WriteLine($"用户 '{目标用户名}' 已被封禁。");

                    // 尝试踢出被封禁用户
                    踢出封禁用户(目标用户名);

                    // 记录封禁操作到日志文件
                    记录日志($"用户 '{目标用户名}' 被管理员封禁。");
                }
                else
                {
                    Console.WriteLine($"用户 '{目标用户名}' 已经被封禁。");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"封禁用户时发生异常：{ex}");
            记录日志($"封禁用户时发生异常：{ex}");
        }
    }

    private void 踢出封禁用户(string 目标用户名)
    {
        try
        {
            lock (锁对象)
            {
                客户端信息 目标客户端 = 客户端列表.FirstOrDefault(c => c.用户名 == 目标用户名);

                if (目标客户端 != null)
                {
                    发送消息(目标客户端, "你已被管理员封禁");

                    // 发送被踢出的消息给其他用户
                    广播消息($"用户 '{目标用户名}' 被管理员封禁");

                    // 从客户端列表中移除被踢出的用户
                    客户端列表.Remove(目标客户端);

                    // 关闭与被踢出用户的连接
                    目标客户端.Tcp客户端.Close();
                }
                else
                {
                    // 如果用户不存在，记录无效用户的消息到日志文件
                    记录日志($"尝试踢出用户 '{目标用户名}'，但该用户不存在。");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"踢出用户时发生异常：{ex}");
            记录日志($"踢出用户时发生异常：{ex}");
        }
    }

    private void 踢出用户(string 目标用户名)
    {
        try
        {
            lock (锁对象)
            {
                客户端信息 目标客户端 = 客户端列表.FirstOrDefault(c => c.用户名 == 目标用户名);

                if (目标客户端 != null)
                {
                    发送消息(目标客户端, "你已被管理员踢出服务器");

                    // 发送被踢出的消息给其他用户
                    广播消息($"用户 '{目标用户名}' 被管理员踢出");

                    // 从客户端列表中移除被踢出的用户
                    客户端列表.Remove(目标客户端);

                    // 关闭与被踢出用户的连接
                    目标客户端.Tcp客户端.Close();
                }
                else
                {
                    // 如果用户不存在，记录无效用户的消息到日志文件
                    记录日志($"尝试踢出用户 '{目标用户名}'，但该用户不存在。");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"踢出用户时发生异常：{ex}");
            记录日志($"踢出用户时发生异常：{ex}");
        }
    }

    private void 解禁用户(string 目标用户名)
    {
        try
        {
            lock (锁对象)
            {
                if (封禁用户集合.Contains(目标用户名))
                {
                    封禁用户集合.Remove(目标用户名);

                    // 更新封禁用户文件
                    File.WriteAllLines(封禁用户文件路径, 封禁用户集合);

                    Console.WriteLine($"用户 '{目标用户名}' 已被解禁。");

                    // 记录解禁操作到日志文件
                    记录日志($"用户 '{目标用户名}' 被管理员解禁。");
                }
                else
                {
                    Console.WriteLine($"用户 '{目标用户名}' 不在封禁列表中。");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"解禁用户时发生异常：{ex}");
            记录日志($"解禁用户时发生异常：{ex}");
        }
    }

    private void 显示所有用户()
    {
        try
        {
            lock (锁对象)
            {
                Console.WriteLine("当前在线用户：");
                foreach (var 客户端 in 客户端列表)
                {
                    Console.WriteLine(客户端.用户名);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"显示用户时发生异常：{ex}");
            记录日志($"显示用户时发生异常：{ex}");
        }
    }

    private void 读取控制台输入()
    {
        while (true)
        {
            string 输入 = Console.ReadLine();

            if (输入.StartsWith("/kick"))
            {
                string 目标用户名 = 输入.Split(' ')[1];
                踢出用户(目标用户名);
            }
            else if (输入.StartsWith("/ban"))
            {
                string 目标用户名 = 输入.Split(' ')[1];
                封禁用户(目标用户名);
            }
            else if (输入.StartsWith("/unban"))
            {
                string 目标用户名 = 输入.Split(' ')[1];
                解禁用户(目标用户名);
            }
            else if (输入.StartsWith("/users"))
            {
                显示所有用户();
            }
            else if (输入.StartsWith("/search"))
            {
                string 搜索关键词 = 输入.Substring(8); // 截取"/search "之后的部分
                搜索日志(搜索关键词);
            }
        }
    }

    private void 记录日志(string 消息)
    {
        using (StreamWriter 日志文件 = new StreamWriter(日志文件路径, true))
        {
            日志文件.WriteLine($"{DateTime.Now}: {消息}");
        }
    }

    private void 搜索日志(string 搜索关键词)
    {
        try
        {
            var 日志内容 = File.ReadAllLines(日志文件路径);
            var 匹配结果 = 日志内容.Where(行 => 行.Contains(搜索关键词)).ToList();

            using (StreamWriter 搜索结果文件 = new StreamWriter(搜索文件路径, false))
            {
                foreach (var 匹配行 in 匹配结果)
                {
                    搜索结果文件.WriteLine(匹配行);
                }
            }

            Console.WriteLine($"找到 {匹配结果.Count} 条包含 \"{搜索关键词}\" 的日志记录，已保存到 \"{搜索文件路径}\"。");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"搜索日志时发生异常：{ex}");
            记录日志($"搜索日志时发生异常：{ex}");
        }
    }

    public class 客户端信息
    {
        public TcpClient Tcp客户端 { get; set; }
        public string 用户名 => 连接消息.Split(':')[0];
        public string 连接消息 { get; set; }
    }

}

class 程序
{
    static void Main()
    {
        Console.WriteLine("欢迎使用XShChat 1.0.r1.b3_server");

        Console.Write("请输入服务器端口号: ");
        int 端口 = int.Parse(Console.ReadLine());

        服务器 服务器 = new 服务器(端口);
        服务器.启动();
    }
}
