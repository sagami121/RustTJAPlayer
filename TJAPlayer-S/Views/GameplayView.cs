using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System;
using TjaPlayer;
using TjaPlayer.Audio;
using TjaPlayer.Gameplay;
using TjaPlayer.Models;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace TjaPlayer.Views;

public class GameplayView : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.Playing;

    private readonly AudioManager audioManager;
    private readonly ScoringSystem scoringSystem;
    private readonly JudgmentSystem judgmentSystem;
    private readonly int audioStream;
    private readonly Tja chart;
    private List<Chip> activeChips = new();

    // SlimDX resources
    private SlimDX.Direct3D11.Device? _device;
    private SwapChain? _swapChain;
    private RenderTargetView? _renderTargetView;

    public GameplayView(Tja chart, AudioManager audioManager)
    {
        this.chart = chart;
        this.audioManager = audioManager;
        this.scoringSystem = new ScoringSystem();
        this.judgmentSystem = new JudgmentSystem();
        this.audioStream = audioManager.PlayTrack(chart.BgmPath);
        Dock = DockStyle.Fill;

        InitializeChips();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        InitializeDevice();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DisposeDevice();
        base.OnHandleDestroyed(e);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ResizeDevice();
    }

    private void InitializeDevice()
    {
        if (_device != null) return;

        var format = Format.R8G8B8A8_UNorm;
        var desc = new SwapChainDescription()
        {
            BufferCount = 1,
            Usage = Usage.RenderTargetOutput,
            OutputHandle = Handle,
            IsWindowed = true,
            ModeDescription = new ModeDescription(ClientSize.Width, ClientSize.Height, new Rational(60, 1), format),
            SampleDescription = new SampleDescription(1, 0),
            Flags = SwapChainFlags.AllowModeSwitch
        };

        SlimDX.Direct3D11.Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport, desc, out _device, out _swapChain);

        // Ignore all interfaces except 11
        using var resource = _swapChain.GetBackBuffer<Texture2D>(0);
        _renderTargetView = new RenderTargetView(_device, resource);

        _device.ImmediateContext.OutputMerger.SetRenderTargets(_renderTargetView);

        var viewport = new Viewport(0, 0, ClientSize.Width, ClientSize.Height, 0.0f, 1.0f);
        _device.ImmediateContext.Rasterizer.SetViewports(viewport);
    }

    private void DisposeDevice()
    {
        _renderTargetView?.Dispose();
        _swapChain?.Dispose();
        _device?.Dispose();

        _renderTargetView = null;
        _swapChain = null;
        _device = null;
    }

    private void ResizeDevice()
    {
        if (_device == null) return;

        // Release render target view before resizing
        _renderTargetView?.Dispose();
        _renderTargetView = null;

        // Resize swap chain buffers
        _swapChain.ResizeBuffers(1, ClientSize.Width, ClientSize.Height, Format.R8G8B8A8_UNorm, SwapChainFlags.AllowModeSwitch);

        // Recreate render target view
        using var resource = _swapChain.GetBackBuffer<Texture2D>(0);
        _renderTargetView = new RenderTargetView(_device, resource);
        _device.ImmediateContext.OutputMerger.SetRenderTargets(_renderTargetView);

        // Update viewport
        var viewport = new Viewport(0, 0, ClientSize.Width, ClientSize.Height, 0.0f, 1.0f);
        _device.ImmediateContext.Rasterizer.SetViewports(viewport);
    }

    private void InitializeChips()
    {
        // Populate activeChips based on chart data
        // For now, add a few test chips
        activeChips.Add(new Chip { ChannelNo = (int)NoteChannel.Don, TimeMs = 2000 }); // 2 seconds
        activeChips.Add(new Chip { ChannelNo = (int)NoteChannel.Ka, TimeMs = 3000 });
        activeChips.Add(new Chip { ChannelNo = (int)NoteChannel.DonBig, TimeMs = 4000 });
    }

    public new void Update()
    {
        double currentTime = audioManager.GetPositionSeconds(audioStream) * 1000.0; // convert to milliseconds

        foreach (var chip in activeChips)
        {
            if (chip.IsHitted || chip.IsMissed) continue;

            // Check for miss
            if (currentTime - chip.TimeMs > JudgmentSystem.BadWindowMs)
            {
                chip.IsMissed = true;
                scoringSystem.AddScore(Judgment.Miss);
            }
            else
            {
                // Calculate screen X position based on timing
                const double pixelsPerMs = 0.5; // example value
                chip.ScreenX = (float)((currentTime - chip.TimeMs) * pixelsPerMs);
            }
        }
    }

    public void Render()
    {
        if (_device == null) return;

        // Clear the render target to a dark blue
        _device.ImmediateContext.ClearRenderTargetView(_renderTargetView, new Color4(0.1f, 0.2f, 0.4f, 1.0f));

        // Present
        _swapChain.Present(0, PresentFlags.None);
    }
}