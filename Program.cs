using DnsClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;

public class Program
{
    private static string[] _subdomains, _topleveldomains;
    private static List<string> _found = new List<string>();
    private static ResourceSemaphore _foundSemaphore = new ResourceSemaphore();
    private static int _foundCount = 0;
    private static int _scanned = 0;
    private static string _detailedCSV = "Host;IP addresses (separated by commas)";

    public static void Main()
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

        Console.Title = "xDNS | Made by https://github.com/GabryB03/";
        Console.WriteLine("Calculating the DNS average resolution time, please wait a while.");
        Console.WriteLine($"The average resolution time for every DNS query is of {GetAverageResolutionTime()}ms (based on 10 resolutions).");
        
        if (!System.IO.Directory.Exists("wordlists"))
        {
            Console.WriteLine("The directory 'wordlists' does not exist. Press ENTER to exit from the program.");
            Console.ReadLine();
            return;
        }

        if (!System.IO.File.Exists("wordlists\\sub.txt"))
        {
            Console.WriteLine("The file 'wordlists\\sub.txt' does not exist. Press ENTER to exit from the program.");
            Console.ReadLine();
            return;
        }

        if (!System.IO.File.Exists("wordlists\\top.txt"))
        {
            Console.WriteLine("The file 'wordlists\\top.txt' does not exist. Press ENTER to exit from the program.");
            Console.ReadLine();
            return;
        }

        _topleveldomains = System.IO.File.ReadAllLines("wordlists\\top.txt");
        _subdomains = System.IO.File.ReadAllLines("wordlists\\sub.txt");

        if (_topleveldomains.Length == 0)
        {
            Console.WriteLine("The file 'wordlists\\top.txt' has 0 top-level domains. Press ENTER to exit from the program.");
            Console.ReadLine();
            return;
        }

        if (_subdomains.Length == 0)
        {
            Console.WriteLine("The file 'wordlists\\sub.txt' has 0 subdomains. Press ENTER to exit from the program.");
            Console.ReadLine();
            return;
        }

        Console.WriteLine("Please, insert the string to analyze (it can also contain a top-level domain, a subdomain or both): ");
        string toAnalyze = Console.ReadLine();
        int method = -1;

        Console.WriteLine($"Please, choose a scan method:\r\n[1] Scan all possibilities mixing top-level domains and subdomains ({(_subdomains.Length * _topleveldomains.Length) + " chances"}).\r\n[2] Scan all possibilities using only top-level domains ({_topleveldomains.Length + " chances"}).\r\n[3] Scan all possibilities using only subdomains ({_subdomains.Length + " chances"}).");

        while (method <= 0 || method > 3)
        {
            Console.Write("> ");

            try
            {
                method = int.Parse(Console.ReadLine());
            }
            catch
            {
                Console.WriteLine("Invalid number. Please, try again.");
                continue;
            }

            if (method <= 0 || method > 3)
            {
                Console.WriteLine("Invalid number. Please, try again.");
            }
        }

        if (System.IO.File.Exists("valid.txt"))
        {
            System.IO.File.Delete("valid.txt");
        }

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        Console.WriteLine("Scan started, please wait. The found results will be saved in the 'valid.txt' output file.");

        Thread thread = new Thread(() => DoScan(method, toAnalyze));
        thread.Priority = ThreadPriority.Highest;
        thread.Start();

        int possibilities = 0;

        if (method == 1)
        {
            possibilities = _subdomains.Length * _topleveldomains.Length;
        }
        else if (method == 2)
        {
            possibilities = _topleveldomains.Length;
        }
        else if (method == 3)
        {
            possibilities = _subdomains.Length;
        }

        while (_scanned != possibilities)
        {
            Thread.Sleep(250);
        }

        string result = "";

        foreach (string entry in _found)
        {
            if (result == "")
            {
                result = entry;
            }
            else
            {
                result += "\r\n" + entry;
            }
        }

        stopwatch.Stop();
        Console.WriteLine("Scan succesfully finished. Found " + _foundCount + " valid entries. Time took: " + stopwatch.ElapsedMilliseconds + "ms (" + (stopwatch.ElapsedMilliseconds / 1000).ToString() + " seconds).");
        System.IO.File.WriteAllText("valid.txt", result);
        Console.WriteLine("All the valid entries that have been found in the scan have been saved in the file 'valid.txt'.");
        System.IO.File.WriteAllText("details.csv", _detailedCSV);
        Console.WriteLine("A detailed CSV with host + all found A records (IP addresses) has been saved as 'details.csv' (you can open it via any Excel executable).");
        Console.WriteLine("Press ENTER to exit from the program.");
        Console.ReadLine();
    }

    public static int GetAverageResolutionTime()
    {
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
            LookupClient lookupClient = new LookupClient(ipEndPoint);
            lookupClient.Query("google.com", QueryType.A);
        }

        int totalTime = 0;

        for (int i = 0; i < 10; i++)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
            LookupClient lookupClient = new LookupClient(ipEndPoint);
            lookupClient.Query("google.com", QueryType.A);

            stopwatch.Stop();
            totalTime += (int) stopwatch.ElapsedMilliseconds;
        }

        return totalTime / 10;
    }

    public static void DoScan(int method, string toAnalyze)
    {
        if (method == 1)
        {
            Thread.Sleep(5);

            foreach (string subdomain in _subdomains)
            {
                Thread.Sleep(5);

                foreach (string topleveldomain in _topleveldomains)
                {
                    Thread.Sleep(5);
                    new Thread(() => Scan(subdomain + "." + toAnalyze + "." + topleveldomain)).Start();
                    Thread.Sleep(5);
                }
            }
        }
        else if (method == 2)
        {
            foreach (string topleveldomain in _topleveldomains)
            {
                Thread.Sleep(5);
                new Thread(() => Scan(toAnalyze + "." + topleveldomain)).Start();
                Thread.Sleep(5);
            }
        }
        else if (method == 3)
        {
            foreach (string subdomain in _subdomains)
            {
                Thread.Sleep(5);
                new Thread(() => Scan(subdomain + "." + subdomain)).Start();
                Thread.Sleep(5);
            }
        }
    }

    public static void Scan(string str)
    {
        try
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
            LookupClient lookupClient = new LookupClient(ipEndPoint);
            IDnsQueryResponse theQuery = lookupClient.Query(str, QueryType.A);
            
            if (theQuery.Answers.Count > 0)
            {
                waitAgain: while (_foundSemaphore.IsResourceNotAvailable())
                {
                    Thread.Sleep(250);
                }

                if (_foundSemaphore.IsResourceAvailable())
                {
                    _foundSemaphore.LockResource();
                    _found.Add(str);
                    _foundCount++;
                    _scanned++;
                    string csvLine = str + ";";
                    bool initialized = true;

                    foreach (DnsClient.Protocol.ARecord aRecord in theQuery.Answers.ARecords())
                    {
                        if (initialized)
                        {
                            csvLine += aRecord.Address;
                            initialized = false;
                        }
                        else
                        {
                            csvLine += ", " + aRecord.Address;
                        }
                    }

                    _detailedCSV += "\r\n" + csvLine;
                    _foundSemaphore.UnlockResource();
                }
                else
                {
                    goto waitAgain;
                }
            }
            else
            {
                _scanned++;
            }
        }
        catch
        {
            _scanned++;
        }
    }
}