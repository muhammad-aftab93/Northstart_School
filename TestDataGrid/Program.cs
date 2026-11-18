using EnterpriseUI.WinForms;
using TestDataGrid.Services;

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
                EnterpriseSplashScreen? splash = null;
                try
                {
                    splash = new EnterpriseSplashScreen { Title = "Northstar School", Subtitle = "Loading your workspace", FooterText = "Built with EnterpriseUI", Logo = SystemIcons.Application.ToBitmap(), MinimumDuration = 1200 };
                    splash.ShowSplash();
                    splash.ReportProgress(10, "Opening school records\u2026");
                    var store = new SchoolStore();
                    splash.ReportProgress(45, "Building the workspace\u2026");
                    var main = new MainForm(store);
                    splash.ReportProgress(95, "Almost ready\u2026");
                    main.Shown += (_, _) => splash.Close();
                    Application.Run(main);
                }
                catch (Exception exception)
                {
                    if (splash != null) { splash.FadeOut = false; splash.MinimumDuration = 0; splash.Close(); }
                    MessageBox.Show(exception.Message, "Northstar School", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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