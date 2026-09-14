using System.Media;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

public sealed class SystemSoundPlayer : ISoundPlayer
{
    public void Play(string soundName)
    {
        var sound = soundName switch
        {
            "Beep" => SystemSounds.Beep,
            "Exclamation" => SystemSounds.Exclamation,
            "Hand" => SystemSounds.Hand,
            "Question" => SystemSounds.Question,
            _ => SystemSounds.Asterisk,
        };
        sound.Play();
    }
}
