using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Colossal.Logging;
using KoreaLiveStreamRadio.Code;
using KoreaLiveStreamRadio.Patches;
using Game.Audio;
using Game.Audio.Radio;
using HarmonyLib;
using NAudio.Wave;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Networking;

namespace KoreaLiveStreamRadio.Mono;

[BurstCompile]
public class RadioController : MonoBehaviour
{
    private static Traverse _traverse;
    private static Radio.RadioPlayer _mRadioPlayer;
    private static int _aacUrlParamNumber;
    private static string _aacPath;
    private static string _chunkFileUrl;
    public static AudioSource AudioSource;
    private static AudioManager _audioManager;
    private static Radio _radio;
    private static bool _isPaused;
    private static bool _isPlaying;
    private static string _selectionChannel;
    private static readonly ILog LOG = KoreaRadioBroadcasting._log;
    private static bool _dummy;
    private static bool _isConverting;
    private static string streamServer;

    private void Start()
    {
        _traverse = RealtimeRadio.Traverse;
        _mRadioPlayer = _traverse.Field("m_RadioPlayer").GetValue<Radio.RadioPlayer>();
        _audioManager = AudioManager.instance;
        AudioSource = gameObject.AddComponent<AudioSource>();
        AudioSource.playOnAwake = false;
        AudioSource.loop = false;
        _radio = RealtimeRadio.Radio;
        LOG.Info("Radio start");
    }

    private void Update()
    {
        if (!_radio.isActive) return;
        if (!_radio.isEnabled) return;
        if (AudioSource == null) return;
        var channel = _radio.currentChannel;
        _isPaused = _radio.paused;
        _selectionChannel = channel.name;
        AudioSource.volume = _audioManager.radioVolume;
        if (_isPaused || _radio.muted)
        {
            AudioSource.volume = 0.0f;
        }
        if (channel.network == "Live korean radio")
        {
            if (_mRadioPlayer.isPlaying)
            {
                if (!_radio.hasEmergency)
                {
                    // Resolving issue of previous radio playback after emergency alert
                    _mRadioPlayer.Pause();
                }
            }
            string liveStation = null;
            string liveChannel = null;
            string streamChannel = null;
            
            switch (channel.name)
            {
                case "MBC mini music":
                    liveStation = "mbc";
                    liveChannel = "chm";
                    streamChannel = "chm";
                    break;
                case "MBC standard fm":
                    liveStation = "mbc";
                    liveChannel = "sfm";
                    streamChannel = "sfm";
                    break;
                case "MBC fm 4u":
                    liveStation = "mbc";
                    liveChannel = "fm4u";
                    streamChannel = "mfm";
                    break;
                case "SBS love fm":
                    liveStation = "sbs";
                    liveChannel = "lovefm";
                    streamChannel = "lovefm";
                    break;
                case "SBS power fm":
                    liveStation = "sbs";
                    liveChannel = "powerfm";
                    streamChannel = "powerfm";
                    break;
                case "SBS gorilradio m":
                    liveStation = "sbs";
                    liveChannel = "dmb";
                    streamChannel = "dmb";
                    break;
                case "KBS 1 radio":
                    liveStation = "kbs";
                    liveChannel = "1radio";
                    streamChannel = "1radio";
                    break;
                case "KBS 2 radio":
                    liveStation = "kbs";
                    liveChannel = "2radio";
                    streamChannel = "2radio";
                    break;
                case "KBS 3 radio":
                    liveStation = "kbs";
                    liveChannel = "3radio";
                    streamChannel = "3radio";
                    break;
                case "KBS 1 FM radio":
                    liveStation = "kbs";
                    liveChannel = "1fm";
                    streamChannel = "1fm";
                    break;
                case "KBS 2 FM radio":
                    liveStation = "kbs";
                    liveChannel = "2fm";
                    streamChannel = "2fm";
                    break;
                case "YTN radio":
                    liveStation = "ytn";
                    liveChannel = "";
                    streamChannel = "";
                    break;
                case "CBS music fm radio":
                    liveStation = "cbs";
                    liveChannel = "mfm";
                    streamChannel = "mfm";
                    break;
            }
            var liveStreamRadio = new LiveStreamRadio(liveStation, liveChannel, streamChannel);
            if (liveStation == null) return;

            if (_isPlaying) return;
            _isPlaying = true;
            _aacUrlParamNumber = 0;
            LOG.Info($"start radio: {channel.name}");
            StartCoroutine(Play(liveStreamRadio, channel.name));
        }
        else
        {
            _isPlaying = false;
        }
        
    }

