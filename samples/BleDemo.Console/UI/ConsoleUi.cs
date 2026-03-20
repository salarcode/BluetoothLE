namespace BleDemo.Console.UI;

/// <summary>
/// Lightweight console UI helpers: spinner, coloured output, menus.
/// </summary>
internal static class ConsoleUi
{
    // ── Colours ──────────────────────────────────────────────────────────────

    public static void Write(string text, ConsoleColor color)
    {
        var prev = System.Console.ForegroundColor;
        System.Console.ForegroundColor = color;
        System.Console.Write(text);
        System.Console.ForegroundColor = prev;
    }

    public static void WriteLine(string text, ConsoleColor color)
    {
        Write(text, color);
        System.Console.WriteLine();
    }

    public static void Success(string msg) => WriteLine($"✓ {msg}", ConsoleColor.Green);
    public static void Error(string msg)   => WriteLine($"✗ {msg}", ConsoleColor.Red);
    public static void Info(string msg)    => WriteLine(msg, ConsoleColor.Cyan);
    public static void Dim(string msg)     => WriteLine(msg, ConsoleColor.DarkGray);

    // ── Headers ───────────────────────────────────────────────────────────────

    public static void PrintHeader(string title)
    {
        System.Console.WriteLine();
        var line = new string('─', title.Length + 4);
        WriteLine($"┌{line}┐", ConsoleColor.DarkCyan);
        WriteLine($"│  {title}  │", ConsoleColor.Cyan);
        WriteLine($"└{line}┘", ConsoleColor.DarkCyan);
        System.Console.WriteLine();
    }

    // ── Spinner ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Displays an animated spinner on the current line while <paramref name="task"/> runs.
    /// </summary>
    public static async Task SpinAsync(string label, Task task)
    {
        char[] frames = ['|', '/', '-', '\\'];
        int idx = 0;
        bool canAnimate = !System.Console.IsOutputRedirected;
        if (canAnimate) System.Console.CursorVisible = false;

        var cts = new CancellationTokenSource();
        var spin = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (canAnimate)
                {
                    System.Console.Write($"\r ");
                    Write($"{frames[idx++ % frames.Length]}", ConsoleColor.Yellow);
                    System.Console.Write($" {label}   ");
                }
                try { await Task.Delay(120, cts.Token); } catch { break; }
            }
        }, CancellationToken.None);

        await task;
        cts.Cancel();
        await spin;
        if (canAnimate)
        {
            System.Console.Write("\r" + new string(' ', label.Length + 10) + "\r");
            System.Console.CursorVisible = true;
        }
        else
        {
            System.Console.WriteLine($"  {label} done.");
        }
    }

    // ── Signal bar ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a visual signal-strength bar for an RSSI value (in dBm).
    /// Typical range: −100 (weak) to −40 (strong).
    /// </summary>
    public static string SignalBar(int rssi)
    {
        int bars = rssi >= -55 ? 5
                 : rssi >= -65 ? 4
                 : rssi >= -75 ? 3
                 : rssi >= -85 ? 2
                 : 1;
        return $"[{"█".PadRight(bars, '█').PadRight(5, '░')}] {rssi} dBm";
    }

    // ── Menu selection ────────────────────────────────────────────────────────

    /// <summary>
    /// Prints a numbered menu and returns the chosen 1-based index,
    /// or 0 if the user typed something invalid.
    /// </summary>
    public static int PromptMenu(string prompt, IReadOnlyList<string> options)
    {
        System.Console.WriteLine();
        WriteLine(prompt, ConsoleColor.White);
        for (int i = 0; i < options.Count; i++)
            System.Console.WriteLine($"  {i + 1}. {options[i]}");

        System.Console.WriteLine();
        System.Console.Write("  › ");
        var input = System.Console.ReadLine()?.Trim();
        if (int.TryParse(input, out int choice) && choice >= 1 && choice <= options.Count)
            return choice;
        return 0;
    }

    public static void PressAnyKey(string msg = "Press any key to continue...")
    {
        Dim(msg);
        if (!System.Console.IsInputRedirected)
            System.Console.ReadKey(intercept: true);
        else
            System.Console.ReadLine();
    }

    public static void Separator() =>
        WriteLine(new string('─', 60), ConsoleColor.DarkGray);
}
