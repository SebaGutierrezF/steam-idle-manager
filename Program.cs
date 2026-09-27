namespace SteamIdleManager;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLogger.Initialize();

        Application.SetUnhandledExceptionMode(
            UnhandledExceptionMode.CatchException
        );

        Application.ThreadException += (_, e) =>
        {
            AppLogger.Error(
                "Unhandled WinForms thread exception.",
                e.Exception
            );

            MessageBox.Show(
                "Steam Idle Manager encountered an unexpected error.\n\n"
                + "The error was written to the application log.",
                "Steam Idle Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLogger.Error(
                    "Unhandled application-domain exception.",
                    ex
                );
            }
            else
            {
                AppLogger.Error(
                    $"Unhandled non-Exception object: {e.ExceptionObject}"
                );
            }
        };

        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            AppLogger.Error("Fatal application startup/runtime error.", ex);

            MessageBox.Show(
                "Steam Idle Manager could not continue.\n\n"
                + "Check the logs folder for details.",
                "Steam Idle Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
        finally
        {
            AppLogger.Info("Steam Idle Manager process exiting.");
        }
    }
}
