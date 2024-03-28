using NikonController;

using (CaptureDevice captureDevice = new())
{
    try
    {
        bool exitSession = false;

        do
        {
            string? lastInput = Console.ReadLine()?.Trim()?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(lastInput))
            {
                try
                {
                    captureDevice.Dispatch(lastInput);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Ready");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(ex.ToString());
                    exitSession = true;
                }
            }
            else
            {
                exitSession = true;
            }
        }
        while (!exitSession);
    }
    finally
    {
        captureDevice.Disconnect();
    }
}