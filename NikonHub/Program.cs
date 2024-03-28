// See https://aka.ms/new-console-template for more information
using NikonScript;

Console.WriteLine("Hello, World!");

/*
Process p = new ();
p.StartInfo.FileName = @"..\..\..\..\NikonConsoleDriver\bin\Debug\net8.0\NikonConsoleDriver.exe";
p.StartInfo.Arguments = "";
p.StartInfo.UseShellExecute = false;
p.StartInfo.RedirectStandardOutput = true;
p.StartInfo.CreateNoWindow = true;
p.StartInfo.RedirectStandardInput = true;
p.Start();

p.StandardInput.WriteLine("connect D500");
Thread.Sleep(5000);
p.StandardInput.WriteLine("capture");
Thread.Sleep(2000);
p.StandardInput.WriteLine("capture");
Thread.Sleep(2000);
p.StandardInput.WriteLine("capture");
Thread.Sleep(2000);
p.StandardInput.WriteLine("disconnect");
Thread.Sleep(5000);
p.StandardInput.WriteLine("");
Thread.Sleep(5000);
p.WaitForExit();
*/

List<ProcessHost> agents = new List<ProcessHost>();

foreach(var arg in args)
{
    if(File.Exists(arg))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"launching {arg}");
        Console.ForegroundColor = ConsoleColor.White;
        var host = new ProcessHost();
        host.Start();
        host.RunPlan(arg);
        agents.Add(host);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"unable to launch {arg}");
        Console.ForegroundColor = ConsoleColor.White;
    }
}

Console.WriteLine("press any key to exit");
Console.ReadKey();

foreach(var agent in agents)
{
    agent.StopPlan();
}

Console.WriteLine("Goodbye, World!");