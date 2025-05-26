using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace PSP4_4
{
    class UdpChatServer
{
    private const int Port = 11000;
    private static UdpClient _udpClient;
    private static readonly string _historyFile = "chat_history.txt";
    private static readonly Dictionary<IPEndPoint, string> _clients = new Dictionary<IPEndPoint, string>();
    private static readonly object _lock = new object();

    static void Main()
    {
        Console.Title = "Сервер чата";
        LoadHistory();

        _udpClient = new UdpClient(Port);
        Console.WriteLine($"Сервер чата запущен на порту {Port}. Ожидание подключений...");

        Thread receiveThread = new Thread(ReceiveMessages);
        receiveThread.Start();

        Thread consoleThread = new Thread(ServerConsole);
        consoleThread.Start();
    }

    private static void LoadHistory()
    {
        if (File.Exists(_historyFile))
        {
            Console.WriteLine("Последние сообщения из истории:");
            Console.WriteLine(File.ReadAllText(_historyFile));
            Console.WriteLine(new string('-', 50));
        }
    }

    private static void ReceiveMessages()
    {
        try
        {
            while (true)
            {
                IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _udpClient.Receive(ref clientEndPoint);
                string message = Encoding.UTF8.GetString(data);

                // Обработка регистрации нового пользователя
                if (message.StartsWith("[REGISTER]"))
                {
                    string useName = message.Substring(10);
                    RegisterClient(clientEndPoint, useName);
                    continue;
                }

                // Обработка обычного сообщения
                if (_clients.TryGetValue(clientEndPoint, out string userName))
                {
                    string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {userName}: {message}";
                    Console.WriteLine(formattedMessage);
                    
                    lock (_lock)
                    {
                        File.AppendAllText(_historyFile, formattedMessage + Environment.NewLine);
                    }

                    BroadcastMessage(formattedMessage, clientEndPoint);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private static void RegisterClient(IPEndPoint endPoint, string userName)
    {
        lock (_lock)
        {
            _clients[endPoint] = userName;
            Console.WriteLine($"Пользователь '{userName}' подключен ({endPoint.Address})");
            
            string welcomeMessage = $"[{DateTime.Now:HH:mm:ss}] Система: {userName} присоединился к чату";
            File.AppendAllText(_historyFile, welcomeMessage + Environment.NewLine);
            BroadcastMessage(welcomeMessage, null);
        }
    }

    private static void BroadcastMessage(string message, IPEndPoint sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        lock (_lock)
        {
            foreach (var client in _clients.Keys)
            {
                if (sender == null || !client.Equals(sender))
                {
                    try
                    {
                        _udpClient.Send(data, data.Length, client);
                    }
                    catch { /* Игнорируем ошибки отправки */ }
                }
            }
        }
    }

    private static void ServerConsole()
    {
        while (true)
        {
            string message = Console.ReadLine();
            if (message.ToLower() == "exit")
            {
                Environment.Exit(0);
            }

            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] Сервер: {message}";
            Console.WriteLine(formattedMessage);
            
            lock (_lock)
            {
                File.AppendAllText(_historyFile, formattedMessage + Environment.NewLine);
            }
            
            BroadcastMessage(formattedMessage, null);
        }
    }
}
}
