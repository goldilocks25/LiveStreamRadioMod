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
    private static int _initialParamNumber;
    private static string _aacUrl;
    private static AudioSource _audioSource;
    private static AudioManager _audioManager;
    private static Radio _radio;
    private static bool _isPaused;
    private static bool _isPlaying;
    private static string _selectionChannel;
    private static readonly ILog LOG = KoreaRadioBroadcasting._log;

    private void Start()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioManager = AudioManager.instance;
        _audioSource.playOnAwake = false;
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
        _audioSource.volume = _audioManager.radioVolume;
        if (_isPaused || _radio.muted)
        {
            _audioSource.volume = 0.0f;
        }
        if (channel.network == "Live korean radio")
        {
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

            if (liveStation == null) return;

            if (_isPlaying) return;
            _isPlaying = true;
            LOG.Info($"start radio: {channel.name}");
            StopCoroutine("Play");
            StartCoroutine(Play(liveStation, liveChannel, streamChannel, channel.name));
        }
        else
        {
            _isPlaying = false;
        }
        
    }

    private void OnDisable()
    {
        _audioSource.Stop();
        _isPlaying = false;
        _isPaused = false;
    }

    private IEnumerator ConvertAndPlayAudio(string streamServer)
    {
        var aacUrl = streamServer + ReplaceNumbers(_aacUrl, _initialParamNumber++.ToString());
        LOG.Info($"Aac Url: {aacUrl}");
        ConvertAccToWavFile(aacUrl, 0);
        yield return StartCoroutine(PlayAudio());
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

    private IEnumerator Play(string liveStation, string liveChannel, string streamChannel, string gameChannel)
    {
        yield return StartCoroutine(DownloadAndParsePls(
            RadioStreamServer.GetStreamServer(liveStation, streamChannel), RadioStreamServer.GetHlsServerUrl(liveStation, liveChannel))
        );
        while (_selectionChannel == gameChannel)
        {
            yield return StartCoroutine(ConvertAndPlayAudio(RadioStreamServer.GetStreamServer(liveStation, streamChannel)));
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
            _initialParamNumber = ExtractNumbers(_aacUrl);
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

    private IEnumerator PlayAudio()
    {
        yield return new WaitWhile(() => isConverting);
        var www = UnityWebRequestMultimedia.GetAudioClip(GetWavFileName(0), AudioType.WAV);
        yield return www.SendWebRequest();
        
        var nextAudioSource = gameObject.AddComponent<AudioSource>();
        var clip = DownloadHandlerAudioClip.GetContent(www);
        nextAudioSource.clip = clip;
        
        yield return new WaitWhile(() =>
        {
            if (!_audioSource.isPlaying)
                return false;
            
            var currentTime = _audioSource.time;
            var clipLength = _audioSource.clip.length;
            var correction = clipLength < 3 ? 0.06f : clipLength < 4 ? 0.08f : 0.1f;
            return currentTime < clipLength - correction;
        });
        
        if (_isPaused)
        {
            nextAudioSource.Pause();
        }
        else
        {
            nextAudioSource.Play();
        }

        _audioSource = nextAudioSource;
    }

    private static bool isConverting = false;
    
    // FIXME : Start of wave file Audio truncated
    private static void ConvertAccToWavFile(string aacUrl, int index)
    {
        if (isConverting)
        {
            return;
        }

        isConverting = true;
        ThreadPool.QueueUserWorkItem(state =>
        {
            try
            {
                using var aacReader = new MediaFoundationReader(aacUrl);
                var aacToWav = new WaveFormatConversionStream(new WaveFormat(44100, 16, 2), aacReader);
                using var wavWriter = new WaveFileWriter(GetWavFileName(index), new WaveFormat(44100, 16, 2));
                var buffer = new byte[4096];
                int bytesRead;
                        
                while ((bytesRead = aacToWav.Read(buffer, 0, buffer.Length)) > 0)
                {
                    wavWriter.Write(buffer, 0, bytesRead);
                }
            }
            finally
            {
                isConverting = false;
            }
        });
    }

    private static string GetWavFileName(int index)
    {
        return GetFileName(index, "wav");
    }

    private static string GetFileName(int index, string format)
    {
        return $"{Application.persistentDataPath}/radio_{index}.{format}";
    }

}