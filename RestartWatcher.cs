using System.Diagnostics;
using System.Text;

namespace nvrlift.AssettoServer.HostExtension;

public class RestartWatcher
{
    private readonly string _basePath;
    private readonly string _restartPath;
    private readonly string _assettoServerPath;
    private readonly string _restartFilter = "*.asrestart";
    private Process? CurrentProcess = null;
    private readonly string _presetsPath;

    public RestartWatcher()
    {
        _basePath = Environment.CurrentDirectory;
        _restartPath = Path.Join(_basePath, "cfg", "restart");
        _presetsPath = Path.Join(_basePath, "presets");
        _assettoServerPath = Path.Join(_basePath, "AssettoServer.exe");

        if (!Path.Exists(_restartPath))
            Directory.CreateDirectory(_restartPath);
        if (!Path.Exists(_presetsPath))
            Directory.CreateDirectory(_presetsPath);
        
        // Init File Watcher
        StartWatcher(_restartPath);
        foreach (var path in Directory.GetDirectories(_presetsPath))
        {
            var presetRestartPath = Path.Join(path, "restart");
            if (!Path.Exists(presetRestartPath))
                Directory.CreateDirectory(presetRestartPath);
            StartWatcher(presetRestartPath);
        }
    }

    public void Init()
    {
        ConsoleLog($"Starting restart service.");
        ConsoleLog($"Base directory: {_basePath}");
        ConsoleLog($"Preset directory: {_presetsPath}");

        var presets = Directory.GetDirectories(_presetsPath).Select(d => Path.GetFileName(d)).ToList();
        var randomPreset = presets[Random.Shared.Next(presets.Count)];
        
        var initPath = Path.Join(_restartPath, "init.asrestart");
        var initFile = File.Create(initPath);
        byte[] content = new UTF8Encoding(true).GetBytes(randomPreset);
        initFile.Write(content, 0, content.Length);
        initFile.Close();
        Thread.Sleep(2_000);
    }

    private void StartWatcher(string path)
    {
        var watcher = new FileSystemWatcher()
        {
            Path = Path.Join(path),
            NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                                                    | NotifyFilters.FileName,
            Filter = _restartFilter,
        };
        watcher.Created += new FileSystemEventHandler(OnRestartFileCreated);

        watcher.EnableRaisingEvents = true;
    }
    
    private void OnRestartFileCreated(object source, FileSystemEventArgs e)
    {
        if (CurrentProcess != null)
            StopAssettoServer(CurrentProcess);

        ConsoleLog($"Restart file found: {e.Name}");

        string preset = File.ReadAllText(e.FullPath);

        ConsoleLogSpacer();
        
        string args = $"--preset=\"{preset.Trim()}\"";
        CurrentProcess = StartAssettoServer(_assettoServerPath, args);
        ConsoleLog($"Server restarted with Process-ID: {CurrentProcess?.Id}");
        ConsoleLog($"Using config preset: {preset}");
        
        File.Delete(e.FullPath);
    }

    private Process StartAssettoServer(string assettoServerPath, string assettoServerArgs)
    {
        var psi = new ProcessStartInfo(assettoServerPath, assettoServerArgs);
        psi.UseShellExecute = true;

        return Process.Start(psi);
    }

    private void StopAssettoServer(Process serverProcess)
    {
        while (!serverProcess.HasExited)
            serverProcess.Kill();
    }

    public void StopAssettoServer()
    {
        StopAssettoServer(CurrentProcess!);
    }

    private string ConsoleLogTime()
    {
        var date = DateTime.Now;
        return $"[{date:yyyy-MM-dd hh:mm:ss}]";
    }

    private void ConsoleLogSpacer()
    {
        Console.WriteLine("-----");
    }
    
    private void ConsoleLog(string log)
    {
        var output = $"{ConsoleLogTime()} {log}";
        Console.WriteLine(output);
    }
}
