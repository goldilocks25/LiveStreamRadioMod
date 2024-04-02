namespace KoreaLiveStreamRadio.Code;

public class RadioStreamServer
{
    private const string HlsServer = "https://radio.bsod.kr/stream/playback.pls";

    private RadioStreamServer()
    {
    }

    public static string GetStreamServer(string station, string channel)
    {
        if (station == "mbc")
        {
            return $"https://minicw.imbc.com/d{channel}/_definst_/{channel}.stream/";
        }
        if (station == "sbs")
        {
            if (channel == "lovefm")
            {
                return "https://radiolive.sbs.co.kr/lovepc/lovefm.stream/";
            }

            if (channel == "powerfm")
            {
                return "https://radiolive.sbs.co.kr/powerpc/powerfm.stream/";
            }

            if (channel == "dmb")
            {
                return "https://radiolive.sbs.co.kr/sbsdmbpc/sbsdmb.stream/";
            }
        }

        if (station == "kbs")
        {
            if (channel == "1radio")
            {
                return "https://1radio.gscdn.kbs.co.kr/";
            }

            if (channel == "2radio")
            {
                return "https://2radio-ad.gscdn.kbs.co.kr/";
            }

            if (channel == "3radio")
            {
                return "https://3radio.gscdn.kbs.co.kr/";
            }

            if (channel == "1fm")
            {
                return "https://1fm.gscdn.kbs.co.kr/";
            }

            if (channel == "2fm")
            {
                return "https://2fm-ad.gscdn.kbs.co.kr/";
            }
        }

        if (station == "ytn")
        {
            return "https://radiolive.ytn.co.kr/radio/_definst_/20211118_fmlive/";
        }

        if (station == "cbs")
        {
            if (channel == "mfm")
            {
                return "https://aac.cbs.co.kr/cbs939/_definst_/cbs939.stream/";
            }
        }

        return "";
    }

    public static string GetHlsServerUrl(string station, string channel)
    {
        return $"{HlsServer}?stn={station}&ch={channel}";
    }
}