using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Server
{
    internal class Program
    {
        private static ReaderWriterLockSlim _lock = new();
        static Dictionary<int, string> dict = {};
        static int counter = 0;
        static string dir = "";

        static void Main()
        {
            if (File.Exists("data.txt"))
            {
                var lines = File.ReadLines("data.txt");
                foreach (var line in lines)
                {
                    var data = line.Split(' ');
                    dict.Add(int.Parse(data[0]), data[1]);
                    counter = Math.Max(counter, int.Parse(data[0]));
                }
            }
            string strExeFilePath = Assembly.GetExecutingAssembly().Location;
            string strWorkPath = Path.GetDirectoryName(strExeFilePath);
            dir = Path.Combine(strWorkPath, "server\\data");
            Directory.CreateDirectory(dir);

            IPEndPoint ipPoint = new(IPAddress.Any, 8888);
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(ipPoint);
            socket.Listen(1000);
            Console.WriteLine("Server started!");

            while (true)
            {
                Socket client = socket.Accept();
                new Thread(async () => await Interaction(client)).Start();
            }
        }
        static async Task Interaction(Socket client)
        {
            var responseBytes = new byte[1024];
            var bytes = await client.ReceiveAsync(responseBytes);
            string response = Encoding.UTF8.GetString(responseBytes, 0, bytes);
            if (response == "exit")
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
                Environment.Exit(0);
            }

            string[] clientArgs = response.Split(' ', 2);
            string message = "";
            using MemoryStream ms = new();

            if (clientArgs[0] == "PUT")
            {
                string filename;
                if (string.IsNullOrEmpty(clientArgs[1]))
                {
                    filename = Path.Combine(dir, Guid.NewGuid().ToString());
                }
                else
                {
                    filename = Path.Combine(dir, clientArgs[1]);
                }
                if (File.Exists(filename))
                {
                    message += "403";
                }
                else
                {
                    try
                    {
                        var buffer = new byte[512];
                        int b;
                        do
                        {
                            b = await client.ReceiveAsync(buffer);
                            ms.Write(buffer, 0, b);

                        } while (b > 0);


                        _lock.EnterWriteLock();
                        try
                        {
                            using (FileStream fs = new(filename, FileMode.CreateNew, FileAccess.Write))
                            {
                                ms.WriteTo(fs);
                            }

                            counter++;
                            dict.Add(counter, filename);

                            using (StreamWriter sw = File.AppendText("data.txt"))
                            {
                                sw.WriteLine(counter + " " + filename);
                            }

                            message += "200 " + counter;
                        }
                        catch
                        {
                            message += "403";
                        }
                        finally { _lock.ExitWriteLock(); }
                    }
                    catch
                    {
                        message += "404";
                    }

                }
            }
            else if (clientArgs[0] == "DELETE")
            {
                var deleteArgs = clientArgs[1].Split(' ');

                string filename;
                if (deleteArgs[0] == "BY_ID")
                {
                    _lock.EnterUpgradeableReadLock();
                    try
                    {
                        if (dict.ContainsKey(int.Parse(deleteArgs[1])))
                        {
                            filename = dict[int.Parse(deleteArgs[1])];
                            _lock.EnterWriteLock();
                            try
                            {
                                File.Delete(filename);

                                List<string> lines = new(File.ReadAllLines("data.txt"));
                                List<string> newlines = [];
                                foreach (string line in lines)
                                {
                                    var temp = line.Split(' ');
                                    if (temp[1] != filename)
                                    {
                                        newlines.Add(line);
                                    }
                                    else
                                    {
                                        dict.Remove(int.Parse(temp[0]));
                                    }
                                }

                                File.WriteAllLines("data.txt", newlines);

                                message += "200";
                            }
                            catch
                            {
                                message += "404";
                            }
                            finally { _lock.ExitWriteLock(); }
                        }
                        else
                        {
                            message += "404";
                        }
                    }
                    finally { _lock.ExitUpgradeableReadLock(); }
                }
                else 
                {
                    filename = Path.Combine(dir, deleteArgs[1]);
                    if (File.Exists(filename))
                    {
                        _lock.EnterWriteLock();
                        try
                        {
                            File.Delete(filename);

                            List<string> lines = new(File.ReadAllLines("data.txt"));
                            List<string> newlines = [];
                            foreach (string line in lines)
                            {
                                var temp = line.Split(' ');
                                if (temp[1] != filename)
                                {
                                    newlines.Add(line);
                                }
                                else
                                {
                                    dict.Remove(int.Parse(temp[0]));
                                }
                            }

                            File.WriteAllLines("data.txt", newlines);

                            message += "200";
                        }
                        catch
                        {
                            message += "404";
                        }
                        finally { _lock.ExitWriteLock(); }
                    }
                    else
                    {
                        message += "404";
                    }
                }
            }
            else 
            {
                var getArgs = clientArgs[1].Split(' ');
                string filename;
                if (getArgs[0] == "BY_ID")
                {
                    _lock.EnterReadLock();
                    try
                    {
                        if (dict.ContainsKey(int.Parse(getArgs[1])))
                        {
                            filename = dict[int.Parse(getArgs[1])];
                            ms.Write(File.ReadAllBytes(filename));
                            message += "200";
                        }
                        else
                        {
                            message += "404";
                        }
                    }
                    catch
                    {
                        message += "404";
                    }
                    finally { _lock.ExitReadLock(); }
                }
                else 
                {
                    filename = Path.Combine(dir, getArgs[1]);
                    if (File.Exists(filename))
                    {
                        _lock.EnterReadLock();
                        try
                        {
                            ms.Write(File.ReadAllBytes(filename));
                            message += "200";
                        }
                        catch
                        {
                            message += "404";
                        }
                        finally { _lock.ExitReadLock(); }
                    }
                    else
                    {
                        message += "404";
                    }
                }
                message += " ";
            }
            var messageBytes = Encoding.UTF8.GetBytes(message);
            if (clientArgs[0] == "GET")
            {
                await client.SendAsync(messageBytes.Concat(ms.ToArray()).ToArray());
            }
            else
            {
                await client.SendAsync(messageBytes);
            }
            client.Shutdown(SocketShutdown.Send);
        }
    }
}
