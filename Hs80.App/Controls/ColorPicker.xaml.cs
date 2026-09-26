using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace Hs80.App.Controls;

public partial class ColorPicker : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor), typeof(Color), typeof(ColorPicker),
        new PropertyMetadata(Color.FromRgb(245, 166, 35), OnColorChanged));

    private static readonly (string Name, Color Color)[] Palette =
    {
        ("amber", Color.FromRgb(0xF5, 0xA6, 0x23)),
        ("red", Color.FromRgb(0xE7, 0x4C, 0x3C)),
        ("orange", Color.FromRgb(0xE6, 0x7E, 0x22)),
        ("yellow", Color.FromRgb(0xF1, 0xC4, 0x0F)),
        ("green", Color.FromRgb(0x2E, 0xCC, 0x71)),
        ("teal", Color.FromRgb(0x1A, 0xBC, 0x9C)),
        ("cyan", Color.FromRgb(0x34, 0x98, 0xDB)),
        ("blue", Color.FromRgb(0x29, 0x80, 0xB9)),
        ("violet", Color.FromRgb(0x9B, 0x59, 0xB6)),
        ("pink", Color.FromRgb(0xE9, 0x1E, 0x63)),
        ("white", Color.FromRgb(0xFF, 0xFF, 0xFF)),
        ("grey", Color.FromRgb(0xBD, 0xC3, 0xC7)),
        ("slate", Color.FromRgb(0x7F, 0x8C, 0x8D)),
        ("navy", Color.FromRgb(0x34, 0x49, 0x5E)),
        ("near-black", Color.FromRgb(0x15, 0x15, 0x15)),
        ("black", Color.FromRgb(0x00, 0x00, 0x00)),
    };

    private bool _syncing;
    private Button? _selectedSwatch;

    public event Action? ColorCommitted;

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ColorPicker()
    {
        InitializeComponent();
        BuildPalette();
        SyncFromColor();
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ColorPicker)d).SyncFromColor();
    }

    private void BuildPalette()
    {
        var style = (Style)FindResource("SwatchButton");
        var swatches = new Button[Palette.Length];
        for (var i = 0; i < Palette.Length; i++)
        {
            var swatch = new Button
            {
                Style = style,
                Background = new SolidColorBrush(Palette[i].Color),
                BorderBrush = (Brush)FindResource("HairlineBrush"),
                Tag = Palette[i].Color,
            };
            var captured = swatch;
            swatch.Click += (_, _) => CommitColor((Color)captured.Tag);
            swatches[i] = swatch;
            PalettePanel.Children.Add(swatch);
        }
        _selectedSwatch = swatches[0];
        UpdateSwatchHighlight();
    }

    private void OnHexChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        var text = HexBox.Text.Trim().TrimStart('#');
        if (text.Length != 6) return;
        if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) return;
        CommitColor(Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value));
    }

    private void CommitColor(Color color)
    {
        SelectedColor = color;
        ColorCommitted?.Invoke();
    }

    private void SyncFromColor()
    {
        var c = SelectedColor;
        _syncing = true;
        HexBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        SwatchBox.Background = new SolidColorBrush(c);
        _syncing = false;
        var target = PalettePanel.Children.Count > 0 && PalettePanel.Children[0] is Button
            ? PalettePanel.Children.Cast<Button>().FirstOrDefault(b => b.Tag is Color col && col == c)
            : null;
        _selectedSwatch = target ?? _selectedSwatch;
        UpdateSwatchHighlight();
    }

    private void UpdateSwatchHighlight()
    {
        foreach (var child in PalettePanel.Children)
        {
            if (child is not Button b) continue;
            b.BorderBrush = ReferenceEquals(b, _selectedSwatch)
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("HairlineBrush");
        }
    }
}
