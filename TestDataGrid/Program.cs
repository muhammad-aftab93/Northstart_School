namespace TestDataGrid
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            if (Environment.GetCommandLineArgs().Contains("--verify-school"))
            {
                Environment.ExitCode = Verification.SchoolVerification.Run();
                return;
            }
            var verification = Environment.GetCommandLineArgs().Contains("--verify-grid");
            using var verificationLog = verification ? new StreamWriter(Path.Combine(Path.GetTempPath(), "TestDataGrid-feature-checks.log")) { AutoFlush = true } : null;
            if (verificationLog != null) { Console.SetOut(verificationLog); Console.SetError(verificationLog); }
            if (!verification)
            {
                try { Application.Run(new MainForm()); }
                catch (Exception exception) { MessageBox.Show(exception.Message, "Northstar School", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                return;
            }
            var form = new Form1(true);
            if (verification)
            {
                form.Shown += async (_, _) =>
                {
                    try { await form.VerifyGridFeaturesAsync(); }
                    catch (Exception exception) { Console.Error.WriteLine(exception); Environment.ExitCode = 1; }
                    finally { form.Close(); }
                };
            }
            Application.Run(form);
        }
    }
}