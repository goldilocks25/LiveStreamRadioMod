namespace KoreaLiveStreamRadio.Code;

public class RadioStreamServer
{
    private const string HlsServer = "https://radio.bsod.kr/stream/playback.pls";

    private RadioStreamServer()
    {
    }

    public static string GetStreamServer(string station, string channel)
    {
        return station switch
        {
            "mbc" => $"https://minicw2.imbc.com/d2{channel}/_definst_/{channel}.stream/",
            "sbs" when channel == "lovefm" => "https://radiolive.sbs.co.kr/lovepc/lovefm.stream/",
            "sbs" when channel == "powerfm" => "https://radiolive.sbs.co.kr/powerpc/powerfm.stream/",
            "sbs" when channel == "dmb" => "https://radiolive.sbs.co.kr/sbsdmbpc/sbsdmb.stream/",
            "kbs" when channel == "1radio" => "https://1radio.gscdn.kbs.co.kr/",
            "kbs" when channel == "2radio" => "https://2radio-ad.gscdn.kbs.co.kr/",
            "kbs" when channel == "3radio" => "https://3radio.gscdn.kbs.co.kr/",
            "kbs" when channel == "1fm" => "https://1fm.gscdn.kbs.co.kr/",
            "kbs" when channel == "2fm" => "https://2fm-ad.gscdn.kbs.co.kr/",
            "ytn" => "https://radiolive.ytn.co.kr/radio/_definst_/20211118_fmlive/",
            "cbs" when channel == "mfm" => "https://aac.cbs.co.kr/cbs939/_definst_/cbs939.stream/",
            _ => ""
        };
    }

    public static string GetHlsServerUrl(string station, string channel)
    {
        return $"{HlsServer}?stn={station}&ch={channel}";
    }
}