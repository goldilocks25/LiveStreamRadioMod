﻿using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Colossal.Logging;
using KoreaLiveStreamRadio.Code;
using KoreaLiveStreamRadio.Patches;
using Game.Audio;
using Game.Audio.Radio;
using NAudio.Wave;
using UnityEngine;
using UnityEngine.Networking;

namespace KoreaLiveStreamRadio.Mono;

public class RadioController : MonoBehaviour
{
    private static int _aacUrlParamNumber;
    private static string _aacUrl;
    public static AudioSource AudioSource;
    private static AudioManager _audioManager;
    private static Radio _radio;
    private static bool _isPaused;
    private static bool _isPlaying;
    private static string _selectionChannel;
    private static readonly ILog LOG = KoreaRadioBroadcasting._log;
    private static bool _isConverting = true;

    private void Start()
    {
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
            string liveStation = null;
            string liveChannel = null;
            string streamChannel = null;
            var conversion = 0.65f;
            
            switch (channel.name)
            {
                case "MBC mini music":
                    liveStation = "mbc";
                    liveChannel = "chm";
                    streamChannel = "chm";
                    conversion = 0.06f;
                    break;
                case "MBC standard fm":
                    liveStation = "mbc";
                    liveChannel = "sfm";
                    streamChannel = "sfm";
                    conversion = 0.06f;
                    break;
                case "MBC fm 4u":
                    liveStation = "mbc";
                    liveChannel = "fm4u";
                    streamChannel = "mfm";
                    conversion = 0.06f;
                    break;
                case "SBS love fm":
                    liveStation = "sbs";
                    liveChannel = "lovefm";
                    streamChannel = "lovefm";
                    conversion = 0.05f;
                    break;
                case "SBS power fm":
                    liveStation = "sbs";
                    liveChannel = "powerfm";
                    streamChannel = "powerfm";
                    conversion = 0.0375f;
                    break;
                case "SBS gorilradio m":
                    liveStation = "sbs";
                    liveChannel = "dmb";
                    streamChannel = "dmb";
                    conversion = 0.0375f;
                    break;
                case "KBS 1 radio":
                    liveStation = "kbs";
                    liveChannel = "1radio";
                    streamChannel = "1radio";
                    conversion = 0.055f;
                    break;
                case "KBS 2 radio":
                    liveStation = "kbs";
                    liveChannel = "2radio";
                    streamChannel = "2radio";
                    conversion = 0.055f;
                    break;
                case "KBS 3 radio":
                    liveStation = "kbs";
                    liveChannel = "3radio";
                    streamChannel = "3radio";
                    conversion = 0.055f;
                    break;
                case "KBS 1 FM radio":
                    liveStation = "kbs";
                    liveChannel = "1fm";
                    streamChannel = "1fm";
                    conversion = 0.06f;
                    break;
                case "KBS 2 FM radio":
                    liveStation = "kbs";
                    liveChannel = "2fm";
                    streamChannel = "2fm";
                    conversion = 0.06f;
                    break;
                case "YTN radio":
                    liveStation = "ytn";
                    liveChannel = "";
                    streamChannel = "";
                    conversion = 0.06f;
                    break;
                case "CBS music fm radio":
                    liveStation = "cbs";
                    liveChannel = "mfm";
                    streamChannel = "mfm";
                    conversion = 0.06f;
                    break;
            }
            var liveStreamRadio = new LiveStreamRadio(liveStation, liveChannel, streamChannel, conversion);
            if (liveStation == null) return;

            if (_isPlaying) return;
            _isPlaying = true;
            LOG.Info($"start radio: {channel.name}");
            StopCoroutine("Play");
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

    private IEnumerator ConvertAndPlayAudio(string streamServer, float correction)
    {
        var aacUrl = streamServer + ReplaceNumbers(_aacUrl, _aacUrlParamNumber++.ToString());
        LOG.Info($"convert start: {aacUrl}");
        yield return ThreadPool.QueueUserWorkItem(_ => ConvertAccToWavFile(aacUrl, 0));
        yield return StartCoroutine(PlayAudio(correction));
    }

    private IEnumerator DownloadAndParsePls(string streamServer, string hlsServer)
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
                
        yield return StartCoroutine(DownloadPls(streamServer, m3U8File));
    }

