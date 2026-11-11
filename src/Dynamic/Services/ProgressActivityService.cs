namespace Dynamic.Services;

public sealed record ProgressActivitySnapshot(
    string Title, double Percent, string Detail, string Speed, bool IsCompleted);

public sealed class ProgressActivityService
{
    public event EventHandler? Changed;
    public ProgressActivitySnapshot? Current { get; private set; }

    // Real producers (downloads, exports, installers) report measured progress here.
    public void Report(string title, double percent, string detail, string speed)
    {
        Current = new ProgressActivitySnapshot(title, Math.Clamp(percent, 0, 100), detail, speed, false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Complete(string title, string detail)
    {
        Current = new ProgressActivitySnapshot(title, 100, detail, "Готово", true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Current = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}