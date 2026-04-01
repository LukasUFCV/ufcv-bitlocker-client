using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace BitLockerUFCV.Client.Models;

public sealed class ProgressStepItem
{
    public ProgressStepItem(ProgressStepKind kind, string message)
    {
        Kind = kind;
        Message = message;
        StateLabel = kind switch
        {
            ProgressStepKind.Success => "Succès",
            ProgressStepKind.Warning => "Avertissement",
            ProgressStepKind.Error => "Erreur",
            _ => "En cours"
        };
        IconGlyph = kind switch
        {
            ProgressStepKind.Success => "\uE73E",
            ProgressStepKind.Warning => "\uE7BA",
            ProgressStepKind.Error => "\uEA39",
            _ => "\uE823"
        };
        IconBrush = kind switch
        {
            ProgressStepKind.Success => CreateBrush(0x66, 0xBB, 0x6A),
            ProgressStepKind.Warning => CreateBrush(0xFF, 0xB7, 0x4D),
            ProgressStepKind.Error => CreateBrush(0xEF, 0x53, 0x50),
            _ => CreateBrush(0x1E, 0x5B, 0xB8)
        };
    }

    public ProgressStepKind Kind { get; }

    public string Message { get; }

    public string StateLabel { get; }

    public string IconGlyph { get; }

    public SolidColorBrush IconBrush { get; }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(0xFF, red, green, blue));
    }
}
