namespace MyTranslate
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Overlay.NativeMethods.SetProcessDPIAware();
            ApplicationConfiguration.Initialize();
            Application.Run(new Main_Translate());
        }
    }
}