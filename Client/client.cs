using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
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
                string filename = "";

                string strExeFilePath = Assembly.GetExecutingAssembly().Location;
                string strWorkPath = Path.GetDirectoryName(strExeFilePath);
                string dir = Path.Combine(strWorkPath, "client\\data");
                Directory.CreateDirectory(dir);

                string method = "0";
                byte[] messageBytes;
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
                    if (method == "3") 
                    {
                        var nameOrId = "0";
                        while (!"12".Contains(nameOrId))
                        {
                            Console.Write("Do you want to delete the file by name or by id (1 - name, 2 - id): > ");
                            nameOrId = Console.ReadLine() ?? "0";
                            if (nameOrId == "1")
                            {
                                message += "BY_NAME ";
                                message += AddFilenameOrId();
                            }
                            else if (nameOrId == "2")
                            {
                                message += "BY_ID ";
                                message += AddFilenameOrId(false);
                            }
                            else
                            {
                                nameOrId = "0";
                                Console.WriteLine("Invalid action");
                            }
                        }
                    }
                    if (method == "1") 
                    {
                        var nameOrId = "0";
                        while (!"12".Contains(nameOrId))
                        {
                            Console.Write("Do you want to get the file by name or by id (1 - name, 2 - id): > ");
                            nameOrId = Console.ReadLine() ?? "0";
                            if (nameOrId == "1")
                            {
                                message += "BY_NAME ";
                                message += AddFilenameOrId();
                            }
                            else if (nameOrId == "2")
                            {
                                message += "BY_ID ";
                                message += AddFilenameOrId(false);
                            }
                            else
                            {
                                nameOrId = "0";
                                Console.WriteLine("Invalid action");
                            }
                        }
                    }
                    if (method == "2") 
                    {
                        do
                        {
                            filename = AddFilenameOrId();
                            if (File.Exists(Path.Combine(dir, filename)))
                            {
                                break;
                            }
                            else
                            {
                                Console.WriteLine("File does not exist");
                            }
                        } while (true);

                        string fileOnServer;
                        do
                        {
                            Console.Write("Enter filename to be saved on server: > ");
                            fileOnServer = Console.ReadLine() ?? "";
                            if (!IsFileNameValid(fileOnServer, true))
                            {
                                Console.WriteLine("Invalid filename");
                            }
                        } while (!IsFileNameValid(fileOnServer, true));

                        message += fileOnServer;
                    }
                }

                messageBytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(messageBytes);
                Console.WriteLine("The request was sent");

                if (method == "2")
                {
                    var fileSend = Path.Combine(dir, filename);

                    await socket.SendAsync(File.ReadAllBytes(fileSend));
                    socket.Shutdown(SocketShutdown.Send);
                }

                var responseBytes = new byte[512];
                using MemoryStream ms = new();
                int bytes;
                do
                {
                    bytes = await socket.ReceiveAsync(responseBytes);
                    ms.Write(responseBytes, 0, bytes);
                }
                while (bytes > 0);

                if (method == "1") 
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    var response = new List<byte>();
                    int bytesRead = 10;
                    while ((bytesRead = ms.ReadByte()) != ' ')
                    {
                        response.Add((byte)bytesRead);
                    }
                    var word = Encoding.UTF8.GetString(response.ToArray());
                    if (word == "200")
                    {
                        string localFilename;
                        do
                        {
                            Console.Write("The file was downloaded! Specify a name for it: > ");
                            localFilename = Console.ReadLine();
                            if (string.IsNullOrEmpty(localFilename))
                            {
                                Console.WriteLine("Invalid file name");
                            }
                            else if (File.Exists(Path.Combine(dir, localFilename)))
                            {
                                Console.WriteLine("File already exists");
                            }
                            else
                            {
                                break;
                            }
                        } while (true);
                        try
                        {
                            using FileStream fs = File.OpenWrite(Path.Combine(dir, localFilename));
                            ms.CopyTo(fs);
                            Console.WriteLine("File saved on the hard drive");
                        }
                        catch { Console.WriteLine("File couldn't be saved on the hard drive"); }
                    }
                    if (word == "404")
                    {
                        Console.WriteLine("The response says that the file was not found");
                    }
                }
                if (method == "2")
                {
                    string msg = Encoding.UTF8.GetString(ms.ToArray());
                    var response = msg.Split(' ', 2);
                    if (response[0] == "200")
                    {
                        Console.WriteLine($"The response says that the file was created. ID = {response[1]}");
                    }
                    if (response[0] == "403")
                    {
                        Console.WriteLine("The response says that creating the file was forbidden");
                    }
                }
                if (method == "3")
                {
                    string msg = Encoding.UTF8.GetString(ms.ToArray());
                    var response = msg.Split(' ', 2);
                    if (response[0] == "200")
                    {
                        Console.WriteLine("The response says that the file was successfully deleted");
                    }
                    if (response[0] == "404")
                    {
                        Console.WriteLine("The response says that the file was not found");
                    }
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("Couldn't connect to server");
            }
        }

        private static readonly byte[] TestFileBytes = Encoding.ASCII.GetBytes(@"X");
        public static bool IsFileNameValid(string file, bool allowNulls = false)
        {
            try
            {
                if (string.IsNullOrEmpty(file))
                {
                    if (allowNulls) return true;
                    return false;
                }

                if (file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return false;

                if (file.Contains(' ') || file.Contains('\t')) return false;

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

        public static string AddFilenameOrId(bool isFile = true)
        {
            if (isFile)
            {
                string filename;
                do
                {
                    Console.Write("Enter filename: > ");
                    filename = Console.ReadLine() ?? "";
                    if (!IsFileNameValid(filename))
                    {
                        Console.WriteLine("Invalid filename");
                    }
                } while (!IsFileNameValid(filename));
                return filename;
            }
            else
            {
                string id;
                do
                {
                    Console.Write("Enter id: > ");
                    id = Console.ReadLine() ?? "";
                    if (!int.TryParse(id, out _))
                    {
                        Console.WriteLine("Invalid id");
                    }
                } while (!int.TryParse(id, out _));
                return id;
            }
        }
    }
}
