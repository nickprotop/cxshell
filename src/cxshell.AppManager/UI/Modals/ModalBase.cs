using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Drawing;

namespace cxshell.AppManager.UI.Modals;

/// <summary>
/// Base class for App Manager modals. Provides async show/await via TaskCompletionSource,
/// standard modal-window creation, Escape handling, and guaranteed result completion on close.
/// Modelled on LazyNuGet's ModalBase, trimmed to DotOS needs.
/// </summary>
public abstract class ModalBase<TResult> where TResult : notnull
{
    private readonly TaskCompletionSource<TResult> _tcs = new();
    protected TResult? Result { get; set; }

    protected Window Modal { get; private set; } = null!;
    protected ConsoleWindowSystem WindowSystem { get; private set; } = null!;
    protected Window? ParentWindow { get; private set; }

    /// <summary>Show the modal and await its result.</summary>
    public Task<TResult> ShowAsync(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
    {
        WindowSystem = windowSystem;
        ParentWindow = parentWindow;

        Modal = CreateModal();
        BuildContent();

        Modal.KeyPressed += OnKeyPressed;
        Modal.OnClosed += OnModalClosed;

        WindowSystem.AddWindow(Modal);
        WindowSystem.SetActiveWindow(Modal);
        SetInitialFocus();

        return _tcs.Task;
    }

    protected virtual Window CreateModal()
    {
        var builder = new WindowBuilder(WindowSystem)
            .AsModal()
            .Resizable(GetResizable())
            .Movable(true)
            .Minimizable(false)
            .Maximizable(false)
            .WithBorderStyle(GetBorderStyle())
            .WithBorderColor(GetBorderColor());

        var title = GetTitle();
        if (!string.IsNullOrEmpty(title)) builder.WithTitle(title);

        var (w, h) = GetSize();
        builder.WithSize(w, h).Centered();

        if (ParentWindow != null) builder.WithParent(ParentWindow);

        return builder.Build();
    }

    protected abstract void BuildContent();
    protected abstract string GetTitle();
    protected virtual (int width, int height) GetSize() => (60, 14);
    protected virtual bool GetResizable() => false;
    protected virtual BorderStyle GetBorderStyle() => BorderStyle.Rounded;
    protected virtual Color GetBorderColor() => Color.Grey27;
    protected virtual void SetInitialFocus() { }

    protected virtual void OnKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        if (e.KeyInfo.Key == ConsoleKey.Escape)
        {
            OnEscapePressed();
            e.Handled = true;
        }
    }

    protected virtual void OnEscapePressed() => CloseWithResult(GetDefaultResult());
    protected virtual TResult GetDefaultResult() => default!;

    private void OnModalClosed(object? sender, EventArgs e)
    {
        OnCleanup();
        _tcs.TrySetResult(Result ?? GetDefaultResult());
    }

    protected virtual void OnCleanup() { }

    protected void CloseWithResult(TResult result)
    {
        Result = result;
        Modal.Close();
    }
}
