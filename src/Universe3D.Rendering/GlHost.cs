using System.Diagnostics;
using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;

namespace Universe3D.Rendering;

/// <summary>
/// Owns the <see cref="GLWpfControl"/> setup (OpenGL 4.1 core) and turns its render callback into
/// <see cref="ContextReady"/> (once, context current) and <see cref="Draw"/> (each frame, viewport set and cleared).
/// Renders on demand: call <see cref="RequestRender"/> after a change, and set <see cref="Animating"/> while
/// something moves.
/// </summary>
public sealed class GlHost
{
    // GLWpfControl shows a requested frame one composition late, so a request renders this many frames.
    private const int FramesPerRequest = 2;

    private readonly GLWpfControl _control;
    private bool _contextReady;
    private bool _animating;
    private int _framesOwed;

    public GlHost(GLWpfControl control)
    {
        _control = control;
        _control.Render += OnRender;
    }

    /// <summary>Raised once, inside the first frame, with "vendor | renderer | version".</summary>
    public event Action<string>? ContextReady;

    /// <summary>Raised each frame with the elapsed time and the viewport size in pixels.</summary>
    public event Action<TimeSpan, Size>? Draw;

    /// <summary>Raised once when a frame throws (shader compile error, GL error, ...); drawing then stops.</summary>
    public event Action<Exception>? FrameFailed;

    public Exception? Failure { get; private set; }

    public string GlInfo { get; private set; } = "";

    /// <summary>
    /// When true, each frame waits for the GPU to finish (glFinish) so <see cref="LastDrawMs"/> is the real cost of
    /// the frame rather than the display's refresh interval. Used briefly at startup to classify the GPU.
    /// </summary>
    public bool MeasureDraw { get; set; }

    /// <summary>CPU + GPU time of the last <see cref="Draw"/> in milliseconds; only meaningful while <see cref="MeasureDraw"/>.</summary>
    public double LastDrawMs { get; private set; }

    /// <summary>Render every composition frame while true (e.g. during a camera flight).</summary>
    public bool Animating
    {
        get => _animating;
        set { _animating = value; UpdateContinuous(); }
    }

    /// <summary>Creates the context. Throws if OpenGL 4.1 core is unavailable.</summary>
    public void Start()
    {
        _control.Start(new GLWpfControlSettings
        {
            MajorVersion = 4,
            MinorVersion = 1,
            Profile = ContextProfile.Core,
            RenderContinuously = false,
        });
    }

    /// <summary>
    /// Stops drawing and runs <paramref name="release"/> with the GL context current, so GL objects can be deleted
    /// (call it when the window closes). Does nothing but stop when the context was never created.
    /// </summary>
    public void Shutdown(Action release)
    {
        _control.Render -= OnRender;
        _animating = false;
        _framesOwed = 0;
        if (_control.RenderContinuously) _control.RenderContinuously = false;
        if (!_contextReady || _control.Context is not { } context) return;
        context.MakeCurrent();
        release();
    }

    public void RequestRender()
    {
        _framesOwed = FramesPerRequest;
        UpdateContinuous();
        _control.InvalidateVisual();
    }

    private void UpdateContinuous()
    {
        bool on = Failure == null && (_animating || _framesOwed > 0);
        if (_control.RenderContinuously != on) _control.RenderContinuously = on;
    }

    private void OnRender(TimeSpan delta)
    {
        if (Failure != null) return;
        try
        {
            if (!_contextReady)
            {
                _contextReady = true;
                GlInfo = $"{GL.GetString(StringName.Vendor)} | {GL.GetString(StringName.Renderer)} | " +
                         $"{GL.GetString(StringName.Version)}";
                ContextReady?.Invoke(GlInfo);
            }
            int w = _control.FrameBufferWidth, h = _control.FrameBufferHeight;
            GL.Viewport(0, 0, w, h);
            GL.ClearColor(0.01f, 0.01f, 0.03f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Disable(EnableCap.DepthTest);
            if (w > 0 && h > 0)
            {
                long t0 = Stopwatch.GetTimestamp();
                Draw?.Invoke(delta, new Size(w, h));
                if (MeasureDraw)
                {
                    GL.Finish();
                    LastDrawMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                }
            }
            var error = GL.GetError();
            if (error != OpenTK.Graphics.OpenGL4.ErrorCode.NoError)
                throw new InvalidOperationException($"OpenGL error {error}.");
        }
        catch (Exception ex)
        {
            Failure = ex;
            FrameFailed?.Invoke(ex);
        }
        if (_framesOwed > 0) _framesOwed--;
        UpdateContinuous();
    }
}
