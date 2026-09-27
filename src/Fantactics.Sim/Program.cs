using System.Text;
using Fantactics.Sim;
using McMaster.Extensions.CommandLineUtils;

Console.OutputEncoding = Encoding.UTF8;
return CliHost.Run(args, PhysicalConsole.Singleton);
