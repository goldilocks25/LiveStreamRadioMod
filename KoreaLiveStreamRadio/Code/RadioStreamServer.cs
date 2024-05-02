namespace KoreaLiveStreamRadio.Code;

public class RadioStreamServer
{
    private const string HlsServer = "https://radio.bsod.kr/stream/playback.pls";

    private RadioStreamServer()
    {
    }

    public static string GetHlsServerUrl(string station, string channel)
    {
        return $"{HlsServer}?stn={station}&ch={channel}";
    }
}