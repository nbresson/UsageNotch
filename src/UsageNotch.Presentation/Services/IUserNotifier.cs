namespace UsageNotch.Presentation.Services;

/// <summary>Notification système visible par l'utilisateur. <paramref name="critical"/> : limite atteinte.</summary>
public interface IUserNotifier
{
    void Show(string title, string message, bool critical);
}
