using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.App.Controls;

/// <summary>
/// Cellule unitaire de la pilule encapsulant ses anneaux de progression,
/// son logo de fournisseur et ses animations d'activité pilotées par VisualStateManager.
/// </summary>
public partial class PillCellView : UserControl
{
    public PillCellView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateGeometry();
        Unloaded += (_, _) => StopAnimations();
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

    public void UpdateAnimations(bool isHostVisible, bool ringShown, bool animationsAllowed = true)
    {
        if (DataContext is not CellModel cell || !isHostVisible || !ringShown || !animationsAllowed)
        {
            VisualStateManager.GoToElementState(RootLayout, "Idle", true);
            return;
        }

        var state = cell.Activity switch
        {
            ActivityKind.Running => "Running",
            ActivityKind.Attention => "Attention",
            _ => "Idle"
        };
        VisualStateManager.GoToElementState(RootLayout, state, true);
    }

    public void StopAnimations()
    {
        VisualStateManager.GoToElementState(RootLayout, "Idle", false);
    }
}
