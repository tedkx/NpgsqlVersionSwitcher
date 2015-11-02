using System;
using System.Collections.Generic;

namespace NpgsqlVersionSwitcher
{
    public static class Constants
    {
        public static Dictionary<GacUtilOperation, string> OperationArgs = new Dictionary<GacUtilOperation, string>()
        {
            { GacUtilOperation.Uninstall, "u" }, { GacUtilOperation.Install, "i" }, { GacUtilOperation.List, "l" },
        };
        public static Dictionary<string, string> DbProviderNodeAttributeValues = new Dictionary<string, string>()
        {
            { "name", "Npgsql Data Provider" }, { "invariant", "Npgsql" }, { "support", "FF" }, { "description", ".Net Framework Data Provider for Postgresql Server" },
            { "type", "Npgsql.NpgsqlFactory, Npgsql, Version={0}, Culture=neutral, PublicKeyToken=5d8b90d52f46fda7" },
        };
        public static string[] CreatedFilenames = new string[] {
            "gacutil.zip", "gacutil.exe", "gacutil.exe.config", "1033\\gacutlrc.dll", "Npgsql.dll", "Mono.Security.dll", "Npgsql.nupkg",
        };
        public const string NugetUrl = "http://packages.nuget.org/api/v1/package/Npgsql";
        public const string LocalFilename = "Npgsql.nupkg";
        public const string DefaultMachineConfigPath = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\Config\machine.config";
        public const string DefaultMachineConfig64Path = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config";
        public const string GacUtilResourceKey = "NpgsqlVersionSwitcher.Resources.gacutil.zip";
        //public const string DefaultWindowsSDKPath = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\";
        public static string DefaultGacUtilPath { get { return Environment.CurrentDirectory + @"\gacutil.exe"; } }
    }

    public enum GacUtilOperation { Install = 1, Uninstall = 2, List = 3 }
}
