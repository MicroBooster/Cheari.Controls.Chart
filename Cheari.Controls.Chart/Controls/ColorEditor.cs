using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cheari.Controls.Controls;

public class ColorEditor : Control
{
    private Canvas? _spectrumCanvas;
    private Canvas? _hueBarCanvas;
    private Image? _spectrumImage;
    private Image? _hueBarImage;
    private Slider? _alphaSlider;
    private TextBox? _hexBox;
    private TextBox? _rBox, _gBox, _bBox, _aBox;
    private Border? _newPreview;
    private Border? _oldPreview;
    private ItemsControl? _presetsControl;
    private Button? _okButton;
    private Button? _cancelButton;

    private bool _updatingFromCode;

    private double _hue = 0;
    private double _saturation = 1;
    private double _value = 1;

    static ColorEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ColorEditor),
            new FrameworkPropertyMetadata(typeof(ColorEditor)));
    }

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorEditor),
            new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public static readonly DependencyProperty PreviousColorProperty =
        DependencyProperty.Register(nameof(PreviousColor), typeof(Color), typeof(ColorEditor),
            new FrameworkPropertyMetadata(Colors.Red));

    public static readonly DependencyProperty ShowAlphaProperty =
        DependencyProperty.Register(nameof(ShowAlpha), typeof(bool), typeof(ColorEditor),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty ShowPresetsProperty =
        DependencyProperty.Register(nameof(ShowPresets), typeof(bool), typeof(ColorEditor),
            new FrameworkPropertyMetadata(true));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public Color PreviousColor
    {
        get => (Color)GetValue(PreviousColorProperty);
        set => SetValue(PreviousColorProperty, value);
    }

    public bool ShowAlpha
    {
        get => (bool)GetValue(ShowAlphaProperty);
        set => SetValue(ShowAlphaProperty, value);
    }

    public bool ShowPresets
    {
        get => (bool)GetValue(ShowPresetsProperty);
        set => SetValue(ShowPresetsProperty, value);
    }

    public Action<bool>? OnClose { get; set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _spectrumCanvas = GetTemplateChild("PART_SpectrumCanvas") as Canvas;
        _hueBarCanvas = GetTemplateChild("PART_HueBarCanvas") as Canvas;
        _spectrumImage = GetTemplateChild("PART_SpectrumImage") as Image;
        _hueBarImage = GetTemplateChild("PART_HueBarImage") as Image;
        _alphaSlider = GetTemplateChild("PART_AlphaSlider") as Slider;
        _hexBox = GetTemplateChild("PART_HexBox") as TextBox;
        _rBox = GetTemplateChild("PART_RBox") as TextBox;
        _gBox = GetTemplateChild("PART_GBox") as TextBox;
        _bBox = GetTemplateChild("PART_BBox") as TextBox;
        _aBox = GetTemplateChild("PART_ABox") as TextBox;
        _newPreview = GetTemplateChild("PART_NewPreview") as Border;
        _oldPreview = GetTemplateChild("PART_OldPreview") as Border;
        _presetsControl = GetTemplateChild("PART_Presets") as ItemsControl;
        _okButton = GetTemplateChild("PART_OkButton") as Button;
        _cancelButton = GetTemplateChild("PART_CancelButton") as Button;

        if (_spectrumCanvas != null)
        {
            _spectrumCanvas.MouseLeftButtonDown += OnSpectrumMouseDown;
            _spectrumCanvas.MouseMove += OnSpectrumMouseMove;
            _spectrumCanvas.MouseLeftButtonUp += OnSpectrumMouseUp;
        }

        if (_hueBarCanvas != null)
        {
            _hueBarCanvas.MouseLeftButtonDown += OnHueMouseDown;
            _hueBarCanvas.MouseMove += OnHueMouseMove;
            _hueBarCanvas.MouseLeftButtonUp += OnHueMouseUp;
        }

        if (_alphaSlider != null)
            _alphaSlider.ValueChanged += OnAlphaSliderChanged;

        if (_hexBox != null)
        {
            _hexBox.LostFocus += OnHexBoxLostFocus;
            _hexBox.KeyDown += OnHexBoxKeyDown;
        }

        if (_rBox != null) _rBox.LostFocus += OnRgbaBoxLostFocus;
        if (_gBox != null) _gBox.LostFocus += OnRgbaBoxLostFocus;
        if (_bBox != null) _bBox.LostFocus += OnRgbaBoxLostFocus;
        if (_aBox != null) _aBox.LostFocus += OnRgbaBoxLostFocus;

        if (_presetsControl != null)
        {
            _presetsControl.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnPresetButtonClick));
            PopulatePresets();
        }

        if (_okButton != null)
            _okButton.Click += (_, _) => OnClose?.Invoke(true);

        if (_cancelButton != null)
            _cancelButton.Click += (_, _) =>
            {
                _updatingFromCode = true;
                SelectedColor = PreviousColor;
                _updatingFromCode = false;
                OnClose?.Invoke(false);
            };

        UpdateHsvFromColor(SelectedColor);
        RenderSpectrum();
        RenderHueBar();
        UpdateAllVisuals();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorEditor editor && !editor._updatingFromCode)
        {
            editor.UpdateHsvFromColor((Color)e.NewValue);
            editor.UpdateAllVisuals();
        }
    }

    private void UpdateHsvFromColor(Color color)
    {
        ColorToHsv(color, out _hue, out _saturation, out _value);
    }

    private static void ColorToHsv(Color color, out double h, out double s, out double v)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0)
        {
            h = 0;
        }
        else if (max == r)
        {
            h = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            h = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            h = 60 * (((r - g) / delta) + 4);
        }

        if (h < 0) h += 360;
    }

    private static Color HsvToColor(double h, double s, double v, byte alpha = 255)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromArgb(alpha,
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255));
    }

    private void RenderSpectrum()
    {
        if (_spectrumImage == null) return;
        int w = (int)_spectrumImage.Width;
        int h = (int)_spectrumImage.Height;
        if (w <= 0 || h <= 0) return;

        var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[w * h * 4];

        for (int y = 0; y < h; y++)
        {
            double v = 1.0 - (double)y / h;
            for (int x = 0; x < w; x++)
            {
                double s = (double)x / w;
                var color = HsvToColor(_hue, s, v);
                int idx = (y * w + x) * 4;
                pixels[idx] = color.B;
                pixels[idx + 1] = color.G;
                pixels[idx + 2] = color.R;
                pixels[idx + 3] = 255;
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
        _spectrumImage.Source = bitmap;
    }

    private void RenderHueBar()
    {
        if (_hueBarImage == null) return;
        int w = (int)_hueBarImage.Width;
        int h = (int)_hueBarImage.Height;
        if (w <= 0 || h <= 0) return;

        var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[w * h * 4];

        for (int y = 0; y < h; y++)
        {
            double hue = 360.0 * (1.0 - (double)y / h);
            var color = HsvToColor(hue, 1, 1);
            for (int x = 0; x < w; x++)
            {
                int idx = (y * w + x) * 4;
                pixels[idx] = color.B;
                pixels[idx + 1] = color.G;
                pixels[idx + 2] = color.R;
                pixels[idx + 3] = 255;
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
        _hueBarImage.Source = bitmap;
    }

    private bool _spectrumCaptured;
    private bool _hueCaptured;

    private void OnSpectrumMouseDown(object sender, MouseButtonEventArgs e)
    {
        _spectrumCaptured = true;
        (_spectrumCanvas ?? (UIElement)sender).CaptureMouse();
        UpdateSpectrumFromMouse(e.GetPosition(_spectrumCanvas));
    }

    private void OnSpectrumMouseMove(object sender, MouseEventArgs e)
    {
        if (_spectrumCaptured)
            UpdateSpectrumFromMouse(e.GetPosition(_spectrumCanvas));
    }

    private void OnSpectrumMouseUp(object sender, MouseButtonEventArgs e)
    {
        _spectrumCaptured = false;
        (_spectrumCanvas ?? (UIElement)sender).ReleaseMouseCapture();
    }

    private void UpdateSpectrumFromMouse(Point pos)
    {
        if (_spectrumImage == null) return;
        double w = _spectrumImage.Width;
        double h = _spectrumImage.Height;
        _saturation = Math.Clamp(pos.X / w, 0, 1);
        _value = Math.Clamp(1.0 - pos.Y / h, 0, 1);
        ApplyHsvToColor();
    }

    private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
    {
        _hueCaptured = true;
        (_hueBarCanvas ?? (UIElement)sender).CaptureMouse();
        UpdateHueFromMouse(e.GetPosition(_hueBarCanvas));
    }

    private void OnHueMouseMove(object sender, MouseEventArgs e)
    {
        if (_hueCaptured)
            UpdateHueFromMouse(e.GetPosition(_hueBarCanvas));
    }

    private void OnHueMouseUp(object sender, MouseButtonEventArgs e)
    {
        _hueCaptured = false;
        (_hueBarCanvas ?? (UIElement)sender).ReleaseMouseCapture();
    }

    private void UpdateHueFromMouse(Point pos)
    {
        if (_hueBarImage == null) return;
        double h = _hueBarImage.Height;
        _hue = Math.Clamp(360.0 * (1.0 - pos.Y / h), 0, 360);
        RenderSpectrum();
        ApplyHsvToColor();
    }

    private void ApplyHsvToColor()
    {
        var color = HsvToColor(_hue, _saturation, _value,
            _alphaSlider != null ? (byte)Math.Clamp((int)_alphaSlider.Value, 0, 255) : (byte)255);
        _updatingFromCode = true;
        SelectedColor = color;
        _updatingFromCode = false;
        UpdateAllVisuals();
    }

    private void OnAlphaSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingFromCode) return;
        var c = SelectedColor;
        _updatingFromCode = true;
        SelectedColor = Color.FromArgb((byte)Math.Clamp((int)e.NewValue, 0, 255), c.R, c.G, c.B);
        _updatingFromCode = false;
        UpdateAllVisuals();
    }

    private void OnHexBoxLostFocus(object sender, RoutedEventArgs e) => ApplyHex();
    private void OnHexBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyHex();
    }

    private void ApplyHex()
    {
        if (_hexBox == null) return;
        var hex = _hexBox.Text.Trim().TrimStart('#');
        try
        {
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                _updatingFromCode = true;
                SelectedColor = Color.FromArgb(SelectedColor.A, r, g, b);
                _updatingFromCode = false;
                UpdateHsvFromColor(SelectedColor);
                RenderSpectrum();
                UpdateAllVisuals();
            }
            else if (hex.Length == 8)
            {
                byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                _updatingFromCode = true;
                SelectedColor = Color.FromArgb(a, r, g, b);
                _updatingFromCode = false;
                UpdateHsvFromColor(SelectedColor);
                RenderSpectrum();
                UpdateAllVisuals();
            }
        }
        catch
        {
            UpdateAllVisuals();
        }
    }

    private void OnRgbaBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingFromCode || _rBox == null || _gBox == null || _bBox == null) return;
        try
        {
            byte r = byte.TryParse(_rBox.Text, out var rv) ? rv : SelectedColor.R;
            byte g = byte.TryParse(_gBox.Text, out var gv) ? gv : SelectedColor.G;
            byte b = byte.TryParse(_bBox.Text, out var bv) ? bv : SelectedColor.B;
            byte a = SelectedColor.A;
            if (_aBox != null && byte.TryParse(_aBox.Text, out var av)) a = av;
            _updatingFromCode = true;
            SelectedColor = Color.FromArgb(a, r, g, b);
            _updatingFromCode = false;
            UpdateHsvFromColor(SelectedColor);
            RenderSpectrum();
            UpdateAllVisuals();
        }
        catch
        {
            UpdateAllVisuals();
        }
    }

    private void UpdateAllVisuals()
    {
        var c = SelectedColor;

        if (_alphaSlider != null && !_updatingFromCode)
        {
            _updatingFromCode = true;
            _alphaSlider.Value = c.A;
            _updatingFromCode = false;
        }

        if (_hexBox != null && !_hexBox.IsFocused)
        {
            _hexBox.Text = c.A == 255
                ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        if (_rBox != null && !_rBox.IsFocused) _rBox.Text = c.R.ToString();
        if (_gBox != null && !_gBox.IsFocused) _gBox.Text = c.G.ToString();
        if (_bBox != null && !_bBox.IsFocused) _bBox.Text = c.B.ToString();
        if (_aBox != null && !_aBox.IsFocused) _aBox.Text = c.A.ToString();

        if (_newPreview != null)
            _newPreview.Background = new SolidColorBrush(c);
        if (_oldPreview != null)
            _oldPreview.Background = new SolidColorBrush(PreviousColor);
    }

    private void PopulatePresets()
    {
        if (_presetsControl == null) return;

        var presetColors = new[]
        {
            Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Cyan, Colors.Blue,
            Colors.Purple, Colors.Pink, Colors.White, Colors.Gray, Colors.LightBlue, Colors.LightGreen,
            Colors.DarkRed, Colors.DarkOrange, Color.FromRgb(139, 119, 0), Colors.DarkGreen,
            Colors.DarkCyan, Colors.DarkBlue, Color.FromRgb(148, 0, 211),
            Color.FromRgb(238, 18, 137), Colors.Black, Colors.LightGray, Colors.SkyBlue, Colors.LimeGreen
        };

        _presetsControl.Items.Clear();
        foreach (var color in presetColors)
        {
            _presetsControl.Items.Add(color);
        }
    }

    private void OnPresetButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button button && button.DataContext is Color color)
        {
            _updatingFromCode = true;
            SelectedColor = Color.FromArgb(SelectedColor.A, color.R, color.G, color.B);
            _updatingFromCode = false;
            UpdateHsvFromColor(SelectedColor);
            RenderSpectrum();
            UpdateAllVisuals();
        }
    }

    public static readonly RoutedEvent ColorSelectedEvent =
        EventManager.RegisterRoutedEvent(nameof(ColorSelected), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(ColorEditor));

    public event RoutedEventHandler ColorSelected
    {
        add => AddHandler(ColorSelectedEvent, value);
        remove => RemoveHandler(ColorSelectedEvent, value);
    }
}
