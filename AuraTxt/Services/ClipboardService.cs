namespace AuraTxt.Services;

public static class ClipboardService
{
    public static async Task<string> GetSelectedTextAsync(int delayMs = 100)
    {
        await Task.Delay(delayMs);
        string previous = "";
        string selected = "";

        try
        {
            if (System.Windows.Clipboard.ContainsText())
                previous = System.Windows.Clipboard.GetText();

            System.Windows.Clipboard.Clear();
            System.Windows.Forms.SendKeys.SendWait("^c");
            await Task.Delay(80);

            if (System.Windows.Clipboard.ContainsText())
                selected = System.Windows.Clipboard.GetText();
        }
        catch { }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(previous))
                    System.Windows.Clipboard.SetText(previous);
                else
                    System.Windows.Clipboard.Clear();
            }
            catch { }
        }
        return selected;
    }
}
