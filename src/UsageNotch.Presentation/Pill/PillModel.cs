using UsageNotch.Core.Settings;

namespace UsageNotch.Presentation.Pill;

/// <summary>
/// Modèle complet de la pilule unifiée, supportant une ou deux cellules (Claude et Antigravity)
/// et calculant la couleur de bande pour le mode replié.
/// </summary>
public sealed record PillModel(
    IReadOnlyList<CellModel> Cells,
    double BodyLength,
    double WindowLength,
    string BandColor,
    ScreenEdge Edge = ScreenEdge.Top)
{
    public CellModel PrimaryCell => Cells.Count > 0 ? Cells[0] : throw new InvalidOperationException("La pilule ne contient aucune cellule.");
    public CellModel Cell1 => PrimaryCell;
    public CellModel? Cell2 => Cells.Count > 1 ? Cells[1] : null;
    public CellModel? Cell3 => Cells.Count > 2 ? Cells[2] : null;
    public CellModel? CellClaude => Cells.FirstOrDefault(c => c.ProviderId == "claude");
    public CellModel? CellAntigravity => Cells.FirstOrDefault(c => c.ProviderId == "antigravity");
    public CellModel? CellOpenAi => Cells.FirstOrDefault(c => c.ProviderId == "openai");
    public bool IsDual => Cells.Count > 1;
    public bool IsTriple => Cells.Count > 2;
}
