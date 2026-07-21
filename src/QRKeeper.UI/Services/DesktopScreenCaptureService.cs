using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace QRKeeper.UI.Services;

public sealed class DesktopScreenCaptureService : IScreenCaptureService
{
    private readonly Func<Window> _ownerAccessor;
    private readonly IQRCodeService _qrCodeService;
    private readonly ILocalizationService _localizationService;

    public DesktopScreenCaptureService(
        Func<Window> ownerAccessor,
        IQRCodeService qrCodeService,
        ILocalizationService localizationService)
    {
        _ownerAccessor = ownerAccessor;
        _qrCodeService = qrCodeService;
        _localizationService = localizationService;
    }

    public async Task<ScreenCaptureResult> CaptureScreenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return ScreenCaptureResult.Empty(_localizationService.GetString("Status_ScreenRecognitionUnsupported"));
        }

        Window owner = _ownerAccessor();
        owner.Hide();
        try
        {
            await Task.Delay(250, cancellationToken);
            DrawingRectangle virtualScreen = GetVirtualScreenBounds();
            await using Stream fullScreen = CaptureWindowsArea(virtualScreen);
            IReadOnlyList<QRCodeDecodeResult> results = await _qrCodeService.DecodeAllAsync(fullScreen, cancellationToken);
            ScreenCandidate[] candidates = results.Select(result => ToScreenCandidate(virtualScreen, result)).ToArray();

            ScreenSelection? selection = await ScreenSelectionWindow.SelectAreaAsync(
                candidates,
                _localizationService.GetString("ScreenSelection_ManualInstruction"),
                _localizationService.GetString("ScreenSelection_CandidateInstruction"),
                cancellationToken);
            if (selection is null || selection.Bounds.Width < 8 || selection.Bounds.Height < 8)
            {
                return ScreenCaptureResult.Empty(_localizationService.GetString("Status_ScreenRecognitionCanceled"));
            }

            if (!string.IsNullOrWhiteSpace(selection.DecodedText))
            {
                return ScreenCaptureResult.FromDecodedText(selection.DecodedText);
            }

            return ScreenCaptureResult.FromStream(CaptureWindowsArea(selection.Bounds));
        }
        finally
        {
            owner.Show();
            owner.Activate();
        }
    }

    [SupportedOSPlatform("windows")]
    private static DrawingRectangle GetVirtualScreenBounds()
    {
        return new DrawingRectangle(
            GetSystemMetrics(SystemMetricVirtualScreenLeft),
            GetSystemMetrics(SystemMetricVirtualScreenTop),
            GetSystemMetrics(SystemMetricVirtualScreenWidth),
            GetSystemMetrics(SystemMetricVirtualScreenHeight));
    }

    [SupportedOSPlatform("windows")]
    private static Stream CaptureWindowsArea(DrawingRectangle area)
    {
        using DrawingBitmap bitmap = new(area.Width, area.Height);
        using DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap);
        graphics.CopyFromScreen(area.Left, area.Top, 0, 0, new DrawingSize(area.Width, area.Height));

        MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return stream;
    }

    private static ScreenCandidate ToScreenCandidate(DrawingRectangle virtualScreen, QRCodeDecodeResult result)
    {
        float span = Math.Max(result.Width, result.Height);
        int padding = Math.Max(64, (int)Math.Ceiling(span * 0.32f));
        int left = virtualScreen.Left + (int)Math.Floor(result.X) - padding;
        int top = virtualScreen.Top + (int)Math.Floor(result.Y) - padding;
        int width = (int)Math.Ceiling(result.Width) + padding * 2;
        int height = (int)Math.Ceiling(result.Height) + padding * 2;

        DrawingRectangle bounds = DrawingRectangle.Intersect(
            new DrawingRectangle(left, top, Math.Max(24, width), Math.Max(24, height)),
            virtualScreen);
        return new ScreenCandidate(bounds, result.Text);
    }

    private const int SystemMetricVirtualScreenLeft = 76;
    private const int SystemMetricVirtualScreenTop = 77;
    private const int SystemMetricVirtualScreenWidth = 78;
    private const int SystemMetricVirtualScreenHeight = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private sealed record ScreenCandidate(DrawingRectangle Bounds, string DecodedText);

    private sealed record ScreenSelection(DrawingRectangle Bounds, string? DecodedText);

    private sealed class ScreenSelectionWindow : Window
    {
        private readonly Canvas _canvas = new();
        private readonly Border _instruction = new();
        private readonly List<CandidateBox> _candidates = new();
        private readonly Avalonia.Controls.Shapes.Rectangle _selection = new()
        {
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(42, 0, 120, 212)),
            IsVisible = false
        };

        private readonly TaskCompletionSource<ScreenSelection?> _completion = new();
        private Avalonia.Point? _startPoint;
        private Avalonia.Point _currentPoint;
        private int _selectedCandidateIndex = -1;

        private ScreenSelectionWindow(
            IReadOnlyList<ScreenCandidate> candidates,
            string manualInstruction,
            string candidateInstruction)
        {
            WindowState = WindowState.FullScreen;
            SystemDecorations = SystemDecorations.None;
            Topmost = true;
            CanResize = false;
            Background = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0));
            Cursor = new Cursor(StandardCursorType.Cross);
            Content = _canvas;

            _instruction.Child = new TextBlock
            {
                Text = candidates.Count == 0
                    ? manualInstruction
                    : candidateInstruction,
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 10)
            };
            _instruction.Background = new SolidColorBrush(Color.FromArgb(190, 24, 24, 24));
            _instruction.CornerRadius = new CornerRadius(6);
            Canvas.SetLeft(_instruction, 28);
            Canvas.SetTop(_instruction, 24);

            _canvas.Children.Add(_instruction);
            foreach (ScreenCandidate candidate in candidates)
            {
                AddCandidate(candidate);
            }

            _canvas.Children.Add(_selection);

            Opened += (_, _) => SelectCandidate(_candidates.Count > 0 ? 0 : -1);
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            KeyDown += OnKeyDown;
            Closed += (_, _) => _completion.TrySetResult(null);
        }

        public static async Task<ScreenSelection?> SelectAreaAsync(
            IReadOnlyList<ScreenCandidate> candidates,
            string manualInstruction,
            string candidateInstruction,
            CancellationToken cancellationToken)
        {
            ScreenSelectionWindow window = new(candidates, manualInstruction, candidateInstruction);
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    window._completion.TrySetCanceled(cancellationToken);
                    window.Close();
                });
            });

            window.Show();
            window.Activate();
            return await window._completion.Task;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            int candidateIndex = FindCandidateIndex(e.GetPosition(this));
            if (candidateIndex >= 0)
            {
                CompleteWithCandidate(candidateIndex);
                return;
            }

            _startPoint = e.GetPosition(this);
            _currentPoint = _startPoint.Value;
            _selection.IsVisible = true;
            UpdateSelection();
            e.Pointer.Capture(this);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_startPoint is null)
            {
                SelectCandidate(FindCandidateIndex(e.GetPosition(this)));
            }

            if (_startPoint is null)
            {
                return;
            }

            _currentPoint = e.GetPosition(this);
            UpdateSelection();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_startPoint is null)
            {
                return;
            }

            _currentPoint = e.GetPosition(this);
            DrawingRectangle selectedArea = ToScreenRectangle(_startPoint.Value, _currentPoint);
            _completion.TrySetResult(new ScreenSelection(selectedArea, null));
            e.Pointer.Capture(null);
            Close();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            _completion.TrySetResult(null);
            Close();
        }

        private void UpdateSelection()
        {
            if (_startPoint is null)
            {
                return;
            }

            double left = Math.Min(_startPoint.Value.X, _currentPoint.X);
            double top = Math.Min(_startPoint.Value.Y, _currentPoint.Y);
            double width = Math.Abs(_currentPoint.X - _startPoint.Value.X);
            double height = Math.Abs(_currentPoint.Y - _startPoint.Value.Y);

            Canvas.SetLeft(_selection, left);
            Canvas.SetTop(_selection, top);
            _selection.Width = width;
            _selection.Height = height;
        }

        private DrawingRectangle ToScreenRectangle(Avalonia.Point first, Avalonia.Point second)
        {
            double scale = RenderScaling;
            int left = Position.X + (int)Math.Round(Math.Min(first.X, second.X) * scale);
            int top = Position.Y + (int)Math.Round(Math.Min(first.Y, second.Y) * scale);
            int width = (int)Math.Round(Math.Abs(second.X - first.X) * scale);
            int height = (int)Math.Round(Math.Abs(second.Y - first.Y) * scale);
            return new DrawingRectangle(new DrawingPoint(left, top), new DrawingSize(width, height));
        }

        private DrawingPoint ToScreenPoint(Avalonia.Point point)
        {
            double scale = RenderScaling;
            return new DrawingPoint(
                Position.X + (int)Math.Round(point.X * scale),
                Position.Y + (int)Math.Round(point.Y * scale));
        }

        private void AddCandidate(ScreenCandidate candidate)
        {
            Avalonia.Controls.Shapes.Rectangle shape = new()
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(24, 0, 120, 212))
            };

            CandidateBox box = new(candidate.Bounds, candidate.DecodedText, shape);
            _candidates.Add(box);
            _canvas.Children.Add(shape);
            UpdateCandidateShape(box, false);
        }

        private void UpdateCandidateShape(CandidateBox candidate, bool isSelected)
        {
            double scale = RenderScaling;
            Canvas.SetLeft(candidate.Shape, (candidate.ScreenBounds.Left - Position.X) / scale);
            Canvas.SetTop(candidate.Shape, (candidate.ScreenBounds.Top - Position.Y) / scale);
            candidate.Shape.Width = candidate.ScreenBounds.Width / scale;
            candidate.Shape.Height = candidate.ScreenBounds.Height / scale;
            candidate.Shape.Stroke = isSelected ? Brushes.Gold : Brushes.DeepSkyBlue;
            candidate.Shape.StrokeThickness = isSelected ? 3 : 2;
            candidate.Shape.Fill = new SolidColorBrush(isSelected
                ? Color.FromArgb(46, 255, 185, 0)
                : Color.FromArgb(24, 0, 120, 212));
        }

        private int FindCandidateIndex(Avalonia.Point point)
        {
            DrawingPoint screenPoint = ToScreenPoint(point);
            for (int index = 0; index < _candidates.Count; index++)
            {
                if (_candidates[index].ScreenBounds.Contains(screenPoint))
                {
                    return index;
                }
            }

            return -1;
        }

        private void SelectCandidate(int index)
        {
            if (_selectedCandidateIndex == index)
            {
                return;
            }

            _selectedCandidateIndex = index;
            for (int candidateIndex = 0; candidateIndex < _candidates.Count; candidateIndex++)
            {
                UpdateCandidateShape(_candidates[candidateIndex], candidateIndex == _selectedCandidateIndex);
            }
        }

        private void CompleteWithCandidate(int index)
        {
            SelectCandidate(index);
            CandidateBox candidate = _candidates[index];
            _completion.TrySetResult(new ScreenSelection(candidate.ScreenBounds, candidate.DecodedText));
            Close();
        }

        private sealed record CandidateBox(
            DrawingRectangle ScreenBounds,
            string DecodedText,
            Avalonia.Controls.Shapes.Rectangle Shape);
    }
}
