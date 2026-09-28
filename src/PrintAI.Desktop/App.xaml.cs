using System.Windows;

namespace PrintAI.Desktop;

public partial class App : Application
{
    private void Application_Startup(
        object sender,
        StartupEventArgs e)
    {
        if (e.Args.Any(arg =>
                string.Equals(
                    arg,
                    "--self-test",
                    StringComparison.OrdinalIgnoreCase)))
        {
            var outputPath = ReadOption(
                e.Args,
                "--self-test-output");
            var pdfOutputPath = ReadOption(
                e.Args,
                "--self-test-pdf-output");

            Shutdown(
                DesktopSelfTest.Run(
                    outputPath,
                    pdfOutputPath));
            return;
        }

        new MainWindow().Show();
    }

    private static string? ReadOption(
        IReadOnlyList<string> args,
        string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(
                    args[i],
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
