using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

var process = Process.GetProcessesByName("profileMe").Single();

Console.WriteLine($"Process {process.ProcessName} Id: {process.Id} started at {process.StartTime}");
Console.WriteLine($"Start time UNIX: {((DateTimeOffset)process.StartTime).ToUnixTimeSeconds()}");

var directory = Environment.GetEnvironmentVariable("TMP") ?? "/tmp";

Debug.Assert(Directory.Exists(directory));

Console.WriteLine($"Directory: {directory}");

/*
In order to ensure filename uniqueness, a disambiguation key is generated.
On Mac and NetBSD, this is the process start time encoded as the number of seconds since UNIX epoch time.
If /proc/$PID/stat is available (all other *nix platforms), then the process start time encoded as jiffies since boot time is used. 
 */

var file = Directory.GetFiles(directory, $"dotnet-diagnostic-{process.Id}-*").Single();
Console.WriteLine($"File: {file}");

using CancellationTokenSource cts = new();
using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
var endpoint = new UnixDomainSocketEndPoint(file);

await socket.ConnectAsync(endpoint, cts.Token);

Console.WriteLine($"Connected? {socket.Connected}");
