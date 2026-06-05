using System.Text;
using ChetoGen.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

// Console encoding is best-effort: either setter throws when the matching stream is redirected
// (CI, piped --yes runs). The UI degrades gracefully, so never let this crash startup.
try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
try { Console.InputEncoding = Encoding.UTF8; } catch (IOException) { }

try
{
    var app = new CommandApp<GenerateCommand>();
    app.Configure(config =>
    {
        config.SetApplicationName("chetogen");

        config.AddCommand<GenerateCommand>("generate")
            .WithAlias("g")
            .WithDescription("Generates the full file structure for a new entity.")
            .WithExample("generate", "Order")
            .WithExample("generate", "Order", "--prop", "Total:decimal:required", "--prop", "Notes:string");

        config.AddCommand<InitCommand>("init")
            .WithDescription("Creates a starter chetogen.json (and optionally copies the templates) in the target solution.")
            .WithExample("init")
            .WithExample("init", "--with-templates");
    });

    return await app.RunAsync(args);
}
catch (Exception ex)
{
    // MarkupLineInterpolated escapes the message — exception text often contains '[' or ']',
    // which would otherwise throw a second time inside the handler.
    AnsiConsole.MarkupLineInterpolated($"[red]Unexpected error:[/] {ex.Message}");
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return -1;
}
