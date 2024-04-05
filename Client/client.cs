using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                await socket.ConnectAsync("127.0.0.1", 8888);
                string message = "";
                string method = "0";
                while (!("123".Contains(method) || method == "exit"))
                {
                    Console.Write("Enter action (1 - get a file, 2 - create a file, 3 - delete a file): > ");
                    method = Console.ReadLine() ?? "0";
                    if (method == "1")
                    {
                        message += "GET ";
                    }
                    else if (method == "2")
                    {
                        message += "PUT ";
                    }
                    else if (method == "3")
                    {
                        message += "DELETE ";
                    }
                    else if (method == "exit")
                    {
                        message = method;
                    }
                    else
                    {
                        method = "0";
                        Console.WriteLine("Invalid action");
                    }
                }
                if (method != "exit")
                {
                    string filename = "";
                    do
                    {
                        Console.Write("Enter filename: > ");
                        filename = Console.ReadLine() ?? "";
                        if (!IsFileNameValid(filename))
                        {
                            Console.WriteLine("Invalid file name");
                        }
                    } while (!IsFileNameValid(filename));
                    message += filename + " ";
                    if (method == "2")
                    {
                        Console.Write("Enter file content: > ");
                        message += Console.ReadLine();
                    }
                }
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(messageBytes);
                Console.WriteLine("The request was sent");

                var responseBytes = new byte[512];
                var builder = new StringBuilder();
                int bytes;
                do
                {
                    bytes = await socket.ReceiveAsync(responseBytes);
                    string responsePart = Encoding.UTF8.GetString(responseBytes, 0, bytes);
                    builder.Append(responsePart);
                }
                while (bytes > 0);


                var response = builder.ToString().Split(' ', 2);
                if (response[0] == "200")
                {
                    if (method == "1")
                    {
                        string content = response[1];
                        try { content = response[1]; } catch { }
                        Console.WriteLine("The content of the file is: " + content);
                    }
                    if (method == "2")
                    {
                        Console.WriteLine("The response says that the file was created");
                    }
                    if (method == "3")
                    {
                        Console.WriteLine("The response says that the file was successfully deleted");
                    }
                }
                if (response[0] == "403")
                {
                    if (method == "2")
                    {
                        Console.WriteLine("The response says that creating the file was forbidden");
                    }
                }
                if (response[0] == "404")
                {
                    Console.WriteLine("The response says that the file was not found");
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("Couldn't connect to server");
            }
        }

        private static readonly byte[] TestFileBytes = Encoding.ASCII.GetBytes(@"X");
        public static bool IsFileNameValid(string file)
        {
            try
            {
                if (string.IsNullOrEmpty(file))
                    return false;

                if (file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return false;

                string fileName = Path.Combine(Path.GetTempPath(), file);
                using (FileStream fileStream = File.Create(fileName))
                {
                    fileStream.Write(TestFileBytes, 0, TestFileBytes.Length);
                }

                File.Delete(fileName);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}