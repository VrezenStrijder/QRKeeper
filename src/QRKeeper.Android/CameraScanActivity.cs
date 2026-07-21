using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using QRKeeper.Infrastructure.Services;
using ZXing;
using ZXing.Common;
using Camera = Android.Hardware.Camera;
using CameraFacing = Android.Hardware.CameraFacing;
using ZXingResult = ZXing.Result;

#pragma warning disable CS0618

namespace QRKeeper.Android;

/// <summary>
/// Hosts Android camera preview and returns the decoded QR content.
/// </summary>
[Activity(
    Label = "Scan QR",
    Theme = "@style/MyTheme.NoActionBar",
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class CameraScanActivity : Activity, Camera.IPreviewCallback
{
    public const string ResultExtra = "qrkeeper.scan.result";
    public const string PromptTextExtra = "qrkeeper.scan.prompt";
    public const string ScanningTextExtra = "qrkeeper.scan.scanning";
    public const string CancelTextExtra = "qrkeeper.scan.cancel";

    private const int CameraPermissionRequestCode = 4301;
    private const long DecodeFrameIntervalMilliseconds = 90;
    private const long AutoFocusIntervalMilliseconds = 1400;
    private const int StylizedFallbackFrameInterval = 10;
    private const double StylizedFallbackCropRatio = 0.92;
    private const int StylizedFallbackChromaThreshold = 52;
    private static readonly int[] StylizedFallbackLumaThresholds = [180, 200, 220, 235, 245];
    private readonly MultiFormatReader reader = new();
    private Camera? camera;
    private TextureView? preview;
    private TextView? status;
    private int isDecoding; // Keeps frame decode work from stacking up.
    private volatile bool hasCompleted;
    private bool shouldTriggerAutoFocus;
    private int stylizedFallbackFrameCount;
    private int previewWidth;
    private int previewHeight;
    private long lastDecodeTicks;
    private long lastAutoFocusTicks;
    private string promptText = "Point the camera at a QR code.";
    private string scanningText = "Scanning...";
    private string cancelText = "Cancel";

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        promptText = Intent?.GetStringExtra(PromptTextExtra) ?? promptText;
        scanningText = Intent?.GetStringExtra(ScanningTextExtra) ?? scanningText;
        cancelText = Intent?.GetStringExtra(CancelTextExtra) ?? cancelText;

        reader.Hints = new Dictionary<DecodeHintType, object>
        {
            [DecodeHintType.POSSIBLE_FORMATS] = new[] { BarcodeFormat.QR_CODE },
            [DecodeHintType.CHARACTER_SET] = "UTF-8",
            [DecodeHintType.TRY_HARDER] = true
        };

        SetContentView(CreateContentView());
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M &&
            CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted)
        {
            RequestPermissions([Manifest.Permission.Camera], CameraPermissionRequestCode);
        }
    }

    /// <inheritdoc />
    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == CameraPermissionRequestCode &&
            (grantResults.Length == 0 || grantResults[0] != Permission.Granted))
        {
            FinishCanceled();
            return;
        }

        if (requestCode == CameraPermissionRequestCode &&
            preview?.IsAvailable == true &&
            preview.SurfaceTexture is not null)
        {
            OpenCamera(preview.SurfaceTexture);
        }
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        if (preview?.IsAvailable == true && preview.SurfaceTexture is not null)
        {
            OpenCamera(preview.SurfaceTexture);
        }
    }

    /// <inheritdoc />
    protected override void OnPause()
    {
        CloseCamera();
        base.OnPause();
    }

    /// <inheritdoc />
    public void OnPreviewFrame(byte[]? data, Camera? activeCamera)
    {
        if (data is null || activeCamera is null || hasCompleted)
        {
            return;
        }

        TryTriggerAutoFocus(activeCamera);
        long now = SystemClock.ElapsedRealtime();
        if (now - lastDecodeTicks < DecodeFrameIntervalMilliseconds)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref isDecoding, 1, 0) != 0)
        {
            return;
        }

        lastDecodeTicks = now;
        int width = previewWidth;
        int height = previewHeight;
        if (width <= 0 || height <= 0 || data.Length < width * height)
        {
            Interlocked.Exchange(ref isDecoding, 0);
            return;
        }

        byte[] frame = new byte[data.Length];
        Buffer.BlockCopy(data, 0, frame, 0, data.Length);
        _ = Task.Run(() => DecodeFrameInBackground(frame, width, height));
    }

    private void DecodeFrameInBackground(byte[] data, int width, int height)
    {
        try
        {
            ZXingResult? result = DecodeFrame(data, width, height);
            string? content = result?.Text ?? TryDecodeStylizedFrame(data, width, height);
            if (!string.IsNullOrWhiteSpace(content))
            {
                RunOnUiThread(() => FinishWithResult(content));
            }
        }
        catch (ReaderException)
        {
            reader.reset();
        }
        catch (System.Exception)
        {
            reader.reset();
        }
        finally
        {
            Interlocked.Exchange(ref isDecoding, 0);
        }
    }

    private View CreateContentView()
    {
        LinearLayout root = new(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };

        status = new TextView(this)
        {
            Text = promptText,
            Gravity = GravityFlags.Center,
            TextSize = 16
        };
        root.AddView(status, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        FrameLayout previewHost = new(this);
        preview = new TextureView(this);
        preview.SurfaceTextureListener = new CameraPreviewTextureListener(this);
        previewHost.AddView(preview, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));
        previewHost.AddView(new ScanFrameView(this), new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));
        root.AddView(previewHost, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            0,
            1));

        Button cancel = new(this)
        {
            Text = cancelText
        };
        cancel.Click += (_, _) => FinishCanceled();
        root.AddView(cancel, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        return root;
    }

    private void OpenCamera(SurfaceTexture surfaceTexture)
    {
        if (camera is not null || hasCompleted)
        {
            return;
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.M &&
            CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted)
        {
            return;
        }

        try
        {
            camera = OpenPreferredCamera();
            if (camera is null)
            {
                throw new Java.Lang.RuntimeException("Unable to open camera.");
            }

            Camera.Parameters? parameters = camera.GetParameters();
            Camera.Size? previewSize = ChoosePreviewSize(parameters?.SupportedPreviewSizes);
            if (parameters is not null && previewSize is not null)
            {
                parameters.SetPreviewSize(previewSize.Width, previewSize.Height);
                shouldTriggerAutoFocus = ConfigureFocusMode(parameters);
                ConfigureFocusAndMeteringAreas(parameters);
                ConfigureSceneMode(parameters);

                if (parameters.IsZoomSupported && parameters.MaxZoom > 0)
                {
                    parameters.Zoom = 0;
                }

                camera.SetParameters(parameters);
                previewWidth = previewSize.Width;
                previewHeight = previewSize.Height;
            }

            camera.SetDisplayOrientation(90);
            camera.SetPreviewTexture(surfaceTexture);
            camera.SetPreviewCallback(this);
            camera.StartPreview();
            if (status is not null)
            {
                status.Text = scanningText;
            }
        }
        catch (System.Exception ex) when (ex is Java.Lang.RuntimeException or IOException)
        {
            if (status is not null)
            {
                status.Text = ex.Message;
            }

            CloseCamera();
        }
    }

    private void CloseCamera()
    {
        Camera? activeCamera = camera;
        camera = null;
        previewWidth = 0;
        previewHeight = 0;
        if (activeCamera is null)
        {
            return;
        }

        try
        {
            activeCamera.SetPreviewCallback(null);
        }
        catch (Java.Lang.RuntimeException)
        {
        }

        try
        {
            activeCamera.StopPreview();
        }
        catch (Java.Lang.RuntimeException)
        {
        }

        try
        {
            activeCamera.Release();
        }
        catch (Java.Lang.RuntimeException)
        {
        }
    }

    private void FinishWithResult(string content)
    {
        if (hasCompleted)
        {
            return;
        }

        hasCompleted = true;
        CloseCamera();
        Intent result = new();
        result.PutExtra(ResultExtra, content);
        SetResult(global::Android.App.Result.Ok, result);
        Finish();
    }

    private void FinishCanceled()
    {
        if (hasCompleted)
        {
            return;
        }

        hasCompleted = true;
        CloseCamera();
        SetResult(global::Android.App.Result.Canceled);
        Finish();
    }

    private static Camera? OpenPreferredCamera()
    {
        for (int index = 0; index < Camera.NumberOfCameras; index++)
        {
            Camera.CameraInfo cameraInfo = new();
            Camera.GetCameraInfo(index, cameraInfo);
            if (cameraInfo.Facing == CameraFacing.Back)
            {
                return Camera.Open(index);
            }
        }

        return Camera.Open();
    }

    private ZXingResult? DecodeFrame(byte[] data, int width, int height)
    {
        PlanarYUVLuminanceSource fullFrame = new(
            data,
            width,
            height,
            0,
            0,
            width,
            height,
            false);

        ZXingResult? result = DecodeSource(fullFrame);
        if (result is not null)
        {
            return result;
        }

        int cropWidth = width * 3 / 4;
        int cropHeight = height * 3 / 4;
        PlanarYUVLuminanceSource centerFrame = new(
            data,
            width,
            height,
            (width - cropWidth) / 2,
            (height - cropHeight) / 2,
            cropWidth,
            cropHeight,
            false);

        return DecodeSource(centerFrame);
    }

    private string? TryDecodeStylizedFrame(byte[] data, int width, int height)
    {
        stylizedFallbackFrameCount++;
        if (stylizedFallbackFrameCount % StylizedFallbackFrameInterval != 0)
        {
            return null;
        }

        int pixelCount = width * height;
        if (data.Length < pixelCount)
        {
            return null;
        }

        int cropSize = Math.Max(1, (int)Math.Round(Math.Min(width, height) * StylizedFallbackCropRatio));
        int cropLeft = Math.Max(0, (width - cropSize) / 2);
        int cropTop = Math.Max(0, (height - cropSize) / 2);
        return QRCodeService.TryDecodeStylizedDotQrFromNv21(
            data,
            width,
            height,
            cropLeft,
            cropTop,
            cropSize,
            cropSize,
            StylizedFallbackLumaThresholds,
            StylizedFallbackChromaThreshold)?.Text;
    }

    private ZXingResult? DecodeSource(LuminanceSource source)
    {
        try
        {
            return reader.decodeWithState(new BinaryBitmap(new HybridBinarizer(source)));
        }
        catch (ReaderException)
        {
            reader.reset();
        }

        try
        {
            return reader.decodeWithState(new BinaryBitmap(new GlobalHistogramBinarizer(source)));
        }
        catch (ReaderException)
        {
            reader.reset();
            return null;
        }
    }

    private void TryTriggerAutoFocus(Camera activeCamera)
    {
        if (!shouldTriggerAutoFocus)
        {
            return;
        }

        long now = SystemClock.ElapsedRealtime();
        if (now - lastAutoFocusTicks < AutoFocusIntervalMilliseconds)
        {
            return;
        }

        lastAutoFocusTicks = now;
        try
        {
            activeCamera.AutoFocus(null);
        }
        catch (Java.Lang.RuntimeException)
        {
        }
    }

    private static bool ConfigureFocusMode(Camera.Parameters parameters)
    {
        if (parameters.SupportedFocusModes?.Contains(Camera.Parameters.FocusModeContinuousVideo) == true)
        {
            parameters.FocusMode = Camera.Parameters.FocusModeContinuousVideo;
            return false;
        }

        if (parameters.SupportedFocusModes?.Contains(Camera.Parameters.FocusModeContinuousPicture) == true)
        {
            parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            return false;
        }

        if (parameters.SupportedFocusModes?.Contains(Camera.Parameters.FocusModeAuto) == true)
        {
            parameters.FocusMode = Camera.Parameters.FocusModeAuto;
            return true;
        }

        return false;
    }

    private static void ConfigureFocusAndMeteringAreas(Camera.Parameters parameters)
    {
        List<Camera.Area> centerArea = [new(new Rect(-450, -450, 450, 450), 800)];
        if (parameters.MaxNumFocusAreas > 0)
        {
            parameters.FocusAreas = centerArea;
        }

        if (parameters.MaxNumMeteringAreas > 0)
        {
            parameters.MeteringAreas = centerArea;
        }
    }

    private static void ConfigureSceneMode(Camera.Parameters parameters)
    {
        if (parameters.SupportedSceneModes?.Contains(Camera.Parameters.SceneModeBarcode) == true)
        {
            parameters.SceneMode = Camera.Parameters.SceneModeBarcode;
        }
    }

    private static Camera.Size? ChoosePreviewSize(IList<Camera.Size>? supportedPreviewSizes)
    {
        if (supportedPreviewSizes is null || supportedPreviewSizes.Count == 0)
        {
            return null;
        }

        return supportedPreviewSizes
            .Where(size => size.Width >= 1280 && size.Height >= 720)
            .OrderBy(size => Math.Abs(size.Width * size.Height - 1920 * 1080))
            .FirstOrDefault()
            ?? supportedPreviewSizes
                .OrderByDescending(size => size.Width * size.Height)
                .FirstOrDefault();
    }

    private sealed class CameraPreviewTextureListener : Java.Lang.Object, TextureView.ISurfaceTextureListener
    {
        private readonly CameraScanActivity activity;

        public CameraPreviewTextureListener(CameraScanActivity activity)
        {
            this.activity = activity;
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            activity.OpenCamera(surface);
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            activity.CloseCamera();
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
        }
    }

    private sealed class ScanFrameView : View
    {
        private readonly Paint framePaint = new(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 255, 255, 255),
            StrokeWidth = 3
        };

        private readonly Paint cornerPaint = new(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(255, 47, 111, 237),
            StrokeWidth = 8,
            StrokeCap = Paint.Cap.Round
        };

        public ScanFrameView(Context context) : base(context)
        {
            framePaint.SetStyle(Paint.Style.Stroke);
            cornerPaint.SetStyle(Paint.Style.Stroke);
            SetWillNotDraw(false);
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            float frameSize = Math.Min(Width, Height) * 0.68f;
            float left = (Width - frameSize) / 2f;
            float top = (Height - frameSize) / 2f;
            float right = left + frameSize;
            float bottom = top + frameSize;
            RectF frame = new(left, top, right, bottom);
            canvas.DrawRoundRect(frame, 28, 28, framePaint);

            float cornerLength = frameSize * 0.18f;
            canvas.DrawLine(left, top, left + cornerLength, top, cornerPaint);
            canvas.DrawLine(left, top, left, top + cornerLength, cornerPaint);
            canvas.DrawLine(right, top, right - cornerLength, top, cornerPaint);
            canvas.DrawLine(right, top, right, top + cornerLength, cornerPaint);
            canvas.DrawLine(left, bottom, left + cornerLength, bottom, cornerPaint);
            canvas.DrawLine(left, bottom, left, bottom - cornerLength, cornerPaint);
            canvas.DrawLine(right, bottom, right - cornerLength, bottom, cornerPaint);
            canvas.DrawLine(right, bottom, right, bottom - cornerLength, cornerPaint);
        }
    }
}
