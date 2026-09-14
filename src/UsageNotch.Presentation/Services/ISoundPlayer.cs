namespace UsageNotch.Presentation.Services;

/// <summary>Joue un son système par son nom : Asterisk, Beep, Exclamation, Hand, Question.</summary>
public interface ISoundPlayer
{
    void Play(string soundName);
}