    private IEnumerator Play(LiveStreamRadio liveStreamRadio, string gameChannel)
    {
        var streamServer =
            RadioStreamServer.GetStreamServer(liveStreamRadio.LiveStation, liveStreamRadio.StreamChannel);
        yield return StartCoroutine(DownloadAndParsePls(streamServer,
            RadioStreamServer.GetHlsServerUrl(liveStreamRadio.LiveStation, liveStreamRadio.LiveChannel))
        );
        while (_selectionChannel == gameChannel)
        {
            yield return StartCoroutine(ConvertAndPlayAudio(streamServer, liveStreamRadio.Correction));
        }
        _isPlaying = false;
    }

    private static string ParsePls(string plsContent)
    {
        var lines = plsContent.Split('\n');
        const string match = "File1=";
        return (from line in lines where line.StartsWith(match) select line.Substring(match.Length).Trim()).FirstOrDefault();
    }

    private IEnumerator DownloadPls(string streamServer, string m3U8Url)
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

        yield return StartCoroutine(ExtractUrls(streamServer, www.downloadHandler.text));
    }
    
    private IEnumerator ExtractUrls(string streamServer, string m3U8Content)
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
                LOG.Info($"chunk server: {streamServer + line.Trim()}");
                // M3U8 파일 다운로드
                var www = UnityWebRequest.Get(streamServer + line.Trim());
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

                yield return StartCoroutine(ExtractUrls(streamServer, www.downloadHandler.text));
                yield break;
            }
            var match = Regex.Match(line, pattern);
            if (!match.Success) continue;
            
            _aacUrl = lines[++i].Trim();
            _aacUrlParamNumber = ExtractNumbers(_aacUrl);
            break;
        }
    }

    private static int ExtractNumbers(string input)
    {
        var match = Regex.Match(input, @"\d+\.");
        return int.Parse(match.Value.Replace(".", ""));
    }

    private static string ReplaceNumbers(string input, string newNumbers)
    {
        var match = Regex.Match(input, @"\d+\.");
        return input.Substring(0, match.Index) + newNumbers+ "." + input.Substring(match.Index + match.Length);
    }

    private IEnumerator PlayAudio(float correction)
    {
        yield return new WaitWhile(() => _isConverting);
        var www = UnityWebRequestMultimedia.GetAudioClip(GetWavFileName(0), AudioType.WAV);
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
        nextAudioSource.volume = AudioSource.volume;
        nextAudioSource.clip = DownloadHandlerAudioClip.GetContent(www);
        
        yield return new WaitWhile(() =>
        {
            if (!AudioSource.isPlaying)
                return false;
            
            // Missing buffer after conversion to wave file
            return AudioSource.time < AudioSource.clip.length - correction;
        });
        
        if (_isPaused)
        {
            nextAudioSource.Pause();
        }
        else
        {
            nextAudioSource.Play();
        }

        yield return new WaitWhile(() => AudioSource.isPlaying);
        Destroy(AudioSource);
        AudioSource = nextAudioSource;
    }
    
    // FIXME : Start of wave file Audio truncated
    private static void ConvertAccToWavFile(string aacUrl, int index)
    {
        _isConverting = true;
        try
        {
            using var aacReader = new MediaFoundationReader(aacUrl);
            var aacToWav = new WaveFormatConversionStream(new WaveFormat(44100, 16, 2), aacReader);
            using var wavWriter = new WaveFileWriter(GetWavFileName(index), new WaveFormat(44100, 16, 2));
            var buffer = new byte[4096 * 4096];
            int bytesRead;

            while ((bytesRead = aacToWav.Read(buffer, 0, buffer.Length)) > 0)
            {
                wavWriter.Write(buffer, 0, bytesRead);
            }
            wavWriter.Close();
        }
        catch
        {
            LOG.Warn("Realtime so fast");
            _aacUrlParamNumber -= 1;
        }

        _isConverting = false;
    }

    private static string GetWavFileName(int index)
    {
        return GetFileName(index, "wav");
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
        public float Correction
        {
            get;
        }

        internal LiveStreamRadio(string liveStation, string liveChannel, string streamChannel, float correction)
        {
            LiveStation = liveStation;
            LiveChannel = liveChannel;
            StreamChannel = streamChannel;
            Correction = correction;
        }
    }

}