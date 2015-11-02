using NuGet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;

namespace NpgsqlVersionSwitcher
{
    public class SwitchCommand : ICommand
    {
        static readonly object _locker = new object();
        private Tuple<string, int, int, Func<string>>[] operations;
        private VersionsViewModel _model = null;

        public SwitchCommand(VersionsViewModel model)
        {
            _model = model;
        }

        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return _model != null && !_model.IsBusy;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;

            _model.ShowBottomPart = true;
            _model.Output = string.Empty;
            _model.Progress = 0;
            _model.IsBusy = true;
            _model.Error = "-- Please Wait --";

            operations = new Tuple<string, int, int, Func<string>>[]
            {
                new Tuple<string, int, int, Func<string>>("Downloading package", 0, 1, _download),
                new Tuple<string, int, int, Func<string>>("Extracting Gacutil", 60, 2, _extractGacUtil),
                new Tuple<string, int, int, Func<string>>("Uninstalling DLLs", 65, 3, _uninstall),
                new Tuple<string, int, int, Func<string>>("Installing DLLs", 85, 4, _install),
                new Tuple<string, int, int, Func<string>>("Making changes to machine.config", 90, 5, _config),
                new Tuple<string, int, int, Func<string>>("Cleaning up", 95, 6, _cleanup),
            };

            new System.Threading.Thread(() =>
            {
                string error = string.Empty;
                try
                {
                    _updateModel("Starting", 0, 0);
                    foreach (var operationData in operations)
                    {
                        _updateModel(operationData.Item1, operationData.Item2, operationData.Item3);
                        error = operationData.Item4();
                        if (!string.IsNullOrWhiteSpace(error)) break;
                    }
                    _updateModel("Done", 100);
                }
                catch (WebException wex)
                {
                    error = string.Format("{0}", wex.Status == WebExceptionStatus.ProtocolError && wex.Message.Contains("404") ? "Version " + _model.SelectedVersion + " not found" : "Error: " + wex.Message);
                }
                catch (Exception ex)
                {
                    error = string.Format("Error: {0}", ex.Message);
                }
                Helper.UIDo(() =>
                {
                    _model.Error = string.IsNullOrWhiteSpace(error) ? "Operation Successful" : error;
                    _model.IsBusy = false;
                });
            }).Start();
        }

        #region Operations

        private string _download()
        {
            var nugetUrl = Constants.NugetUrl + (_model.SelectedVersion == null ? string.Empty : "?version=" + _model.SelectedVersion.Value);

            try
            {
                if (File.Exists(Constants.LocalFilename)) File.Delete(Constants.LocalFilename);
                var client = new WebClient();
                client.DownloadProgressChanged += _downloadProgressChanged;
                client.DownloadFileCompleted += _downloadComplete;
                client.DownloadFileAsync(new Uri(nugetUrl), Constants.LocalFilename);
                lock (_locker) { System.Threading.Monitor.Wait(_locker); }
            }
            catch (WebException wex)
            {
                return string.Format("{0}", wex.Status == WebExceptionStatus.ProtocolError && wex.Message.Contains("404") ? "Version " + _model.SelectedVersion + " not found" : "Error: " + wex.Message);
            }
            catch (Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
            return string.Empty;
        }

        private string _extractGacUtil()
        {
            try
            {
                File.WriteAllBytes("gacutil.zip", System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(Constants.GacUtilResourceKey).ReadAllBytes());
                System.IO.Compression.ZipFile.ExtractToDirectory("gacutil.zip", Environment.CurrentDirectory);
            }
            catch (Exception ex)
            {
                return ex.Message.EndsWith("already exists.") ? string.Empty : string.Format("Error: {0}", ex.Message);
            }
            return string.Empty;
        }

        private string _uninstall()
        {
            try
            {
                foreach (var dll in new string[] { "Npgsql", "Mono.Security" }) Helper.CallGacUtil(GacUtilOperation.Uninstall, dll);
            }
            catch (Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
            return string.Empty;
        }

        private string _install()
        {
            try
            {
                var pkg = new ZipPackage(Constants.LocalFilename);
                foreach (var dll in pkg.AssemblyReferences.Where(a => a.TargetFramework.Version.Major == 4 && a.TargetFramework.Version.Minor == 5))
                {
                    File.WriteAllBytes(dll.EffectivePath, dll.GetStream().ReadAllBytes());

                    if (dll.Name.Contains("Npgsql"))
                    {
                        var ver = AssemblyName.GetAssemblyName(dll.EffectivePath).Version;
                        var versionList = new string[] { ver.Major.ToString(), ver.Minor.ToString(), ver.Build.ToString(), ver.Revision.ToString() }.ToList();
                        Constants.DbProviderNodeAttributeValues["type"] = string.Format(Constants.DbProviderNodeAttributeValues["type"], string.Join(".", versionList));
                    }

                    Helper.CallGacUtil(GacUtilOperation.Install, Environment.CurrentDirectory + "\\" + dll.EffectivePath);
                }
            }
            catch (Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
            return string.Empty;
        }

        private string _config()
        {
            try
            {
                foreach (var machineConfig in new string[] { Constants.DefaultMachineConfigPath, Constants.DefaultMachineConfig64Path })
                {
                    var doc = new XmlDocument();
                    doc.Load(machineConfig);
                    var factoriesNode = doc.SelectSingleNode("configuration/system.data/DbProviderFactories");
                    var node = doc.CreateElement("add");
                    foreach (var pair in Constants.DbProviderNodeAttributeValues)
                    {
                        var attr = doc.CreateAttribute(pair.Key);
                        attr.Value = pair.Value;
                        node.Attributes.Append(attr);
                    }
                    var existingNode = factoriesNode.SelectSingleNode("add[@name='Npgsql Data Provider']");
                    if (existingNode != null) factoriesNode.RemoveChild(existingNode);
                    factoriesNode.AppendChild(node);
                    doc.Save(machineConfig);
                    //_pulse();
                }
            }
            catch (Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
            return string.Empty;
        }

        private string _cleanup()
        {
            try
            {
                foreach (var flnm in Constants.CreatedFilenames) File.Delete(string.Format("{0}\\{1}", Environment.CurrentDirectory, flnm));
                Directory.Delete(Environment.CurrentDirectory + @"\1033");
                return string.Empty;
            }
            catch(Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
        }

        #endregion

        private void _updateModel(string output, int progress, int operationNo = 0)
        {
            Helper.UIDo(() =>
            {
                _model.Output = output + (operationNo == 0 ? string.Empty : string.Format(", {0} of {1}", operationNo, operations.Count()));
                _model.Progress = progress;
            });
        }

        private void _downloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Helper.UIDo(() => { _model.Progress = (int)Math.Ceiling(e.ProgressPercentage * 0.6m); });
        }

        private void _downloadComplete(object sender, AsyncCompletedEventArgs e)
        {
            lock (_locker) { System.Threading.Monitor.Pulse(_locker); }
        }
    }
}
