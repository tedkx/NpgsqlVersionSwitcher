using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NpgsqlVersionSwitcher
{
    public static class Helper
    {
        public static void CallGacUtil(GacUtilOperation operation, string assemblyOrDll)
        {
            if (string.IsNullOrWhiteSpace(assemblyOrDll)) throw new ArgumentNullException("assemblyOrDll", "Assembly/Dll argument cannot be null");
            if (operation == GacUtilOperation.Install && !assemblyOrDll.EndsWith(".dll")) throw new ArgumentException("assemblyOrDll", "Dll file must be supplied when installing with gacutil");

            using (var p = Process.Start(new ProcessStartInfo()
            {
                FileName = Constants.DefaultGacUtilPath,
                Arguments = string.Format("/{0} \"{1}\"", Constants.OperationArgs[operation], assemblyOrDll),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            }))
            {
                var gacoutput = string.Empty;
                while (!p.StandardOutput.EndOfStream) gacoutput += "\n" + p.StandardOutput.ReadLine();
                if (string.IsNullOrWhiteSpace(gacoutput))
                {
                    string error = p.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(error)) throw new Exception("GacUtil error: " + error);
                }
                if (operation == GacUtilOperation.Install && !gacoutput.Contains("Assembly successfully added to the cache") ||
                    (operation != GacUtilOperation.Install && !gacoutput.Contains("Number of failures = 0")))
                {
                    throw new Exception("GacUtil failure, please check output:\n" + gacoutput);
                }
            }
        }

        public static void UIDo(Action action, bool invalidateQuerySuggested = true)
        {
            if (System.Windows.Application.Current == null || System.Windows.Application.Current.Dispatcher == null) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try { action(); }
                catch {  } //katapiola
                if (invalidateQuerySuggested) System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            });
        }
    }
}
