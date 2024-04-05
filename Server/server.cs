using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

IPEndPoint ipPoint = new IPEndPoint(IPAddress.Any, 8888);
using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
socket.Bind(ipPoint);
socket.Listen(1000);
Console.WriteLine("Server started!");
while (true)
{
    using Socket client = await socket.AcceptAsync();
    var responseBytes = new byte[512];
    var bytes = await client.ReceiveAsync(responseBytes);
    string response = Encoding.UTF8.GetString(responseBytes, 0, bytes);
    if (response == "exit")
    {
        break;
    }
    string[] clientArgs = response.Split(' ', 3);
    string message = "";
    string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
    string strWorkPath = Path.GetDirectoryName(strExeFilePath);
    string dir = Path.Combine(strWorkPath, "server\\data");
    Directory.CreateDirectory(dir);
    var filename = Path.Combine(dir, clientArgs[1]);

    if (clientArgs[0] == "PUT")
    {
        if (File.Exists(filename))
        {
            message += "403";
        }
        else
        {
            try
            {
                using StreamWriter writer = new(filename);
                writer.Write(clientArgs[2]);
                message += "200";
            }
            catch
            {
                message += "403";
            }

        }
    }
    else if (clientArgs[0] == "DELETE")
    {
        if (File.Exists(filename))
        {
            try
            {
                File.Delete(filename);
                message += "200";
            }
            catch
            {
                message += "404";
            }
        }
        else
        {
            message += "404";
        }

    }
    else
    {
        if (File.Exists(filename))
        {
            try
            {
                string content = File.ReadAllText(filename);
                message += "200 " + content;
            }
            catch
            {
                message += "404";
            }
        }
        else
        {
            message += "404";
        }
    }
    var messageBytes = Encoding.UTF8.GetBytes(message);
    await client.SendAsync(messageBytes);
}
socket.Close();