    private void OnDisable()
    {
        AudioSource.Stop();
        _isPlaying = false;
        _isPaused = false;
    }

    private IEnumerator ConvertAndPlayAudio()
    {
        var aacUrl = streamServer + ReplaceNumbers(_aacPath, _aacUrlParamNumber++.ToString());
        // LOG.Info($"convert start: {aacUrl}");
        yield return ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                _isConverting = true;
                ConvertAccToWavFile(aacUrl);
            }
            catch
            {
                LOG.Warn("So fast stream ");
                _dummy = true;
            }
            finally
            {
                _isConverting = false;
            }
        });
        
        yield return StartCoroutine(PlayAudio());
    }

    private IEnumerator DownloadAndParsePls(string hlsServer)
    {
        using var www = UnityWebRequest.Get(hlsServer);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
#if DEBUG
            Debug.LogWarning("pls 파일 다운로드 중 오류 발생: " + www.error);
#else
            LOG.Warn("Failed to download M3U8 file: " + www.error);
#endif
            yield break;
        }
        
        LOG.Info("Parse pls");
                
        // pls 파일 내 스트리밍 URL 파싱
        var m3U8File = ParsePls(www.downloadHandler.text);

        if (m3U8File == null)
            yield break;

        streamServer = m3U8File.Substring(0, m3U8File.LastIndexOf("/", StringComparison.Ordinal) + 1);
        yield return StartCoroutine(DownloadPls(m3U8File));
    }

    private IEnumerator Play(LiveStreamRadio liveStreamRadio, string gameChannel)
    {
        yield return StartCoroutine(DownloadAndParsePls(RadioStreamServer.GetHlsServerUrl(liveStreamRadio.LiveStation, liveStreamRadio.LiveChannel))
        );
        while (_selectionChannel == gameChannel && !_dummy)
        {
            yield return StartCoroutine(ConvertAndPlayAudio());
        }
        _isPlaying = false;
        _dummy = false;
    }

    private static string ParsePls(string plsContent)
    {
        var lines = plsContent.Split('\n');
        const string match = "File1=";
        return (from line in lines where line.StartsWith(match) select line.Substring(match.Length).Trim()).FirstOrDefault();
    }

    private IEnumerator DownloadPls(string m3U8Url)
    {
        LOG.Info($"m3u8 start: {m3U8Url}");
        // M3U8 파일 다운로드
        var www = UnityWebRequest.Get(m3U8Url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
#if DEBUG
            Debug.LogWarning("Failed to download M3U8 file: " + www.error);
#else
                        LOG.Warn("Failed to download M3U8 file: " + www.error);
#endif
            yield break;
        }

        yield return StartCoroutine(ExtractUrls(www.downloadHandler.text));
    }
    
    private IEnumerator ExtractUrls(string m3U8Content)
    {
        // 각 줄을 분리하여 순회
        var lines = m3U8Content.Split('\n');
        
        const string pattern = @"#EXTINF:(\d+\.\d+),";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrEmpty(line)) continue;
            if (line.Contains(".m3u8"))
            {
                _chunkFileUrl = streamServer + line.Trim();
                LOG.Info($"chunk server: {_chunkFileUrl}");
                // M3U8 파일 다운로드
                var www = UnityWebRequest.Get(_chunkFileUrl);
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
#if DEBUG
                    Debug.LogWarning("Failed to download M3U8 file: " + www.error);
#else
                        LOG.Warn("Failed to download M3U8 file: " + www.error);
#endif
                    
                    yield break;
                }

                yield return StartCoroutine(ExtractUrls(www.downloadHandler.text));
                yield break;
            }
            
            var match = Regex.Match(line, pattern);
            if (!match.Success) continue;

            if (_aacUrlParamNumber == 0)
            {
                _aacPath = lines[++i].Trim();
                _aacUrlParamNumber = ExtractNumbers(_aacPath);
            }
            break;
        }
    }

    private static int ExtractNumbers(string aacPath)
    {
        var match = Regex.Match(aacPath, @"\d+\.");
        return int.Parse(match.Value.Replace(".", ""));
    }

    private static string ReplaceNumbers(string input, string newNumbers)
    {
        var match = Regex.Match(input, @"\d+\.");
        return input.Substring(0, match.Index) + newNumbers+ "." + input.Substring(match.Index + match.Length);
    }

    [BurstCompile]
    private IEnumerator PlayAudio()
    {
        yield return new WaitWhile(() => _isConverting);
        if (_dummy) yield break;
        var www = UnityWebRequestMultimedia.GetAudioClip(GetWavFileName(), AudioType.WAV);
        yield return www.SendWebRequest();
        
        if (www.result != UnityWebRequest.Result.Success)
        {
            LOG.Warn($"Wave load fail: {www.error}");
            _aacUrlParamNumber -= 1;
            yield return new WaitForSeconds(1.0f);
            yield break;
        }
        
        var nextAudioSource = gameObject.AddComponent<AudioSource>();
        nextAudioSource.playOnAwake = AudioSource.playOnAwake;
        nextAudioSource.loop = AudioSource.loop;
        nextAudioSource.clip = DownloadHandlerAudioClip.GetContent(www);
        
        var segmentLength = 0.0f;
        
        if (AudioSource.isPlaying)
        {
            var clipLength = AudioSource.clip.length;
            segmentLength = Math.Abs(clipLength - Math.Abs(FindNearestInteger(clipLength)));
            yield return new WaitWhile(() => AudioSource.isPlaying && AudioSource.time < clipLength - segmentLength);
        }
        
        if (!_isPaused)
        {
            nextAudioSource.volume = AudioSource.volume;
            nextAudioSource.Play();
        }

        yield return new WaitWhile(() => AudioSource.isPlaying);
        Destroy(AudioSource);
        AudioSource = nextAudioSource;
        yield return new WaitForSeconds(segmentLength);
    }
    
    // FIXME : Start of wave file Audio truncated
    private static void ConvertAccToWavFile(string aacUrl)
    {
        using var aacReader = new MediaFoundationReader(aacUrl);
        using var aacToWav = new WaveFormatConversionStream(aacReader.WaveFormat, aacReader);
        using var wavWriter = new WaveFileWriter(GetWavFileName(), aacReader.WaveFormat);
        var buffer = new byte[4096 * 4096];
        int bytesRead;

        while ((bytesRead = aacToWav.Read(buffer, 0, buffer.Length)) > 0)
        {
            wavWriter.Write(buffer, 0, bytesRead);
        }
    }

    private static string GetWavFileName()
    {
        return GetFileName(0, "wav");
    }

    private static string GetFileName(int index, string format)
    {
        return $"{Application.persistentDataPath}/radio_{index}.{format}";
    }

    private class LiveStreamRadio
    {
        public string LiveStation
        {
            get;
        }
        public string LiveChannel
        {
            get;
        }
        public string StreamChannel
        {
            get;
        }

        internal LiveStreamRadio(string liveStation, string liveChannel, string streamChannel)
        {
            LiveStation = liveStation;
            LiveChannel = liveChannel;
            StreamChannel = streamChannel;
        }
    }

    private static int FindNearestInteger(double number)
    {
        var floorValue = Math.Floor(number);
        var ceilValue = Math.Ceiling(number);

        if (number - floorValue < ceilValue - number)
        {
            return (int)floorValue;
        }

        return (int)ceilValue;
    }

}