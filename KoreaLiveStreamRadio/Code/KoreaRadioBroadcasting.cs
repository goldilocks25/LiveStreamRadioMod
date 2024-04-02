using System.Collections.Generic;
using KoreaLiveStreamRadio.Mono;
using KoreaLiveStreamRadio.Patches;
using Colossal.Logging;
using Game.Audio.Radio;
using HarmonyLib;
using UnityEngine;

namespace KoreaLiveStreamRadio.Code;

public class KoreaRadioBroadcasting
{

    public static ILog _log = KoreaLiveStreamRadioMod.log;
    private static Traverse _traverse;
    private static readonly GameObject RadioController = new("RadioController");
        
    public static void CreateBroadCast()
    {
        _traverse = RealtimeRadio.Traverse;
        RadioController.AddComponent<RadioController>();
        var citiesRadioNetwork = _traverse.Field("m_Networks").GetValue<Dictionary<string, Radio.RadioNetwork>>();
        var citiesRadioChannels = _traverse.Field("m_RadioChannels").GetValue<Dictionary<string, Radio.RuntimeRadioChannel>>();
        
        citiesRadioNetwork.Add(GetKoreaRadioNetwork(), new Radio.RadioNetwork
        {
            name = GetKoreaRadioNetwork(),
            nameId = GetKoreaRadioNetwork(),
            icon = GetAssets("korean_radio.png"),
            description = "실시간 한국 라디오 방송국입니다.",
            descriptionId = "실시간 한국 라디오 방송국입니다.",
            allowAds = false,
        });
        citiesRadioChannels.Add("MBC mini music", CreateRadioChannel("MBC mini music", "MBC 미니뮤직 채널입니다.", "mbc_radio.jpg"));
        citiesRadioChannels.Add("MBC standard fm", CreateRadioChannel("MBC standard fm", "MBC 표준 FM 채널입니다.", "mbc_radio.jpg"));
        citiesRadioChannels.Add("MBC fm 4u", CreateRadioChannel("MBC fm 4u", "MBC FM4U 채널입니다.", "mbc_radio.jpg"));
        citiesRadioChannels.Add("SBS love fm", CreateRadioChannel("SBS love fm", "SBS 러브FM 채널입니다.", "sbs_radio.jpg"));
        citiesRadioChannels.Add("SBS power fm", CreateRadioChannel("SBS power fm", "SBS 파워FM 채널입니다.", "sbs_radio.jpg"));
        citiesRadioChannels.Add("SBS gorilradio m", CreateRadioChannel("SBS gorilradio m", "SBS 고릴라디오M 채널입니다.", "sbs_radio.jpg"));
        citiesRadioChannels.Add("KBS 1 radio", CreateRadioChannel("KBS 1 radio", "KBS 1 라디오 채널입니다.", "kbs_radio.png"));
        citiesRadioChannels.Add("KBS 2 radio", CreateRadioChannel("KBS 2 radio", "KBS 2 라디오 채널입니다.", "kbs_radio.png"));
        citiesRadioChannels.Add("KBS 3 radio", CreateRadioChannel("KBS 3 radio", "KBS 3 라디오 채널입니다.", "kbs_radio.png"));
        citiesRadioChannels.Add("KBS 1 FM radio", CreateRadioChannel("KBS 1 FM radio", "KBS 1 FM 라디오 채널입니다.", "kbs_radio.png"));
        citiesRadioChannels.Add("KBS 2 FM radio", CreateRadioChannel("KBS 2 FM radio", "KBS 2 FM 라디오 채널입니다.", "kbs_radio.png"));
        citiesRadioChannels.Add("YTN radio", CreateRadioChannel("YTN radio", "YTN 라디오 채널입니다.", "ytn_radio.png"));
        citiesRadioChannels.Add("CBS music fm radio", CreateRadioChannel("CBS music fm radio", "CBS 음악FM 라디오 채널입니다.", "cbs_music_radio.png"));
        _traverse.Field("m_Networks").SetValue(citiesRadioNetwork);
        _traverse.Field("m_RadioChannels").SetValue(citiesRadioChannels);
        _traverse.Field("m_CachedRadioChannelDescriptors").SetValue(null);
        _log.Info($"{nameof(KoreaRadioBroadcasting)} Created");
    }

    private static Radio.Program CreateProgram(string name, string icon)
    {
        return new Radio.Program
        {
            name = name,
            description = "라디오",
            icon = icon,
            startTime = "00:00",
            endTime = "23:59",
            loopProgram = false,
            pairIntroOutro = false,
            segments = [],
        };
    }

    private static Radio.RuntimeRadioChannel CreateRadioChannel(string name, string description, string icon)
    {
        return new Radio.RadioChannel
        {
            name = name,
            nameId = name,
            description = description,
            network = GetKoreaRadioNetwork(),
            icon = GetAssets(icon),
            programs =
            [
                CreateProgram(name, GetAssets(icon))
            ]
        }.CreateRuntime("");
    }

    private static string GetKoreaRadioNetwork()
    {
        return "Live korean radio";
    }

    private static string GetAssets(string name)
    {
        return $@"Assets\{name}";
    }
}