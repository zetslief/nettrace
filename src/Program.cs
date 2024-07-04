using Nettrace;

var filePath = args[0];
using var file = File.OpenRead(filePath);
NettraceReader.Read(file);