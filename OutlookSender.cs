using System.Runtime.InteropServices;

namespace WPUService;

internal static class OutlookSender
{
    public static bool IsAvailable()
    {
        try { return Type.GetTypeFromProgID("Outlook.Application") != null; }
        catch { return false; }
    }

    public static Task<(bool Ok, string Error)> SendAsync(string to, string subject, string body)
    {
        var tcs = new TaskCompletionSource<(bool, string)>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            object? outlook = null;
            object? mail = null;
            try
            {
                var t = Type.GetTypeFromProgID("Outlook.Application");
                if (t == null)
                {
                    tcs.SetResult((false, "Outlook is not installed."));
                    return;
                }

                outlook = Activator.CreateInstance(t);
                if (outlook == null)
                {
                    tcs.SetResult((false, "Could not start Outlook."));
                    return;
                }

                dynamic outlookApp = outlook;
                mail = outlookApp.CreateItem(0); // olMailItem

                dynamic mailItem = mail!;
                mailItem.To = to ?? "";
                mailItem.Subject = subject ?? "";
                mailItem.Body = body ?? "";
                mailItem.Send();

                tcs.SetResult((true, ""));
            }
            catch (Exception ex)
            {
                tcs.SetResult((false, ex.Message));
            }
            finally
            {
                if (mail != null)
                {
                    try { Marshal.FinalReleaseComObject(mail); } catch { }
                }
                if (outlook != null)
                {
                    try { Marshal.FinalReleaseComObject(outlook); } catch { }
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }
}
