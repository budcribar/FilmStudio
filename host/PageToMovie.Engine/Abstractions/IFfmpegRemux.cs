namespace PageToMovie.Engine.Abstractions;

/// <summary>
/// Optional native ffmpeg binary for gen-time silence-trim / extend-tail when
/// <c>PageToMovie:UseNativeFfmpeg=true</c>. Scene/WIP remux lives in the browser (ffmpeg.wasm).
/// </summary>
public interface IFfmpegRemux
{
    string FfmpegPath { get; }
    bool IsAvailable();
}
