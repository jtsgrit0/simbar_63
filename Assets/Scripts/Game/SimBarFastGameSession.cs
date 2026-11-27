#if UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using UnityEngine;

public sealed class SimBarFastGameSession
{
    private const int GameWidth = 1600, GameHeight = 900;
    private readonly object gate = new object();
    private NamedPipeServerStream pipe;
    private StreamWriter writer;
    private Process player;
    private Texture2D frame;
    private byte[] nextFrame;
    private string error;
    private volatile bool loaded;
    private string version;
    private int generation;
    private bool pointerHeld;
    private bool gameFocused;
    private int pointerButton;
    private Vector2 pointer;

    public void StartPreview()
    {
        ClosePreview();
        error = null;
        loaded = false;
        try
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string index = File.ReadAllText(Path.Combine(root, "docs", "index.html"));
            Match match = Regex.Match(index, "(?:src|href)=\"FastGame/([0-9a-f]{16})/boot[.]js\"");
            if (!match.Success) throw new InvalidDataException("No FastGame release was found in docs/index.html.");
            version = match.Groups[1].Value;
            string distribution = Path.Combine(root, "Builds", "Steam", "Windows", version, "SimBar-win32-x64");
            string game = Path.Combine(distribution, "resources", "game");
            if (!File.Exists(Path.Combine(distribution, "SimBar.exe")) ||
                !File.Exists(Path.Combine(game, "FastGame", version, "runtime-manifest.json")))
                throw new FileNotFoundException("Build or unpack the matching Windows release first: " + version);

            string source = Path.Combine(root, "Tools", "UnityFastGamePreview", "main.cjs");
            string previewRoot = Path.Combine(root, "Library", "SimBarFastGamePreview");
            string host = Path.Combine(previewRoot, version);
            string bundle = Path.Combine(host, "resources", "app");
            Directory.CreateDirectory(bundle);
            foreach (string file in Directory.GetFiles(distribution))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;
                CopyIfChanged(file, Path.Combine(host, name));
            }
            string language = Path.Combine(host, "locales");
            Directory.CreateDirectory(language);
            foreach (string file in Directory.GetFiles(Path.Combine(distribution, "locales"), "*.pak"))
                CopyIfChanged(file, Path.Combine(language, Path.GetFileName(file)));
            File.Copy(source, Path.Combine(bundle, "main.cjs"), true);
            File.WriteAllText(Path.Combine(bundle, "package.json"), "{\"name\":\"simbar-unity-preview\",\"version\":\"1.0.0\",\"main\":\"main.cjs\"}");

            string channel = "simbar-unity-" + Guid.NewGuid().ToString("N");
            var stream = new NamedPipeServerStream(channel, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            pipe = stream;
            var start = new ProcessStartInfo(Path.Combine(host, "SimBar.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = host
            };
            start.EnvironmentVariables["SIMBAR_PREVIEW_PIPE"] = @"\\.\pipe\" + channel;
            start.EnvironmentVariables["SIMBAR_PREVIEW_GAME"] = game;
            start.EnvironmentVariables["SIMBAR_PREVIEW_PROFILE"] = Path.Combine(previewRoot, "Profile");
            player = Process.Start(start);
            if (player == null) throw new InvalidOperationException("Electron did not start.");
            int id = generation;
            new Thread(() => ReadFrames(stream, id)) { IsBackground = true }.Start();
        }
        catch (Exception ex)
        {
            ClosePreview();
            error = ex.Message;
        }
    }

    private static void CopyIfChanged(string from, string to)
    {
        var a = new FileInfo(from);
        var b = new FileInfo(to);
        if (!b.Exists || a.Length != b.Length || a.LastWriteTimeUtc != b.LastWriteTimeUtc)
            File.Copy(from, to, true);
    }

