using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.App.Controls;

/// <summary>
/// Cellule unitaire de la pilule encapsulant ses anneaux de progression,
/// son logo de fournisseur et ses animations d'activité (rotation et pulsation).
/// </summary>
public partial class PillCellView : UserControl
{
    private bool _spinRunning;
    private bool _pulseRunning;

    public PillCellView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateGeometry();
    }

    public void SetOrientation(bool vertical)
    {
        RootStack.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        RingHost.Margin = vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 6, 0);
    }

    public void UpdateGeometry()
    {
        if (DataContext is CellModel cell)
        {
            var geom = BrandGeometry.ForProvider(cell.ProviderId);
            LogoMuted.Data = geom;
            LogoTint.Data = geom;
        }
    }

    public void UpdateAnimations(bool isHostVisible, bool ringShown)
    {
        if (DataContext is not CellModel cell || !isHostVisible)
        {
            SetAnimation("Spin", ref _spinRunning, false);
            SetAnimation("Pulse", ref _pulseRunning, false);
            return;
        }

        SetAnimation("Spin", ref _spinRunning, ringShown && cell.Activity == ActivityKind.Running);
        SetAnimation("Pulse", ref _pulseRunning, ringShown && cell.Activity == ActivityKind.Attention);
    }

    private void SetAnimation(string key, ref bool running, bool desired)
    {
        if (running == desired) return;
        var sb = (Storyboard)Resources[key];
        if (desired) sb.Begin(this, isControllable: true);
        else sb.Stop(this);
        running = desired;
    }
}
