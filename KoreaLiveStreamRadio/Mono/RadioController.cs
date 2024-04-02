using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
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

    private void Start()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioManager = AudioManager.instance;
        _audioSource.playOnAwake = false;
        _radio = RealtimeRadio.Radio;
        KoreaRadioBroadcasting._log.Info("Radio start");
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
            KoreaRadioBroadcasting._log.Info($"start radio: {channel.name}");
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

    private IEnumerator PlayAudio(string streamServer)
    {
        yield return StartCoroutine(LoadAudio(streamServer + ReplaceNumbers(_aacUrl, _initialParamNumber++.ToString())));
    }

    private IEnumerator DownloadAndParsePls(string streamServer, string hlsServer)
    {
        using var www = UnityWebRequest.Get(hlsServer);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("pls 파일 다운로드 중 오류 발생: " + www.error);
            yield break;
        }
        
        KoreaRadioBroadcasting._log.Info("Parse pls");
                
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
            yield return StartCoroutine(PlayAudio(RadioStreamServer.GetStreamServer(liveStation, streamChannel)));
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
        KoreaRadioBroadcasting._log.Info($"m3u8 start: {m3U8Url}");
        // M3U8 파일 다운로드
        var www = UnityWebRequest.Get(m3U8Url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to download M3U8 file: " + www.error);
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
                KoreaRadioBroadcasting._log.Info($"chunk server: {streamServer + line.Trim()}");
                // M3U8 파일 다운로드
                var www = UnityWebRequest.Get(streamServer + line.Trim());
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to download M3U8 file: " + www.error);
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

    private IEnumerator LoadAudio(string aacUrl)
    {
        KoreaRadioBroadcasting._log.Info($"aacUrl: {aacUrl}");
        var request = UnityWebRequest.Get(aacUrl);

        // Send the request
        yield return request.SendWebRequest();
        
        // Check for errors
        if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error fetching HLS stream: " + request.error);
        }
            
        ConvertAccToWavFile(aacUrl, 0);

        var www = UnityWebRequestMultimedia.GetAudioClip(GetWavFileName(0), AudioType.WAV);
        yield return www.SendWebRequest();
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.clip = DownloadHandlerAudioClip.GetContent(www);
        if (_isPaused)
        {
            _audioSource.Pause();
        }
        else
        {
            _audioSource.Play();
        }
        yield return new WaitForSeconds(_audioSource.clip.length - 0.085f);
    }

    private static void ConvertAccToWavFile(string aacUrl, int index)
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

    private static string GetWavFileName(int index)
    {
        return GetFileName(index, "wav");
    }

    private static string GetFileName(int index, string format)
    {
        return $"{Application.persistentDataPath}/radio_{index}.{format}";
    }

}