    private void ReadFrames(NamedPipeServerStream stream, int id)
    {
        try
        {
            stream.WaitForConnection();
            lock (gate)
            {
                if (id != generation) return;
                writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            }
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                while (true)
                {
                    byte kind = reader.ReadByte();
                    int length = reader.ReadInt32();
                    if (length < 0 || length > 8000000) throw new InvalidDataException("Invalid preview frame.");
                    byte[] bytes = reader.ReadBytes(length);
                    if (bytes.Length != length) throw new EndOfStreamException();
                    lock (gate)
                    {
                        if (id != generation) return;
                        if (kind == 1) nextFrame = bytes;
                        else if (kind == 2) error = Encoding.UTF8.GetString(bytes);
                        else if (kind == 3) loaded = Encoding.UTF8.GetString(bytes) == "simbar://app/index.html";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lock (gate) if (id == generation && error == null) error = ex is EndOfStreamException ? "Preview process disconnected." : ex.Message;
        }
    }

    public string Version => version;
    public bool IsRunning => player != null && !player.HasExited;

    public bool Tick()
    {
        byte[] data;
        lock (gate) { data = nextFrame; nextFrame = null; }
        if (data != null)
        {
            if (frame == null) frame = new Texture2D(2, 2, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
            frame.LoadImage(data);
            return true;
        }
        if (player != null && player.HasExited)
        {
            if (error == null) error = "FastGame renderer stopped (exit " + player.ExitCode + ").";
            ClosePreview();
            return true;
        }
        return false;
    }

    public void Draw(Rect area)
    {
        if (frame != null && error == null)
        {
            GUI.DrawTexture(Fit(area), frame, ScaleMode.StretchToFill, false);
            return;
        }
        string message = error ?? (loaded ? "Waiting for the first FastGame frame..." :
            (player == null ? "FastGame is not running." : "Starting SIMBAR FastGame..."));
        GUI.Box(new Rect(area.center.x - Mathf.Min(280f, area.width * 0.45f), area.center.y - 32f,
            Mathf.Min(560f, area.width * 0.9f), 64f), message);
    }


    public static Rect Fit(Rect area)
    {
        float scale = Mathf.Min(area.width / GameWidth, area.height / GameHeight);
        Vector2 size = new Vector2(GameWidth, GameHeight) * scale;
        return new Rect(area.center - size / 2f, size);
    }

    [Serializable]
    private class InputMessage
    {
        public string type, button, keyCode;
        public int x, y, clickCount, deltaX, deltaY;
        public string[] modifiers;
    }

    public void HandleInput(Event entry, Rect view)
    {
        bool inside = view.Contains(entry.mousePosition);
        int x = Mathf.Clamp((int)((entry.mousePosition.x - view.x) / view.width * GameWidth), 0, GameWidth - 1);
        int y = Mathf.Clamp((int)((entry.mousePosition.y - view.y) / view.height * GameHeight), 0, GameHeight - 1);
        var message = new InputMessage { x = x, y = y, clickCount = Mathf.Max(1, entry.clickCount) };
        var modifiers = new System.Collections.Generic.List<string>();
        if (entry.shift) modifiers.Add("shift");
        if (entry.control) modifiers.Add("control");
        if (entry.alt) modifiers.Add("alt");
        if (pointerHeld && pointerButton == 0 && entry.type == EventType.MouseDrag) modifiers.Add("leftButtonDown");
        if (pointerHeld && pointerButton == 1 && entry.type == EventType.MouseDrag) modifiers.Add("rightButtonDown");
        message.modifiers = modifiers.ToArray();
        switch (entry.type)
        {
            case EventType.MouseDown when inside:
                pointerHeld = true; pointerButton = entry.button; gameFocused = true;
                message.type = "mouseDown"; message.button = MouseButton(entry.button); break;
            case EventType.MouseUp when pointerHeld:
                pointerHeld = false;
                message.type = "mouseUp"; message.button = MouseButton(pointerButton); break;
            case EventType.MouseMove when inside:
            case EventType.MouseDrag when pointerHeld:
                message.type = "mouseMove"; break;
            case EventType.ScrollWheel when inside:
                message.type = "mouseWheel"; message.deltaX = Mathf.RoundToInt(-entry.delta.x * 36f);
                message.deltaY = Mathf.RoundToInt(-entry.delta.y * 36f); break;
            case EventType.KeyDown when gameFocused:
            case EventType.KeyUp when gameFocused:
                message.type = entry.type == EventType.KeyDown ? "keyDown" : "keyUp";
                message.keyCode = Key(entry.keyCode); if (message.keyCode == null) return; break;
            default: return;
        }
        if (message.type.StartsWith("mouse", StringComparison.Ordinal)) pointer = new Vector2(x, y);
        Send(message);
        if (message.type == "keyDown" && !entry.control && !entry.alt && entry.character >= 32 && entry.character != 127)
            Send(new InputMessage { type = "char", keyCode = entry.character.ToString(), modifiers = message.modifiers });
        entry.Use();
    }

    public void LoseFocus()
    {
        if (pointerHeld)
            Send(new InputMessage { type = "mouseUp", x = (int)pointer.x, y = (int)pointer.y, button = MouseButton(pointerButton), clickCount = 1 });
        pointerHeld = false;
        gameFocused = false;
    }

    private static string MouseButton(int key) => key == 1 ? "right" : key == 2 ? "middle" : "left";
    private static string Key(KeyCode key)
    {
        if (key >= KeyCode.A && key <= KeyCode.Z) return key.ToString().ToUpperInvariant();
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)key - (int)KeyCode.Alpha0).ToString();
        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return ((int)key - (int)KeyCode.Keypad0).ToString();
        switch (key)
        {
            case KeyCode.Escape: return "Escape";
            case KeyCode.Space: return "Space";
            case KeyCode.Return: case KeyCode.KeypadEnter: return "Return";
            case KeyCode.Tab: return "Tab";
            case KeyCode.Backspace: return "Backspace";
            case KeyCode.Delete: return "Delete";
            case KeyCode.LeftArrow: return "Left";
            case KeyCode.RightArrow: return "Right";
            case KeyCode.UpArrow: return "Up";
            case KeyCode.DownArrow: return "Down";
            default: return null;
        }
    }

    private void Send(InputMessage message)
    {
        StreamWriter sink;
        lock (gate) sink = writer;
        try { if (sink != null) sink.WriteLine(JsonUtility.ToJson(message)); }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    public void ClosePreview()
    {
        Send(new InputMessage { type = "close" });
        lock (gate) { generation++; writer = null; nextFrame = null; }
        pipe?.Dispose();
        pipe = null;
        if (player != null)
        {
            try { if (!player.HasExited && !player.WaitForExit(500)) player.Kill(); }
            catch (InvalidOperationException) {}
            finally { player.Dispose(); player = null; }
        }
        if (frame != null) { UnityEngine.Object.DestroyImmediate(frame); frame = null; }
        pointerHeld = false;
        gameFocused = false;
    }
}
#endif
