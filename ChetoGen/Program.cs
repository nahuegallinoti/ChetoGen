using System.Text;
using ChetoGen.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;
// Setting the input encoding throws when stdin is redirected (CI, piped --yes runs); the
// fancy output only needs OutputEncoding, so make the input side best-effort.
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
    AnsiConsole.MarkupLine($"[red]Unexpected error:[/] {ex.Message}");
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return -1;
}
