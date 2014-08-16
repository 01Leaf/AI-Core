﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using ZMQ;

namespace LinkTable
{
    class Program
    {

        static List<Word> wordList;

        struct Word
        {
            public string translated;
            public string pronounced;
        }

        //初始化引擎
        static bool Initialize()
        {

            string[] rawList;
            string[] parsed;
            wordList = new List<Word>();
            List<string> pronunciations = new List<string>();

            string currentPath = Environment.CurrentDirectory;

            //載入指令表
            try
            {

                rawList = File.ReadAllLines(currentPath + @"\ls.txt");
            }
            catch
            {
                Console.WriteLine("ERR(Command list does not exist or is corrupted. [AI])");
                Console.Out.Flush();
                return false;
            }


            int numLine = 1;
            string tmp; //條件式暫存
            Stack<string> currentScope = new Stack<string>();
            foreach (string line in rawList)
            {
                try
                {
                    if (line.Trim() != "" && !line.StartsWith("#")) //無視備註和空行
                    {
                        if (line.EndsWith("{"))
                        {
                            currentScope.Push(line.Trim('{'));
                        }
                        else if (line == "}")
                        {
                            currentScope.Pop();
                        }
                        else
                        {
                            parsed = line.Split(',');
                            Word word;
                            if (currentScope.Count != 0)
                            {
                                if (parsed[1].Contains("?"))
                                { //組合條件式
                                    tmp = "";
                                    foreach (string cond in currentScope)
                                    {
                                        tmp = tmp + cond + "&";
                                    }
                                    word.translated = tmp + parsed[1];
                                }
                                else
                                {
                                    tmp = "";
                                    foreach (string cond in currentScope)
                                    {
                                        tmp = tmp + cond + "&";
                                    }
                                    word.translated = tmp.TrimEnd('&') + "?" + parsed[1];
                                }
                            }
                            else
                            {
                                word.translated = parsed[1];
                            }
                            word.pronounced = parsed[0];
                            wordList.Add(word);
                            pronunciations.Add(word.pronounced);
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("ERR(Unable to parse line " + numLine.ToString() + " in word list. [AI])");
                    Console.Out.Flush();
                    return false;
                }
                numLine++;
            }

            return true;


        }

        //對消息進行處理
        static void Process(List<string> messages)
        {          
            
            foreach (string msg in messages)
            {
                
                string[] spt_AND; //AND is splited first, OR-first spliting is not necessary due to multi-triggering
                string[] spt_OR; 

                foreach (Word word in wordList)
                {

                    spt_AND = word.pronounced.Split('+');

                    foreach (string str in spt_AND)
                    {
                        spt_OR = str.Split('/');
                        foreach (string s in spt_OR)
                        {
                            if (msg.Contains(s))
                            {
                                Console.WriteLine(word.translated);
                            }
                        }
                    }

                    
                }

            }
        }

        //接下來是與 AZUSA 和其他引擎溝通的部分, 一般不用更改
        //============================================================
        static Thread AZUSAlistener;
        static int AZUSAPid;
        static bool AZUSAAlive = true;

        static string[] InputPorts = new string[] { };
        static bool PortChanged = false;
        static List<Socket> connections = new List<Socket>();
        static List<string> messages = new List<string>();

        static void Main(string[] args)
        {
            if (!Initialize())
            {
                Console.WriteLine("ERR(AI Engine initialization failed.)");
                return;
            }

            AZUSAlistener = new Thread(new ThreadStart(ListenToConsole));
            AZUSAlistener.Start();

            using (Context ctx = new Context())
            {
                while (AZUSAAlive)
                {
                    connections.Clear();

                    foreach (string port in InputPorts)
                    {
                        if (port.Trim() != "")
                        {
                            Socket client = ctx.Socket(SocketType.SUB);
                            client.Connect(port);
                            client.Subscribe("", Encoding.UTF8);

                            connections.Add(client);
                        }
                    }

                    PortChanged = false;

                    while (!PortChanged && AZUSAAlive)
                    {
                        foreach (Socket socket in connections)
                        {
                            messages.Add(socket.Recv(Encoding.UTF8));
                        }

                        Process(messages);

                        messages.Clear();
                    }
                }

            }

        }

        static void ListenToConsole()
        {

            Console.WriteLine("RegisterAs(AI)");
            Console.WriteLine("GetInputPorts()");
            InputPorts = Console.ReadLine().Split(',');
            PortChanged = true;

            Console.WriteLine("GetAzusaPid()");
            AZUSAPid = Convert.ToInt32(Console.ReadLine());


            //Listen for PortHasChanged

            while (AZUSAAlive)
            {
                try
                {
                    System.Diagnostics.Process.GetProcessById(AZUSAPid);
                    if (Console.ReadLine().Trim() == "PortHasChanged")
                    {
                        Console.WriteLine("GetInputPorts()");
                        InputPorts = Console.ReadLine().Split(',');
                        PortChanged = true;
                    }
                }
                catch
                {
                    AZUSAAlive = false;
                    Environment.Exit(0);
                    break;
                }
            }
        }
    }
}
